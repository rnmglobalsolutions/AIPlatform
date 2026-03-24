targetScope = 'subscription'

@description('Azure location for the resource group and all regional resources.')
param location string

@description('Deployment environment name, for example dev or prod.')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Deployment mode controlling which infrastructure footprint is created.')
@allowed([
  'lean'
  'production'
])
param deploymentMode string = 'lean'

@description('Application short name used in resource naming.')
param appName string = 'aimap'

@description('Resource group name for the environment.')
param resourceGroupName string

@description('Tags applied to all supported resources.')
param tags object = {}

@description('SQL admin login name. Required for production mode.')
param sqlAdminLogin string = ''

@secure()
@description('SQL admin login password. Required for production mode.')
param sqlAdminPassword string = ''

@description('Dedicated App Service plan SKU used only in production mode for the ASP.NET Core API.')
param productionAppServicePlanSku object = {
  name: 'B1'
  tier: 'Basic'
  size: 'B1'
  capacity: 1
}

@description('Service Bus SKU.')
@allowed([
  'Standard'
  'Premium'
])
param serviceBusSku string = 'Standard'

@description('SQL database SKU name.')
param sqlDatabaseSkuName string = 'Basic'

@description('SQL database max size in bytes.')
param sqlDatabaseMaxSizeBytes int = 2147483648

@description('Storage account SKU.')
param storageSku string = 'Standard_LRS'

@description('Blob containers to create.')
param blobContainers array = [
  'raw-intake'
  'generated-assets'
  'provider-payloads'
  'voice-transcripts'
  'reports'
]

@description('Service Bus queues to create.')
param serviceBusQueues array = [
  'daily-content-jobs'
  'publishing-jobs'
  'reminder-jobs'
  'follow-up-jobs'
  'voice-jobs'
  'report-jobs'
]

module resourceGroupModule './modules/resource-group.bicep' = {
  name: 'resource-group-${appName}-${environmentName}'
  scope: subscription()
  params: {
    location: location
    resourceGroupName: resourceGroupName
    tags: union(tags, {
      environment: environmentName
      application: appName
    })
  }
}

module environmentModule './modules/environment.bicep' = {
  name: 'environment-${appName}-${environmentName}'
  scope: resourceGroup(resourceGroupName)
  params: {
    location: location
    environmentName: environmentName
    deploymentMode: deploymentMode
    appName: appName
    tags: union(tags, {
      environment: environmentName
      application: appName
    })
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    appServicePlanSku: productionAppServicePlanSku
    serviceBusSku: serviceBusSku
    sqlDatabaseSkuName: sqlDatabaseSkuName
    sqlDatabaseMaxSizeBytes: sqlDatabaseMaxSizeBytes
    storageSku: storageSku
    blobContainers: blobContainers
    serviceBusQueues: serviceBusQueues
  }
  dependsOn: [
    resourceGroupModule
  ]
}

output deployedResourceGroupName string = resourceGroupName
output apiAppName string = environmentModule.outputs.apiAppName
output workerFunctionAppName string = environmentModule.outputs.workerFunctionAppName
output keyVaultName string = environmentModule.outputs.keyVaultName
output storageAccountName string = environmentModule.outputs.storageAccountName
output sqlServerName string = environmentModule.outputs.sqlServerName
output sqlDatabaseName string = environmentModule.outputs.sqlDatabaseName
output serviceBusNamespaceName string = environmentModule.outputs.serviceBusNamespaceName
