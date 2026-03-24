param location string
param serviceBusNamespaceName string
param serviceBusSku string
param queueNames array
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

output namespaceName string = serviceBusNamespace.name
output fullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'
