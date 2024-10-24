using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using System.Text;
using DocApi.Utils;

namespace DocApi.Controllers
{
    [Route("threads")]
    [ApiController]
    public class ThreadController : ControllerBase
    {
        private readonly IThreadRepository _threadRepository;
        private readonly ILogger<ThreadController> _logger;
        private readonly IConfiguration _configuration;
        private readonly Kernel _kernel;
        private readonly VectorStoreTextSearch<IndexDoc> _search;
        private readonly PromptUtils _promptUtils;

        public class MessageRequest
        {
            public string UserId { get; set; }
            public string Message { get; set; }
        }

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRepository cosmosThreadRepository,
            IConfiguration configuration,
            Kernel kernel,
            VectorStoreTextSearch<IndexDoc> search,
            PromptUtils promptUtils
            )
        {
            _threadRepository = cosmosThreadRepository;
            _configuration = configuration;
            _logger = logger;
            _kernel = kernel;
            _search = search;
            _promptUtils = promptUtils;
        }

        [HttpGet("")]
        public async Task<List<Domain.Thread>> GetThreads([FromQuery] string userId)
        {
            _logger.LogInformation("Fetching threads from CosmosDb for userId : {0}", userId);
            
            List<Domain.Thread> threads = await _threadRepository.GetThreadsAsync(userId);

            _logger.LogInformation("Fetched threads from CosmosDb for userId : {0}", userId);
            return threads;
        }

        [HttpPost("")]
        public async Task<Domain.Thread> CreateThread([FromQuery] string userId)
        {
            _logger.LogInformation("Creating thread in CosmosDb for userId : {0}", userId);

            Domain.Thread thread = await _threadRepository.CreateThreadAsync(userId);

            if(thread == null)
            {
                _logger.LogInformation("Failed to create thread in CosmosDb for userId : {0}", userId);
            }

            _logger.LogInformation("Created thread in CosmosDb for userId : {0}", userId);
            
            await _threadRepository.PostMessageAsync(userId, thread.Id, "You are a helpful assistant that helps people find information.", "system");

            return thread;
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId, [FromQuery] string userId)
        {
            _logger.LogInformation("Deleting thread in CosmosDb for threadId : {0}", threadId);

            bool result = await _threadRepository.DeleteThreadAsync(userId, threadId);

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
            List<ThreadMessage> result = await _threadRepository.GetMessagesAsync(userId, threadId);
            return result;
        }

        [HttpPost("{threadId}/messages")]
        [Produces("text/event-stream")]
        [Consumes("application/json")]
        public async Task<IActionResult> Post([FromRoute] string threadId, [FromBody] MessageRequest messageRequest)
        {
            _logger.LogInformation("Adding thread message to CosmosDb for threadId : {0}", threadId);

            List<ThreadMessage> messages = await _threadRepository.GetMessagesAsync(messageRequest.UserId, threadId);

            ChatHistory history = _promptUtils.BuildConversationHistory(messages, messageRequest.Message);
           
            await _threadRepository.PostMessageAsync(messageRequest.UserId, threadId, messageRequest.Message, "user");

            IChatCompletionService completionService = _kernel.GetRequiredService<IChatCompletionService>();

            //Rewrite query for retrieval.
            string rewrittenQuery = await _promptUtils.RewriteQueryAsync(history);

            // Text search.
            var filter = new TextSearchFilter().Equality("ThreadId", threadId);
            var searchOptions = new TextSearchOptions() { 
                Filter = filter,
                Top = 3
            };
            KernelSearchResults<object> searchResults = await _search.GetSearchResultsAsync(rewrittenQuery, searchOptions);

            await _promptUtils.AugmentHistoryWithSearchResults(history, searchResults);

            var response = completionService.GetStreamingChatMessageContentsAsync(
                chatHistory: history,
                kernel: _kernel
            );

            var assistantResponse = "";

            await using (StreamWriter streamWriter = new StreamWriter(Response.Body, Encoding.UTF8))
            {
                await foreach (var chunk in response)
                {
                    Console.Write(chunk);
                    assistantResponse += chunk.Content;
                    await streamWriter.WriteAsync(chunk.Content);
                    await streamWriter.FlushAsync();
                }
            }
            
            await _threadRepository.PostMessageAsync(messageRequest.UserId, threadId, assistantResponse, "assistant");
            
            return new EmptyResult();
        }
    }
}
