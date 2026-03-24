param location string
param environmentName string
param deploymentMode string
param appName string
param tags object = {}
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param appServicePlanSku object
param serviceBusSku string
param sqlDatabaseSkuName string
param sqlDatabaseMaxSizeBytes int
param storageSku string
param blobContainers array
param serviceBusQueues array

var isProduction = deploymentMode == 'production'
var compactSuffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().name, environmentName))

var storageAccountName = take(replace('st${appName}${environmentName}${compactSuffix}', '-', ''), 24)
var logAnalyticsWorkspaceName = 'log-${appName}-${environmentName}'
var appInsightsName = 'appi-${appName}-${environmentName}'
var managedIdentityName = 'id-${appName}-${environmentName}'
var keyVaultName = take(replace('kv-${appName}-${environmentName}-${compactSuffix}', '-', ''), 24)
var sqlServerName = take(replace('sql-${appName}-${environmentName}-${compactSuffix}', '-', ''), 63)
var sqlDatabaseName = 'sqldb-${appName}-${environmentName}'
var serviceBusNamespaceName = take(replace('sb-${appName}-${environmentName}-${compactSuffix}', '-', ''), 50)
var appServicePlanName = 'asp-${appName}-${environmentName}'
var apiAppName = take(replace('app-${appName}-api-${environmentName}-${compactSuffix}', '-', ''), 60)
var workerFunctionAppName = take(replace('func-${appName}-wrk-${environmentName}-${compactSuffix}', '-', ''), 60)

module monitoringModule './monitoring.bicep' = {
  name: 'monitoring-${environmentName}'
  params: {
    location: location
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    applicationInsightsName: appInsightsName
    tags: tags
  }
}

module storageModule './storage.bicep' = {
  name: 'storage-${environmentName}'
  params: {
    location: location
    storageAccountName: storageAccountName
    storageSku: storageSku
    blobContainers: blobContainers
    tags: tags
  }
}

module identityModule './identity.bicep' = {
  name: 'identity-${environmentName}'
  params: {
    location: location
    managedIdentityName: managedIdentityName
    tags: tags
  }
}

module keyVaultModule './key-vault.bicep' = {
  name: 'keyvault-${environmentName}'
  params: {
    location: location
    keyVaultName: keyVaultName
    managedIdentityPrincipalId: identityModule.outputs.principalId
    tags: tags
  }
  dependsOn: [
    identityModule
  ]
}

module sqlModule './sql.bicep' = if (isProduction) {
  name: 'sql-${environmentName}'
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    sqlDatabaseSkuName: sqlDatabaseSkuName
    sqlDatabaseMaxSizeBytes: sqlDatabaseMaxSizeBytes
    tags: tags
  }
}

module serviceBusModule './service-bus.bicep' = if (isProduction) {
  name: 'servicebus-${environmentName}'
  params: {
    location: location
    serviceBusNamespaceName: serviceBusNamespaceName
    serviceBusSku: serviceBusSku
    queueNames: serviceBusQueues
    tags: tags
  }
}

module computeModule './compute.bicep' = {
  name: 'compute-${environmentName}'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    appServicePlanSku: appServicePlanSku
    apiAppName: apiAppName
    workerFunctionAppName: workerFunctionAppName
    managedIdentityResourceId: identityModule.outputs.resourceId
    storageAccountName: storageModule.outputs.storageAccountName
    appInsightsConnectionString: monitoringModule.outputs.applicationInsightsConnectionString
    keyVaultUri: keyVaultModule.outputs.vaultUri
    platformMode: isProduction ? 'Production' : 'Lean'
    persistenceMode: 'InMemory'
    messagingMode: isProduction ? 'ServiceBus' : 'Queue'
    hostingMode: isProduction ? 'Dedicated' : 'FunctionsConsumption'
    targetPersistenceMode: isProduction ? 'Sql' : 'Table'
    targetMessagingMode: isProduction ? 'ServiceBus' : 'Queue'
    targetHostingMode: isProduction ? 'Dedicated' : 'FunctionsConsumption'
    sqlConnectionStringPlaceholder: isProduction ? 'Server=tcp:${sqlModule.outputs.sqlServerFqdn},1433;Initial Catalog=${sqlModule.outputs.sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' : ''
    serviceBusFullyQualifiedNamespace: isProduction ? serviceBusModule.outputs.fullyQualifiedNamespace : ''
    tags: tags
  }
  dependsOn: [
    monitoringModule
    storageModule
    identityModule
    keyVaultModule
  ]
}

output apiAppName string = isProduction ? apiAppName : ''
output workerFunctionAppName string = workerFunctionAppName
output keyVaultName string = keyVaultName
output storageAccountName string = storageAccountName
output sqlServerName string = isProduction ? sqlServerName : ''
output sqlDatabaseName string = isProduction ? sqlDatabaseName : ''
output serviceBusNamespaceName string = isProduction ? serviceBusNamespaceName : ''
