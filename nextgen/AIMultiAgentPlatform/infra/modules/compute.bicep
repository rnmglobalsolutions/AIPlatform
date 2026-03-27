param location string
param appServicePlanName string
param appServicePlanSku object
param apiAppName string
param workerFunctionAppName string
param managedIdentityResourceId string
param storageAccountName string
param appInsightsConnectionString string
param keyVaultUri string
param platformMode string
param persistenceMode string
param messagingMode string
param hostingMode string
param targetPersistenceMode string
param targetMessagingMode string
param targetHostingMode string
param sqlConnectionStringPlaceholder string
param serviceBusFullyQualifiedNamespace string
param tags object = {}

var isLean = toLower(platformMode) == 'lean'
var deploymentStorageContainerName = 'function-releases'
var tallyWebhookSigningSecretName = 'tally-webhook-signing-secret'
var tallyWebhookSigningSecretReference = '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${tallyWebhookSigningSecretName}/)'
var storageBlobEndpoint = storageAccount.properties.primaryEndpoints.blob
var deploymentStorageContainerUrl = '${storageBlobEndpoint}${deploymentStorageContainerName}'
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  scope: resourceGroup()
  name: last(split(managedIdentityResourceId, '/'))
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = if (isLean) {
  parent: blobService
  name: deploymentStorageContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource apiAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = if (!isLean) {
  name: appServicePlanName
  location: location
  kind: 'linux'
  tags: tags
  sku: {
    name: appServicePlanSku.name
    tier: appServicePlanSku.tier
    size: appServicePlanSku.size
    capacity: appServicePlanSku.capacity
  }
  properties: {
    reserved: true
  }
}

resource workerFlexConsumptionPlan 'Microsoft.Web/serverfarms@2023-12-01' = if (isLean) {
  name: '${appServicePlanName}-func-flex'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
    size: 'FC1'
    family: 'FC'
    capacity: 0
  }
  properties: {
    reserved: true
  }
}

resource apiWebApp 'Microsoft.Web/sites@2023-12-01' = if (!isLean) {
  name: apiAppName
  location: location
  kind: 'app,linux'
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: apiAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: managedIdentity.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'PlatformMode'
          value: platformMode
        }
        {
          name: 'Infrastructure__PersistenceMode'
          value: persistenceMode
        }
        {
          name: 'Infrastructure__MessagingMode'
          value: messagingMode
        }
        {
          name: 'Infrastructure__HostingMode'
          value: hostingMode
        }
        {
          name: 'Infrastructure__TargetPersistenceMode'
          value: targetPersistenceMode
        }
        {
          name: 'Infrastructure__TargetMessagingMode'
          value: targetMessagingMode
        }
        {
          name: 'Infrastructure__TargetHostingMode'
          value: targetHostingMode
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        {
          name: 'TallyWebhook__SigningSecret'
          value: tallyWebhookSigningSecretReference
        }
        {
          name: 'Storage__BlobServiceUri'
          value: storageBlobEndpoint
        }
        {
          name: 'Sql__ConnectionString'
          value: sqlConnectionStringPlaceholder
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'PublicEndpoints__FunctionsBaseUrl'
          value: 'https://${workerFunctionAppName}.azurewebsites.net'
        }
        {
          name: 'PublicEndpoints__HeyGenWebhookPath'
          value: 'api/integrations/heygen/webhook'
        }
        {
          name: 'OpenAI__ApiKeySecretName'
          value: 'openai-api-key'
        }
        {
          name: 'ManyChat__ApiKeySecretName'
          value: 'manychat-api-key'
        }
        {
          name: 'Calendly__ApiKeySecretName'
          value: 'calendly-api-key'
        }
        {
          name: 'HeyGen__ApiKeySecretName'
          value: 'heygen-api-key'
        }
        {
          name: 'ElevenLabs__ApiKeySecretName'
          value: 'elevenlabs-api-key'
        }
      ]
    }
  }
}

resource leanFunctionApp 'Microsoft.Web/sites@2023-12-01' = if (isLean) {
  name: workerFunctionAppName
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: workerFlexConsumptionPlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: managedIdentity.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: deploymentStorageContainerUrl
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: 2048
        maximumInstanceCount: 50
        triggers: {
          http: {
            perInstanceConcurrency: 16
          }
        }
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'PlatformMode'
          value: platformMode
        }
        {
          name: 'Infrastructure__PersistenceMode'
          value: persistenceMode
        }
        {
          name: 'Infrastructure__MessagingMode'
          value: messagingMode
        }
        {
          name: 'Infrastructure__HostingMode'
          value: hostingMode
        }
        {
          name: 'Infrastructure__TargetPersistenceMode'
          value: targetPersistenceMode
        }
        {
          name: 'Infrastructure__TargetMessagingMode'
          value: targetMessagingMode
        }
        {
          name: 'Infrastructure__TargetHostingMode'
          value: targetHostingMode
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
          value: storageConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        {
          name: 'TallyWebhook__SigningSecret'
          value: tallyWebhookSigningSecretReference
        }
        {
          name: 'Storage__BlobServiceUri'
          value: storageBlobEndpoint
        }
        {
          name: 'Sql__ConnectionString'
          value: sqlConnectionStringPlaceholder
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'PublicEndpoints__FunctionsBaseUrl'
          value: 'https://${workerFunctionAppName}.azurewebsites.net'
        }
        {
          name: 'PublicEndpoints__HeyGenWebhookPath'
          value: 'api/integrations/heygen/webhook'
        }
      ]
    }
  }
  dependsOn: [
    deploymentContainer
  ]
}

resource productionFunctionApp 'Microsoft.Web/sites@2023-12-01' = if (!isLean) {
  name: workerFunctionAppName
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: apiAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: managedIdentity.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      appSettings: [
        {
          name: 'PlatformMode'
          value: platformMode
        }
        {
          name: 'Infrastructure__PersistenceMode'
          value: persistenceMode
        }
        {
          name: 'Infrastructure__MessagingMode'
          value: messagingMode
        }
        {
          name: 'Infrastructure__HostingMode'
          value: hostingMode
        }
        {
          name: 'Infrastructure__TargetPersistenceMode'
          value: targetPersistenceMode
        }
        {
          name: 'Infrastructure__TargetMessagingMode'
          value: targetMessagingMode
        }
        {
          name: 'Infrastructure__TargetHostingMode'
          value: targetHostingMode
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
          value: storageConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        {
          name: 'TallyWebhook__SigningSecret'
          value: tallyWebhookSigningSecretReference
        }
        {
          name: 'Storage__BlobServiceUri'
          value: storageBlobEndpoint
        }
        {
          name: 'Sql__ConnectionString'
          value: sqlConnectionStringPlaceholder
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'PublicEndpoints__FunctionsBaseUrl'
          value: 'https://${workerFunctionAppName}.azurewebsites.net'
        }
        {
          name: 'PublicEndpoints__HeyGenWebhookPath'
          value: 'api/integrations/heygen/webhook'
        }
      ]
    }
  }
}

output apiAppName string = isLean ? '' : apiWebApp.name
output workerFunctionAppName string = isLean ? leanFunctionApp.name : productionFunctionApp.name
