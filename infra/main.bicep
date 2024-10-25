@description('Name of the storage account.')
param storageAccountName string = 'sa${uniqueString(resourceGroup().id)}'

@description('Name of the AI Search instance.')
param aiSearchName string = 'aisearch-${uniqueString(resourceGroup().id)}'

@description('Name of the Azure OpenAI instance.')
param azureOpenAIName string = 'openai-${uniqueString(resourceGroup().id)}'

@description('Name of the blob storage container that contains the documents for the index.')
param blobStorageContainerName string = 'documents'

@description('Name of the embedding model to use.')
param embeddingModelName string = 'text-embedding-ada-002'

@description('Name/id of the embedding model to be used for the indexer during indexing.')
param indexerEmbeddingModelId string = 'text-embedding-ada-002-indexer'

@description('Name of completionmodel.')
param completionModelName string = 'gpt-4o'

@description('Name/id of the embedding model to be used for the indexer during querying.')
param integratedVectorEmbeddingModelId string = 'text-embedding-ada-002-aisearchquery'

@description('Name of the AI search index to be created or updated, must be lowercase.')
param indexName string = 'onyourdata'

@description('Name of the app service plan.')
param aspName string = 'asp-${uniqueString(resourceGroup().id)}'

@description('Name of the back end site.')
param backendAppName string = 'backend-${uniqueString(resourceGroup().id)}'

@description('Name of the front end site.')
param frontendAppName string = 'frontend-${uniqueString(resourceGroup().id)}'

@description('Name of the cosmos DB account.')
param cosmosAccountName string = 'cosmos-${uniqueString(resourceGroup().id)}'

var cosmosDatabaseName = 'history'
var cosmosDocumentContainerName = 'documentsperthread'
var cosmosHistoryContainerName = 'threadhistory'
var sqlRoleName = 'sql-contributor-${cosmosAccountName}'

module appServicePlan 'br/public:avm/res/web/serverfarm:0.3.0' = {
  name: 'appServicePlan'
  params: {
    name: aspName
    location: resourceGroup().location
    skuName: 'B1'
  }
}

module backendSite 'br/public:avm/res/web/site:0.9.0' = {
  name: 'backendSite'
  params: {
    kind: 'app'
    name: backendAppName
    serverFarmResourceId: appServicePlan.outputs.resourceId
    location: resourceGroup().location
    managedIdentities: {
      systemAssigned: true
    }
  }
}

module frontendSite 'br/public:avm/res/web/site:0.9.0' = {
  name: 'frontendSite'
  params: {
    kind: 'app'
    name: frontendAppName
    serverFarmResourceId: appServicePlan.outputs.resourceId
    location: resourceGroup().location
    siteConfig: {
      appSettings: [
      ]
    }
  }
}

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
    roleAssignments: [
      {
        principalId: backendSite.outputs.systemAssignedMIPrincipalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Search Index Data Contributor'
      }
    ]
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
            {
              principalId: backendSite.outputs.systemAssignedMIPrincipalId
              principalType: 'ServicePrincipal'
              roleDefinitionIdOrName: 'Storage Blob Data Contributor'
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
          name: embeddingModelName
          version: '2'
        }
        name: indexerEmbeddingModelId
      }
      {
        model: {
          format: 'OpenAI'
          name: embeddingModelName
          version: '2'
        }
        name: integratedVectorEmbeddingModelId
      }
      {
        model: {
          format: 'OpenAI'
          name: completionModelName
          version: '2024-05-13'
        }
        name: completionModelName
      }
    ]
    roleAssignmentPrincipalIds: [
      aiSearch.outputs.systemAssignedMIPrincipalId
      backendSite.outputs.systemAssignedMIPrincipalId
    ]
  }
}

module aiSearchIndex 'modules/aisearchindex/ai-search-index.bicep' = {
  name: 'aiSearchIndex'
  params: {
    indexName: indexName
    aiSearchName: aiSearch.outputs.name
    storageAccountContainerName: blobStorageContainerName
    storageAccountResourceId: storageAccount.outputs.resourceId
    embeddingModelName: embeddingModelName
    integratedVectorEmbeddingModelId: integratedVectorEmbeddingModelId
    indexerEmbeddingModelId: indexerEmbeddingModelId
    azureOpenAIEndpoint: openAi.outputs.endpoint
  }
}

module cosmosDB 'br/public:avm/res/document-db/database-account:0.8.0' = {
  name: 'cosmosDB'
  params: {
    name: cosmosAccountName
    location: resourceGroup().location
    sqlDatabases: [
      {
        name: cosmosDatabaseName
        containers: [
          {
            indexingPolicy: {
              automatic: true
            }
            name: cosmosDocumentContainerName
            paths: [
              '/userId'
            ]
          }
          {
            indexingPolicy: {
              automatic: true
            }
            name: cosmosHistoryContainerName
            paths: [
              '/userId'
            ]
          }
        ]
      }
    ]
    sqlRoleAssignmentsPrincipalIds: [
      backendSite.outputs.systemAssignedMIPrincipalId
    ]
    sqlRoleDefinitions: [
      {
        name: sqlRoleName
        dataAction: [
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/write'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/read'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/delete'
        ]
      }
    ]
  }
}

output aiSearchName string = aiSearch.outputs.name
output indexerName string = aiSearchIndex.outputs.indexerName
