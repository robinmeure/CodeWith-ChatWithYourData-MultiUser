using System;
using System.Collections.Generic;
using DocumentCleanUp.Helpers;
using Domain;
using Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentCleanUp;

public class DocumentCleanUpFunction
{
    private readonly ILogger _logger;
    private IConfiguration _config;
    private IDocumentStore _documentStore;
    private IDocumentRegistry _documentRegistry;
    private ISearchService _searchService;

    private readonly string _storageContainerName;

    // this doesn't work.. can only pass const strings to the Run methods of the function
    //private readonly string _cosmosDbDatabaseName;
    //private readonly string _cosmosDbContainerName;
    //private readonly string _cosmosDbLeaseContainerName;

    public DocumentCleanUpFunction(
        ILoggerFactory loggerFactory,
        IConfiguration config,
        IDocumentStore documentStore,
        IDocumentRegistry documentRegistry,
        ISearchService searchService)
    {
        _logger = loggerFactory.CreateLogger<DocumentCleanUpFunction>();
        _config = config;
        _documentStore = documentStore;
        _documentRegistry = documentRegistry;
        _searchService = searchService;

        // this doesn't work.. can only pass const strings to the Run methods of the function
        //_cosmosDbContainerName = _config["CosmosDb:ContainerName"] ?? throw new ArgumentNullException("CosmosDb:ContainerName");
        //_cosmosDbDatabaseName = _config["CosmosDb:DatabaseName"] ?? throw new ArgumentNullException("CosmosDb:DatabaseName");
        //_cosmosDbLeaseContainerName = _config["CosmosDb:LeaseContainerName"] ?? throw new ArgumentNullException("CosmosDb:LeaseContainerName");
        
        _storageContainerName = _config["Storage:ContainerName"] ?? throw new ArgumentNullException("Storage:ContainerName");
    }

    [Function("DocumentCleanUp")]
    public async Task Run([CosmosDBTrigger(
        databaseName: Constants.COSMOS_DOCUMENTS_DATABASE_NAME,
        containerName: Constants.COSMOS_DOCUMENTS_CONTAINER_NAME,
        Connection = "CosmosDbConnection",
        LeaseContainerName =Constants.COSMOS_DOCUMENTS_LEASE_CONTAINER_NAME,
        CreateLeaseContainerIfNotExists = false)] IReadOnlyList<DocsPerThread> input)
    {
        _logger.LogInformation($"CosmosDbTrigger found {input.Count} documents which are soft-deleted.");

        for (int i = 0; i < input.Count; i++)
        {
            var doc = input[i];
            if (doc.Deleted)
            {
                _logger.LogInformation($"Document {doc.DocumentName} is marked for deletion. Deleting...");

                // Fetching data for the specified document (getting the chunk_id so we can delete it from the index later on)
                doc = await _searchService.IsChunkingComplete(doc);

                _logger.LogInformation($"BlobStore : trying to delete {doc.DocumentName}.");
                // Delete the document from storage
                if (await _documentStore.DeleteDocumentAsync(doc.Id, _storageContainerName))
                {
                    _logger.LogInformation($"CosmosDb : trying to delete {doc.DocumentName}.");

                    // Delete the document from the Cosmos DB container
                    if (await _documentRegistry.DeleteDocumentAsync(doc))
                    {
                        _logger.LogInformation($"SearchIndex : trying to delete {doc.DocumentName}.");

                        // Delete the document from the search index
                        await _searchService.DeleteDocumentAsync(doc);
                    }
                }
            }
            else
            {
                _logger.LogInformation($"Document {doc.DocumentName} is not marked for deletion.");
            }
        }
    }
}

