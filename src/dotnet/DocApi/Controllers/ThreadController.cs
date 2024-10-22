using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Reflection.Metadata;
using System.Xml.Linq;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace DocApi.Controllers
{
    [Route("threads")]
    [ApiController]
    public class ThreadController : ControllerBase
    {
        private readonly IThreadRegistry _threadRegistry;
        private readonly ILogger<ThreadController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IChatCompletionService _completionService;
        private readonly Kernel _kernel;

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRegistry cosmosThreadRegistry,
            IConfiguration configuration,
            IChatCompletionService completionService,
            Kernel kernel
            )
        {
            _threadRegistry = cosmosThreadRegistry;
            _configuration = configuration;
            _logger = logger;
            _completionService = completionService;
            _kernel = kernel;
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

            if(thread == null)
            {
                _logger.LogInformation("Failed to create thread in CosmosDb for userId : {0}", userId);
            }

            _logger.LogInformation("Created thread in CosmosDb for userId : {0}", userId);
            
            await _threadRegistry.PostMessageAsync(userId, thread.Id, "You are a helpful assistant that helps people find information.", "system");

            return thread;
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId, [FromQuery] string userId)
        {
            _logger.LogInformation("Deleting thread in CosmosDb for threadId : {0}", threadId);

            bool result = await _threadRegistry.DeleteThreadAsync(userId, threadId);

            if (result)
            {
                return Ok();
            } 

            return BadRequest();
           
        }

        [HttpGet("{threadId}/messages")]
        public async Task<List<ThreadMessage>> Get([FromRoute] string threadId, [FromQuery] string userId)
        {
            _logger.LogInformation("Fetching thread messages from CosmosDb for threadId : {0}", threadId);
            List<ThreadMessage> result = await _threadRegistry.GetMessagesAsync(userId, threadId);
            return result;
        }

        [HttpPost("{threadId}/messages")]
        public async Task<IActionResult> Post([FromRoute] string threadId, [FromQuery] string userId)
        {
            _logger.LogInformation("Adding thread message to CosmosDb for threadId : {0}", threadId);
            List<ThreadMessage> messages = await _threadRegistry.GetMessagesAsync(userId, threadId);

            // Build up history.
            ChatHistory history = [];
            foreach (ThreadMessage message in messages)
            {
                if(message.Role == "user")
                {
                    history.AddUserMessage(message.Content);
                }
                else if(message.Role == "assistant")
                {
                    history.AddAssistantMessage(message.Content);
                }
                else if(message.Role == "system")
                {
                    history.AddSystemMessage(message.Content);
                }
            }

            // Add new message to history.
            history.AddUserMessage("Can you tell me a joke?");
            await _threadRegistry.PostMessageAsync(userId, threadId, "Can you tell me a joke?", "user");

            var response = _completionService.GetStreamingChatMessageContentsAsync(
                chatHistory: history,
                kernel: _kernel
            );

            var assistantResponse = "";

            await foreach (var chunk in response)
            {
                Console.Write(chunk);
                assistantResponse += chunk.Content;
            }

            await _threadRegistry.PostMessageAsync(userId, threadId, assistantResponse, "assistant");


            return Ok();
        }
    }
}
