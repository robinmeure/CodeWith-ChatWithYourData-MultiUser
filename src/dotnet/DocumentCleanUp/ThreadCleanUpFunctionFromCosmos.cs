using System;
using System.Collections.Generic;
using DocumentCleanUp.Helpers;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Thread = Domain.Thread;

namespace DocumentCleanUp;

public class ThreadCleanUpFunctionFromCosmos
{
    private readonly ILogger _logger;
    private ThreadCleanup _threadCleanup;

    public ThreadCleanUpFunctionFromCosmos(
        ILoggerFactory loggerFactory,
        ThreadCleanup threadCleanup
        )
    {
        _threadCleanup = threadCleanup;
        _logger = loggerFactory.CreateLogger<ThreadCleanUpFunctionFromCosmos>();
    }

    [Function("ThreadCleanUpFromCosmos")]
    public async Task<IActionResult> Run([CosmosDBTrigger(
        databaseName: "%CosmosDBDatabase%",
        containerName: "%CosmosDbThreadContainer%",
        Connection = "CosmosDbConnection",
        LeaseContainerName ="%CosmosDbThreadLease%",
        FeedPollDelay = 5000, // this to ensure that when deleting messages from a thread the trigger is not triggered again
        CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Thread> threads)
    {
        _logger.LogInformation($"CosmosDbTrigger found {threads.Count} threads which are soft-deleted.");

        threads.Where(t => t.Deleted).ToList().ForEach(async t =>
        {
            _logger.LogInformation($"Thread {t.ThreadName} is marked for deletion. Deleting...");
            await _threadCleanup.Cleanup(t.Id);
        }); 

        return new OkResult();
    }
}

