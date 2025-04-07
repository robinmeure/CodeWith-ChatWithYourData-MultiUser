using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web.Resource;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using WebApi.Helpers;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Domain.Cosmos;
using Domain.Chat;
using ResponseMessage = Domain.Chat.ResponseMessage;
using Microsoft.Extensions.Logging;
using Domain.Search;
using Thread = Domain.Cosmos.Thread;

namespace WebApi.Controllers
{

    [Route("threads")]
    [Authorize]
    [ApiController]
    [RequiredScope("chat")]

    public class ThreadController : ControllerBase
    {
        private readonly IThreadRepository _threadRepository;
        private readonly ILogger<ThreadController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAIService _aiService;
        private readonly ISearchService _search;
        private readonly Settings _settings;

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRepository cosmosThreadRepository,
            IConfiguration configuration,
            ISearchService search,
            IAIService aIService,
            Settings settings
            )
        {
            _threadRepository = cosmosThreadRepository;
            _configuration = configuration;
            _logger = logger;
            _search = search;
            _aiService = aIService;
            _settings = settings;
        }


       

        [HttpGet("")]
        public async Task<IActionResult> GetThreads()
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();

            _logger.LogInformation("Fetching threads from CosmosDb for userId : {0}", userId);
            
            List<Domain.Cosmos.Thread> threads = await _threadRepository.GetThreadsAsync(userId);
            threads = threads.OrderByDescending(t => t.LastUpdated).ToList();

            _logger.LogInformation("Fetched threads from CosmosDb for userId : {0}", userId);
            return Ok(threads);
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateThread()
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();
            
            _logger.LogInformation("Creating thread in CosmosDb for userId : {0}", userId);

            Domain.Cosmos.Thread thread = await _threadRepository.CreateThreadAsync(userId);

            if(thread == null)
            {
                _logger.LogInformation("Failed to create thread in CosmosDb for userId : {0}", userId);
            }

            _logger.LogInformation("Created thread in CosmosDb for userId : {0}", userId);
            
            //await _threadRepository.PostMessageAsync(userId, thread.Id, "You are a helpful assistant that helps people find information.", "system");

