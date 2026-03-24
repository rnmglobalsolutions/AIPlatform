targetScope = 'subscription'

param location string
param resourceGroupName string
param tags object = {}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

output name string = resourceGroup.name
