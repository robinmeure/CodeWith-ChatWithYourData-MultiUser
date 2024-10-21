using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Reflection.Metadata;
using System.Xml.Linq;

namespace DocApi.Controllers
{
    [Route("threads")]
    [ApiController]
    public class ThreadController : ControllerBase
    {
        private readonly IThreadRegistry _threadRegistry;
        private readonly ILogger<ThreadController> _logger;
        private readonly IConfiguration _configuration;

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRegistry cosmosThreadRegistry,
            IConfiguration configuration
            )
        {
            _threadRegistry = cosmosThreadRegistry;
            _configuration = configuration;
            _logger = logger;

        }

        [HttpGet("")]
        public async Task<List<Domain.Thread>> GetThreads([FromQuery] string userId)
        {
            _logger.LogInformation("Fetching threads from CosmosDb for userId : {0}", userId);
            
            List<Domain.Thread> threads = await _threadRegistry.GetThreadsAsync(userId);

            _logger.LogInformation("Fetched threads from CosmosDb for userId : {0}", userId);
            return threads;
        }

        [HttpPost("")]
        public async Task<Domain.Thread> CreateThread([FromQuery] string userId)
        {
            _logger.LogInformation("Creating thread in CosmosDb for userId : {0}", userId);

            Domain.Thread thread = await _threadRegistry.CreateThreadAsync(userId);

            _logger.LogInformation("Created thread in CosmosDb for userId : {0}", userId);

            return thread;
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId)
        {
            _logger.LogInformation("Deleting thread in CosmosDb for threadId : {0}", threadId);

            bool result = await _threadRegistry.DeleteThreadAsync(threadId);

            if (result)
            {
                return Ok();
            } 

            return BadRequest();
           
        }

        [HttpGet("{threadId}/messages")]
        public async Task<List<ThreadMessage>> Get([FromRoute] string threadId)
        {
            _logger.LogInformation("Fetching thread messages from CosmosDb for threadId : {0}", threadId);
            List<ThreadMessage> result = await _threadRegistry.GetMessagesAsync(threadId);
            return result;
        }

        [HttpPost("{threadId}/messages")]
        public async Task<IActionResult> Post([FromRoute] string threadId, [FromQuery] string userId)
        {
            _logger.LogInformation("Adding thread message to CosmosDb for threadId : {0}", threadId);
            await _threadRegistry.PostMessageAsync(userId, threadId, "hello world");
            return Ok();
        }
    }
}
