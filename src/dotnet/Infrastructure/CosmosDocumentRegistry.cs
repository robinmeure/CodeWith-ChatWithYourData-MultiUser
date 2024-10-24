using Domain;
using Microsoft.Azure.Cosmos;
using System.Reflection.Metadata;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Infrastructure
{
    public class CosmosDocumentRegistry : IDocumentRegistry
    {

        //private CosmosClient _cosmosClient;
        private Container _container;

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

        //public async Task RemoveDocumentFromThreadAsync(List<DocsPerThread> documents)
        //{
        //    bool isSuccess = false;
        //    try
        //    {
        //        foreach (var document in documents)
        //        {
        //            await _container.UpsertItemAsync(document);
        //        }
        //        isSuccess = true;
        //        // is this updating the flag to deleted or not?
        //        // var response = await _container.DeleteItemAsync<DocsPerThread>(docsPerThread.Id, new PartitionKey(docsPerThread.UserId));

        //        //if (response.StatusCode != System.Net.HttpStatusCode.OK)
        //        //{
        //        //    throw new Exception("Failed to delete document from Document Registry");
        //        //}
        //    }
        //    catch (CosmosException cosmosEx)
        //    {
        //        throw cosmosEx;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }

        //    return isSuccess;

        //}

        public async Task<bool> RemoveDocumentAsync(DocsPerThread document)
        {
            var fieldsToUpdate = new Dictionary<string, object>
            {
                { "Deleted", true },
            };
            try
            {
                bool isUpdated = await UpdateDocumentFieldsAsync(document.Id, document.UserId, fieldsToUpdate);
                if (!isUpdated)
                {
                    return false;
                }
            }
            catch (CosmosException cosmosEx)
            {
                throw cosmosEx;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return true;
        }

        public async Task<bool> RemoveDocumentFromThreadAsync(List<DocsPerThread> documents)
        {
            var fieldsToUpdate = new Dictionary<string, object>
            {
                { "Deleted", true },
            };

            foreach(var document in documents)
            {
                bool isUpdated = await UpdateDocumentFieldsAsync(document.Id, document.UserId, fieldsToUpdate);
                if (!isUpdated)
                {
                    return false;
                }
            }
           
            return true;
        }

        public async Task<bool> UpdateDocumentFieldsAsync(string documentId, string userId, Dictionary<string, object> fieldsToUpdate)
        {
            var patchOperations = new List<PatchOperation>();

            foreach (var field in fieldsToUpdate)
            {
                patchOperations.Add(PatchOperation.Set($"/{field.Key}", field.Value));
            }

            try
            {
                var response = await _container.PatchItemAsync<DocsPerThread>(documentId, new PartitionKey(userId), patchOperations);
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (CosmosException ex)
            {
                // Handle exception
                throw new Exception($"Failed to update document: {ex.Message}", ex);
            }
        }

        public async Task<List<DocsPerThread>> GetDocsPerThreadAsync(string threadId)
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