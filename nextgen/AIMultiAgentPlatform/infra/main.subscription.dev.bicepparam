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
param appServicePlanSku = {
  name: 'B1'
  tier: 'Basic'
  size: 'B1'
  capacity: 1
}
param serviceBusSku = 'Standard'
param sqlDatabaseSkuName = 'Basic'
param sqlDatabaseMaxSizeBytes = 2147483648
param storageSku = 'Standard_LRS'
