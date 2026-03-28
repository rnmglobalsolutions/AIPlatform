param location string
param serviceBusNamespaceName string
param serviceBusSku string
param queueNames array
param actionGroupResourceId string = ''
param activeMessagesAlertThreshold int = 250
param deadLetterMessagesAlertThreshold int = 1
param tags object = {}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: serviceBusSku
    tier: serviceBusSku
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    minimumTlsVersion: '1.2'
  }
}

resource queues 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = [for queueName in queueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    requiresDuplicateDetection: false
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: serviceBusSku == 'Standard'
  }
}]

resource activeMessagesAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(actionGroupResourceId)) {
  name: 'alert-${serviceBusNamespaceName}-active-messages'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alerts when Service Bus active message backlog grows above the configured threshold.'
    severity: 2
    enabled: true
    scopes: [
      serviceBusNamespace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    targetResourceType: 'Microsoft.ServiceBus/namespaces'
    targetResourceRegion: location
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ActiveMessagesThreshold'
          metricNamespace: 'Microsoft.ServiceBus/namespaces'
          metricName: 'ActiveMessages'
          operator: 'GreaterThan'
          threshold: activeMessagesAlertThreshold
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroupResourceId
      }
    ]
  }
}

resource deadLetterMessagesAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(actionGroupResourceId)) {
  name: 'alert-${serviceBusNamespaceName}-deadletter'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alerts when dead-lettered messages appear in the Service Bus namespace.'
    severity: 1
    enabled: true
    scopes: [
      serviceBusNamespace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    targetResourceType: 'Microsoft.ServiceBus/namespaces'
    targetResourceRegion: location
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'DeadLetterMessagesThreshold'
          metricNamespace: 'Microsoft.ServiceBus/namespaces'
          metricName: 'DeadletteredMessages'
          operator: 'GreaterThan'
          threshold: deadLetterMessagesAlertThreshold
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroupResourceId
      }
    ]
  }
}

output namespaceName string = serviceBusNamespace.name
output fullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'
