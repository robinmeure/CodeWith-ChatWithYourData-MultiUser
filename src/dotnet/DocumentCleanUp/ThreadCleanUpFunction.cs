using System;
using Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentCleanUp
{
    public class ThreadCleanUpFunction
    {
        private readonly ILogger _logger;
        private IConfiguration _config;
        private IDocumentStore _documentStore;
        private IDocumentRegistry _documentRegistry;
        private ISearchService _searchService;
        private IThreadRepository _threadRepository;
        private static string _cosmosDbDatabaseName = string.Empty;
        private static string _cosmosDbContainerName = string.Empty;
        private static string _storageContainerName = string.Empty;

        public ThreadCleanUpFunction(ILoggerFactory loggerFactory,
        IConfiguration config,
        IDocumentStore documentStore,
        IDocumentRegistry documentRegistry,
        ISearchService searchService,
        IThreadRepository threadRepository)
        {
            _config = config;
            _documentStore = documentStore;
            _documentRegistry = documentRegistry;
            _searchService = searchService;

            _logger = loggerFactory.CreateLogger<ThreadCleanUpFunction>();
            _threadRepository = threadRepository;
        }

        [Function("ThreadCleanUp")]
        public void Run([TimerTrigger("%ThreadCleanupCron%")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Thread Cleanup function executed at: {DateTime.Now}");

            DateTime xDaysAgo = DateTime.Now.AddDays(7);
            //step 1: zoek alle threads in chathistory waarvan het laatste bericht ouder is dan x-dagen (x is een config item)
            var threads = _threadRepository.GetAllThreads(xDaysAgo);

            // get all the threadIds from the threads object
            var threadIds = new HashSet<string>(threads.Select(t => t.ThreadId)).ToList();
            foreach (string _threadId in threadIds)
            {
                // get the userId of the thread as well to be used in the delete function
                string userId = threads.Where(t => t.ThreadId == _threadId).Select(u => u.UserId).First();

                // step 2: soft delete alle documenten die bij deze thread horen (dit triggered DocCleanUp)
                // get all the docs for thread
                var docs = _documentRegistry.GetDocsPerThreadAsync(_threadId).GetAwaiter().GetResult();

                // set the documents in the documentRegistry to be soft deleted so that the DocumentCleanUpFunction can delete them
                if (_documentRegistry.RemoveDocumentFromThreadAsync(docs).GetAwaiter().GetResult())
                {
                    //step 3: hard-delete chat history
                    _threadRepository.DeleteThreadAsync(userId, _threadId).GetAwaiter().GetResult();
                }
            }


            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
