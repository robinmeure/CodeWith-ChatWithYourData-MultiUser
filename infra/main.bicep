@description('Name of the storage account.')
param storageAccountName string = 'sa${uniqueString(resourceGroup().id)}'

@description('Name of the AI Search instance.')
param aiSearchName string = 'aisearch-${uniqueString(resourceGroup().id)}'

@description('Name of the Azure OpenAI instance.')
param azureOpenAIName string = 'openai-${uniqueString(resourceGroup().id)}'

@description('Name of the blob storage container that contains the documents for the index.')
param blobStorageContainerName string = 'documents'

@description('Id of the embedding model to be used for the indexer.')
param embeddingModelId string = 'text-embedding-ada-002'

@description('Name of the embedding model to be used for the indexer.')
param embeddingModelName string = 'text-embedding-ada-002'

@description('Name of the AI search index to be created or updated, must be lowercase.')
param indexName string = 'onyourdata'

module aiSearch 'br/public:avm/res/search/search-service:0.7.1' = {
  name: 'aiSearch'
  params: {
    name: aiSearchName
    sku: 'basic'
    location: resourceGroup().location
    managedIdentities: {
      systemAssigned: true
    }
    replicaCount: 1
    partitionCount: 1
  }
}

module storageAccount 'br/public:avm/res/storage/storage-account:0.9.1' = {
  name: 'storageAccount'
  params: {
    name: storageAccountName
    kind: 'BlobStorage'
    location: resourceGroup().location
    skuName: 'Standard_GRS'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    blobServices: {
      containers: [
        {
          name: blobStorageContainerName
          roleAssignments: [
            {
              principalId: aiSearch.outputs.systemAssignedMIPrincipalId
              principalType: 'ServicePrincipal'
              roleDefinitionIdOrName: 'Storage Blob Data Reader'
            }
          ]
        }
      ]
    }
  }
}

module openAi './modules/cognitiveservices/cognitive-services.bicep' = {
  name: 'openai'
  params: {
    name: azureOpenAIName
    deployments: [
      {
        model: {
          format: 'OpenAI'
          name: embeddingModelId
          version: '2'
        }
        name: embeddingModelName
      }
    ]
    aiSearchManagedIdentity: aiSearch.outputs.systemAssignedMIPrincipalId
  }
}

module aiSearchIndex 'modules/aisearchindex/ai-search-index.bicep' = {
  name: 'aiSearchIndex'
  params: {
    indexName: indexName
    aiSearchName: aiSearch.outputs.name
    storageAccountContainerName: blobStorageContainerName
    storageAccountResourceId: storageAccount.outputs.resourceId
    embeddingModelId: embeddingModelId
    embeddingModelName: embeddingModelName
    azureOpenAIEndpoint: openAi.outputs.endpoint
  }
}

output aiSearchName string = aiSearch.outputs.name
output indexerName string = aiSearchIndex.outputs.indexerName
