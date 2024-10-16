using System;
using System.Collections.Generic;
using Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentCleanUp;

public class DocumentCleanUpFunction
{
    private readonly ILogger _logger;
    private IConfiguration _config;
    

    public DocumentCleanUpFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<DocumentCleanUpFunction>();
        _config = config;
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

            }
            else
            {
                _logger.LogInformation($"Document {doc.DocumentName} is not marked for deletion.");
            }
        }
    }
}

