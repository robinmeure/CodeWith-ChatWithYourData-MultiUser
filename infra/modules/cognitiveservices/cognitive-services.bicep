@description('Name of the cognitive services account.')
param name string

@description('Location of the cognitive services account.')
param location string = resourceGroup().location

@description('SKU of the account, should be an object with name property.')
param sku object = {
  name: 'S0'
}

@description('Models to deploy.')
param deployments array = []

@description('Managed identity to assign the role cognitive services user role to.')
param aiSearchManagedIdentity string

var roleDefinitionResourceId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var kind = 'OpenAI'

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  kind: kind
  sku: sku
  properties: {
    disableLocalAuth: false
    customSubDomainName: name
  }
}

@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = [for deployment in deployments: {
  parent: account
  name: deployment.name
  properties: {
    model: deployment.model
  }
  sku: deployment.?sku ?? {
    name: 'Standard'
    capacity: 20
  }
}]

resource cognitiveServicesUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: account
  name: roleDefinitionResourceId
}

resource roleAssignmentAISearch 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(aiSearchManagedIdentity, cognitiveServicesUserRoleDefinition.id, account.name)
  properties: {
    roleDefinitionId: cognitiveServicesUserRoleDefinition.id
    principalId: aiSearchManagedIdentity
    principalType: 'ServicePrincipal'
  }
}

output endpoint string = account.properties.endpoint
