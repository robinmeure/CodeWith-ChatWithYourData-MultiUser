@description('Name of the Azure Search instance.')
param aiSearchName string

@description('Name of the AI search index to be created or updated, must be lowercase.')
param indexName string = 'onyourdata'

@description('Name of the storage account container that contains the documents for the indexer.')
param storageAccountContainerName string

@description('Resource ID of the storage account, used for setting the credentials for the data source.')
param storageAccountResourceId string

@description('Name of the embedding model.')
param embeddingModelName string

@description('Id of the embedding model to be used for the indexer.')
param indexerEmbeddingModelId string

@description('Name of the embedding model to be used for the indexer during querying.')
param integratedVectorEmbeddingModelId string = 'text-embedding-ada-002'

@description('URI of the Azure OpenAI resource.')
param azureOpenAIEndpoint string

@description('Datasource definition as base64 encoded json.')
param dataSource string = loadFileAsBase64('./definitions/datasource.json')

@description('Index definition as base64 encoded json.')
param index string = loadFileAsBase64('./definitions/index.json')

@description('Skillset definition as base64 encoded json.')
param skillset string = loadFileAsBase64('./definitions/skillset.json')

@description('Indexer definition as base64 encoded json.')
param indexer string = loadFileAsBase64('./definitions/indexer.json')

var aiSearchIndexDeploymentScriptName = 'aiSearchIndexDeploymentScript-${uniqueString(resourceGroup().id)}'
var scriptRoleDefinitionId = '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
var scriptIdentityName = 'scriptIdentity-${uniqueString(resourceGroup().id)}'

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' existing = {
  name: aiSearchName
}

resource scriptRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: aiSearch
  name: scriptRoleDefinitionId
}

module scriptIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.4.0' = {
  name: 'scriptIdentity'
  params: {
    name: scriptIdentityName
    location: resourceGroup().location
  }
}

resource roleAssignmentDeploymentScript 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: resourceGroup()
  name: guid(scriptRoleDefinition.id, aiSearch.name)
  properties: {
    roleDefinitionId: scriptRoleDefinition.id
    principalId: scriptIdentity.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

module aiSearchIndexDeploymentScript 'br/public:avm/res/resources/deployment-script:0.4.0' = {
  name: 'aiSearchIndexDeploymentScript'
  dependsOn: [
    roleAssignmentDeploymentScript
  ]
  params: {
    kind: 'AzurePowerShell'
    name: aiSearchIndexDeploymentScriptName
    azPowerShellVersion: '9.7'
    location: resourceGroup().location
    managedIdentities: {
      userAssignedResourcesIds: [
        scriptIdentity.outputs.resourceId
      ]
    }
    cleanupPreference: 'OnExpiration'
    retentionInterval: 'PT1H'
    scriptContent: loadTextContent('./scripts/setupindex.ps1')
    arguments: '-index \\"${index}\\" -indexer \\"${indexer}\\" -datasource \\"${dataSource}\\" -skillset \\"${skillset}\\" -searchServiceName \\"${aiSearchName}\\" -dataSourceContainerName \\"${storageAccountContainerName}\\" -dataSourceConnectionString \\"ResourceId=${storageAccountResourceId};\\" -indexName \\"${indexName}\\" -AzureOpenAIResourceUri \\"${azureOpenAIEndpoint}\\" -indexerEmbeddingModelId \\"${indexerEmbeddingModelId}\\" -embeddingModelName \\"${embeddingModelName}\\" -searchEmbeddingModelId \\"${integratedVectorEmbeddingModelId}\\"'
  }
}

output indexName string = indexName
output indexerName string = '${indexName}-indexer'
output datasourceName string = '${indexName}-datasource'
output skillsetName string = '${indexName}-skillset'