            return Ok(thread);
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId)
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();

            bool result = await _threadRepository.MarkThreadAsDeletedAsync(userId, threadId);

            if (result)
                return Ok();

            return BadRequest();
           
        }
        [HttpPatch("{threadId}")]
        public async Task<IActionResult> UpdateThread([FromRoute] string threadId, [FromBody]string title)
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();

            Dictionary<string, object> fieldsToUpdate = new Dictionary<string, object>
            {
                { "threadName", title }
            };
            bool success = await _threadRepository.UpdateThreadFieldsAsync(threadId, userId, fieldsToUpdate);
            if (success)
            {
                Thread updatedThread = await _threadRepository.GetThreadAsync(userId, threadId);
                return Ok(updatedThread);
            }
            return BadRequest();
        }

        [HttpGet("{threadId}/messages")]
        public async Task<IActionResult> Get([FromRoute] string threadId)
        {
            _logger.LogInformation("Fetching thread messages from CosmosDb for threadId : {0}", threadId);
            string? userId = HttpContext.GetUserId();
            if (userId == null)
                return BadRequest();

            List<ThreadMessage> result = await _threadRepository.GetMessagesAsync(userId, threadId);

            if (result.Count <= 1)
            {
                // Let's start welcoming the user and see if we can jump start the conversation by adding some predefined prompts
                string[] followUpQuestionList = new string[] { "Help me generate questions about the document", "Summarize the document" };

                var responseMessage = new ResponseMessage("assistant", "Do you need help?");
                var responseContext = new ResponseContext(
                       FollowupQuestions: followUpQuestionList ?? Array.Empty<string>(),
                       DataPointsContent: null,
                       Thoughts: null);

                var answer = new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "CHAT_MESSAGE",
                    ThreadId = threadId,
                    UserId = userId,
                    Role = responseMessage.Role,
                    Content = responseMessage.Content,
                    Context = responseContext,
                    Created = DateTime.Now
                };
                result.Add(answer);
            }
            return Ok(result);
        }

        [HttpDelete("{threadId}/messages")]
        public async Task<IActionResult> DeleteMessages([FromRoute] string threadId)
        {
            _logger.LogInformation("Deleting messages in CosmosDb for threadId : {0}", threadId);

            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return BadRequest();
            }

            bool result = await _threadRepository.DeleteMessages(userId, threadId);

            if (result)
            {
                return Ok();
            }

            return BadRequest();

        }


        [HttpPost("{threadId}/messages")]
        [Produces("application/json")]
        [Consumes("application/json")]
        public async Task<IActionResult> Post([FromRoute] string threadId, [FromBody] MessageRequest messageRequest)
        {
            string? userId = HttpContext.GetUserId();
            if (userId == null)
                return BadRequest("User ID is required.");

            try
            {
                var question = await CreateAndSaveUserMessage(userId, threadId, messageRequest.Message);
                var history = await BuildConversationHistory(userId, threadId, messageRequest.Message);
                string query = (_settings.AllowInitialPromptRewrite) ? await RewriteQuestion(history) : messageRequest.Message;
                var searchResults = await PerformSearch(history, threadId, query);
                var answer = await GenerateAndSaveAssistantResponse(userId, threadId, history, searchResults);

                return Ok(answer);
            }
            catch (HttpOperationException httpEx)
            {
                _logger.LogError("HTTP operation failed: {Message}", httpEx.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message for thread {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        private async Task<ThreadMessage> CreateAndSaveUserMessage(string userId, string threadId, string message)
        {
            var question = new ThreadMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = "user",
                Content = message,
                Context = null,
                Created = DateTime.UtcNow
            };

            await _threadRepository.PostMessageAsync(userId, question);
            return question;
        }

        private async Task<ChatHistory> BuildConversationHistory(string userId, string threadId, string message)
        {
            var messages = await _threadRepository.GetMessagesAsync(userId, threadId);
            return _aiService.BuildConversationHistory(messages, message);
        }

        private async Task<string> RewriteQuestion(ChatHistory history)
        {
            string query = await _aiService.RewriteQueryAsync(history);
            _logger.LogInformation("Query rewritten to: {Query}", query);

            return query;
        }

        private async Task<List<IndexDoc>> PerformSearch(ChatHistory history, string query, string threadId)
        {
            var searchResults = await _search.GetSearchResultsAsync(threadId, query);
            _aiService.AugmentHistoryWithSearchResults(history, searchResults);

            return searchResults;
        }

        private async Task<ThreadMessage> GenerateAndSaveAssistantResponse(
            string userId,
            string threadId,
            ChatHistory history,
            List<IndexDoc> searchResults)
        {
            // Get the AI response
            var assistantAnswer = await _aiService.GetChatCompletion(history);
            if (assistantAnswer == null)
            {
                throw new InvalidOperationException("Failed to generate assistant response");
            }

            // Generate follow-up questions if enabled
            var followUpQuestionList = _settings.AllowFollowUpPrompts
                ? await _aiService.GenerateFollowUpQuestionsAsync(history, assistantAnswer.Answer, assistantAnswer.Answer)
                : Array.Empty<string>();

            // Create thoughts list
            var thoughts = new List<Thoughts>();
            if (!string.IsNullOrEmpty(assistantAnswer.Thoughts))
            {
                thoughts.Add(new Thoughts("Answer", assistantAnswer.Thoughts));
            }

            // Create the response message
            var answer = new ThreadMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = "assistant",
                Content = assistantAnswer.Answer,
                Context = new ResponseContext(
                    FollowupQuestions: followUpQuestionList,
                    DataPointsContent: null,
                    Thoughts: thoughts.ToArray()),
                Created = DateTime.UtcNow
            };

            // Save the response
            await _threadRepository.PostMessageAsync(userId, answer);

            return answer;
        }
    }
}
