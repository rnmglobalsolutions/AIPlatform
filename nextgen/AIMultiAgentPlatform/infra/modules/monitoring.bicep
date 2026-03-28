param location string
param logAnalyticsWorkspaceName string
param applicationInsightsName string
param enableOperationalAlerts bool = false
param actionGroupShortName string = 'AIMAPOps'
param alertEmailReceivers array = []
param tags object = {}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (enableOperationalAlerts && length(alertEmailReceivers) > 0) {
  name: 'ag-${applicationInsightsName}'
  location: 'global'
  tags: tags
  properties: {
    enabled: true
    groupShortName: take(actionGroupShortName, 12)
    emailReceivers: [
      for (receiver, index) in alertEmailReceivers: {
        name: 'email-${index}'
        emailAddress: receiver
        useCommonAlertSchema: true
      }
    ]
  }
}

output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output applicationInsightsResourceId string = applicationInsights.id
output actionGroupResourceId string = enableOperationalAlerts && length(alertEmailReceivers) > 0 ? actionGroup.id : ''
