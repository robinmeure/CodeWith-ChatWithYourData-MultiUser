using System;
using System.Collections.Generic;
using DocumentCleanUp.Helpers;
using Domain;
using Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Thread = Domain.Thread;

namespace DocumentCleanUp;

public class ThreadCleanUpFunctionFromCosmos
{
    private readonly ILogger _logger;
    private IConfiguration _config;
    private ThreadCleanup _threadCleanup;
    private CosmosClient _cosmosClient;

    public ThreadCleanUpFunctionFromCosmos(
        ILoggerFactory loggerFactory,
        IConfiguration config,
        CosmosClient cosmosClient,
        ThreadCleanup threadCleanup
        )
    {
        _cosmosClient = cosmosClient;
        _threadCleanup = threadCleanup;
        _logger = loggerFactory.CreateLogger<DocumentCleanUpFunction>();
        _config = config;
    }

    [Function("ThreadCleanUpFromCosmos")]
    public async Task Run([CosmosDBTrigger(
        databaseName: Constants.COSMOS_THREADS_DATABASE_NAME,
        containerName: Constants.COSMOS_THREADS_CONTAINER_NAME,
        Connection = "CosmosDbConnection", // this points to a local secret.. not sure if this is the right way to do it
        LeaseContainerName = Constants.COSMOS_THREAD_LEASE_CONTAINER_NAME,
        CreateLeaseContainerIfNotExists = false)] IReadOnlyList<Thread> threads)
    {
        _logger.LogInformation($"CosmosDbTrigger found {threads.Count} threads which are soft-deleted.");

        threads.Where(t => t.Deleted).ToList().ForEach(async t =>
        {
            _logger.LogInformation($"Thread {t.ThreadName} is marked for deletion. Deleting...");
            await _threadCleanup.Cleanup(t.Id);
        });
    }
}

