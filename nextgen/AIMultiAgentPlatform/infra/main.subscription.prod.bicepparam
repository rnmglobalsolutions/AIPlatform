using './main.subscription.bicep'

param location = 'eastus2'
param environmentName = 'prod'
param deploymentMode = 'production'
param appName = 'aimap'
param resourceGroupName = 'rg-aimap-prod'
param tags = {
  owner: 'RNM'
  workload: 'AI-Multi-Agent-Platform'
  environment: 'prod'
}
param sqlAdminLogin = 'aimapsqladmin'
param sqlAdminPassword = '<replace-with-secure-value>'
param productionAppServicePlanSku = {
  name: 'P1v3'
  tier: 'PremiumV3'
  size: 'P1v3'
  capacity: 2
}
param serviceBusSku = 'Standard'
param sqlDatabaseSkuName = 'S1'
param sqlDatabaseMaxSizeBytes = 268435456000
param storageSku = 'Standard_GRS'
param enableOperationalAlerts = true
param alertEmailReceivers = [
  'alerts-platform@rnmglobalsolutions.com'
]
