using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Domain;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Infrastructure
{
    public class CosmosDocumentRegistry : IDocumentRegistry
    {
        private CosmosClient _cosmosClient;
        private readonly Container _container;

        //private readonly string _databaseName = "history";
        //private readonly string _containerName = "documentsperthread";

        public CosmosDocumentRegistry(CosmosClient client, string databaseName, string containerName) 
        {
            _cosmosClient = client;
            _container = _cosmosClient.GetContainer(databaseName, containerName);

        }

        public async Task<string> AddDocumentToThreadAsync(DocsPerThread docsPerThread)
        {
           // Database _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
           // Container _container = await _database.CreateContainerIfNotExistsAsync(new ContainerProperties(_containerName, "/userId"));

            var response = await _container.CreateItemAsync(docsPerThread, new PartitionKey(docsPerThread.UserId));
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception("Failed to add document to Document Store");
            }
            return response.Resource.Id;
        }

        public async Task<List<DocsPerThread>> GetDocsPerThreadAsync(string threadId)
        {
          //  Database _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
          //  Container _container = _database.GetContainer(_containerName);

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

        public async Task<bool> RemoveDocumentFromThreadAsync(List<DocsPerThread> documents)
        {
          //  Database _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
          //  Container _container = _database.GetContainer(_containerName);
            
            foreach (var document in documents)
            { 
                await _container.UpsertItemAsync(document);
            }

            return true;
        }

        public async Task<bool> RemoveDocumentAsync(DocsPerThread document)
        {
          //  Database _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
          //  Container _container = _database.GetContainer(_containerName);

            var updatedDocument = await _container.UpsertItemAsync(document);
            if (updatedDocument != null)
                return true;
            return false;
        }
    }
}
