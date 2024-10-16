using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentCleanUp
{
    public class ThreadCleanUpFunction
    {
        private readonly ILogger _logger;

        public ThreadCleanUpFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ThreadCleanUpFunction>();
        }

        [Function("ThreadCleanUp")]
        public void Run([TimerTrigger("%ThreadCleanupCron%")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
