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

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  scope: resourceGroup()
  name: last(split(managedIdentityResourceId, '/'))
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
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

resource apiWebApp 'Microsoft.Web/sites@2023-12-01' = {
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
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
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
          name: 'Storage__BlobServiceUri'
          value: 'https://${storageAccountName}.blob.core.windows.net/'
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

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
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
    serverFarmId: appServicePlan.id
    httpsOnly: true
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
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
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
          name: 'Storage__BlobServiceUri'
          value: 'https://${storageAccountName}.blob.core.windows.net/'
        }
        {
          name: 'Sql__ConnectionString'
          value: sqlConnectionStringPlaceholder
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
      ]
    }
  }
}

output apiAppName string = apiWebApp.name
output workerFunctionAppName string = functionApp.name
