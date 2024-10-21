using Domain;
using Microsoft.Azure.Cosmos;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Infrastructure
{
    public class CosmosDocumentRegistry : IDocumentRegistry
    {
        
        //private CosmosClient _cosmosClient;
        private Container _container;
        private string _databaseName; //= "history";
        private string _containerName; //= "documentsperthread";

        public CosmosDocumentRegistry(Container cosmosDbContainer) 
        {
            _container = cosmosDbContainer;
        }

        public async Task<string> AddDocumentToThreadAsync(DocsPerThread docsPerThread)
        {
            //Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            //Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, "/userId"));
            try
            {
                var response = await _container.CreateItemAsync(docsPerThread, new PartitionKey(docsPerThread.UserId));
                if (response.StatusCode != System.Net.HttpStatusCode.Created)
                {
                    throw new Exception("Failed to add document to Document Registry");
                }
                return response.Resource.Id;
            }
            catch (CosmosException cosmosEx)
            {
                throw cosmosEx;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<string> UpdateDocumentAsync(DocsPerThread docsPerThread)
        {
            //Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            //Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(_containerName, "/userId"));

            var response = await _container.ReplaceItemAsync<DocsPerThread>(docsPerThread, docsPerThread.Id, new PartitionKey(docsPerThread.UserId));
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("Failed to change document");
            }
            return response.Resource.Id;
        }

        public async Task RemoveDocumentFromThreadAsync(DocsPerThread docsPerThread)
        {
            //Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            //Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(_containerName, "/userId"));
            try
            {
                var response = await _container.DeleteItemAsync<DocsPerThread>(docsPerThread.Id, new PartitionKey(docsPerThread.UserId));
                //if (response.StatusCode != System.Net.HttpStatusCode.OK)
                //{
                //    throw new Exception("Failed to delete document from Document Registry");
                //}
            }
            catch (CosmosException cosmosEx) 
            {
                throw cosmosEx;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return;

        }

        public async Task<List<DocsPerThread>> GetDocsPerThread(string threadId)
        {
            //Database _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            //Container _container = _database.GetContainer(_containerName);

            List<DocsPerThread> documents = new List<DocsPerThread>();
            string query = string.Format("SELECT * FROM c WHERE c.threadId = '{0}'", threadId);
            var queryDefinition = new QueryDefinition(query);
            var queryOptions = new QueryRequestOptions
            {
                MaxItemCount = 500
            };

            using (var iterator = _container.GetItemQueryIterator<DocsPerThread>(queryDefinition, requestOptions: queryOptions))
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        documents.Add(item);
                    }
                }
            }

            return documents;
        }
    }
}
