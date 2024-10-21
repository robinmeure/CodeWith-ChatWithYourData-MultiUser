using System;
using System.Collections.Generic;
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


    public DocumentCleanUpFunction(ILoggerFactory loggerFactory, IConfiguration config, IDocumentStore documentStore)
    {
        _logger = loggerFactory.CreateLogger<DocumentCleanUpFunction>();
        _config = config;
        _documentStore = documentStore;
    }

    [Function("DocumentCleanUp")]
    public void Run([CosmosDBTrigger(
        databaseName: "%CosmosDBDatabase%",
        containerName: "%CosmosDBContainer%",
        Connection = "CosmosDbConnection",
        LeaseContainerName = "%CosmosDbLeaseContainer%",
        CreateLeaseContainerIfNotExists = true)] IReadOnlyList<DocsPerThread> input)
    {
        
        for (int i = 0; i < input.Count; i++)
        {
            var doc = input[i];
            if (doc.Deleted)
            {
                _logger.LogInformation($"Document {doc.DocumentName} is marked for deletion. Deleting...");
                // Delete the document from storage
                _documentStore.DeleteDocumentAsync(doc.DocumentName, _config["StorageContainerName"]);

                // Delete the document from the Cosmos DB container
                continue;

            }
            else
            {
                _logger.LogInformation($"Document {doc.DocumentName} is not marked for deletion.");
            }
        }
    }
}

