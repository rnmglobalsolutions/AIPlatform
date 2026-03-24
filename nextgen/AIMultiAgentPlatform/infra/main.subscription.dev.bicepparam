using './main.subscription.bicep'

param location = 'eastus'
param environmentName = 'dev'
param deploymentMode = 'lean'
param appName = 'aimap'
param resourceGroupName = 'rg-aimap-dev'
param tags = {
  owner: 'RNM'
  workload: 'AI-Multi-Agent-Platform'
  environment: 'dev'
}
param serviceBusSku = 'Standard'
param sqlDatabaseSkuName = 'Basic'
param sqlDatabaseMaxSizeBytes = 2147483648
param storageSku = 'Standard_LRS'
