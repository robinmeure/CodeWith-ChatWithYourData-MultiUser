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

namespace DocApi.Controllers
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

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRepository cosmosThreadRepository,
            IConfiguration configuration,
            ISearchService search,
            IAIService aIService
            )
        {
            _threadRepository = cosmosThreadRepository;
            _configuration = configuration;
            _logger = logger;
            _search = search;
            _aiService = aIService;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetThreads()
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
            {
                return BadRequest();
            }

            _logger.LogInformation("Fetching threads from CosmosDb for userId : {0}", userId);
            
            List<Domain.Cosmos.Thread> threads = await _threadRepository.GetThreadsAsync(userId);

            _logger.LogInformation("Fetched threads from CosmosDb for userId : {0}", userId);
            return Ok(threads);
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateThread()
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
            {
                return BadRequest();
            }
            _logger.LogInformation("Creating thread in CosmosDb for userId : {0}", userId);

            Domain.Cosmos.Thread thread = await _threadRepository.CreateThreadAsync(userId);

            if(thread == null)
            {
                _logger.LogInformation("Failed to create thread in CosmosDb for userId : {0}", userId);
            }

            _logger.LogInformation("Created thread in CosmosDb for userId : {0}", userId);
            
            await _threadRepository.PostMessageAsync(userId, thread.Id, "You are a helpful assistant that helps people find information.", "system");

            return Ok(thread);
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId)
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
            {
                return BadRequest();
            }

            bool result = await _threadRepository.MarkThreadAsDeletedAsync(userId, threadId);

            if (result)
            {
                return Ok();
            } 

            return BadRequest();
           
        }

        [HttpGet("{threadId}/messages")]
        public async Task<IActionResult> Get([FromRoute] string threadId)
        {
            _logger.LogInformation("Fetching thread messages from CosmosDb for threadId : {0}", threadId);
            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return BadRequest();
            }

            List<ThreadMessage> result = await _threadRepository.GetMessagesAsync(userId, threadId);
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
            bool suggestFollowupQuestions = true; // need to configure this
            var thoughts = new List<Thoughts>();
            _logger.LogInformation("Adding thread message to CosmosDb for threadId : {0}", threadId);

            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return BadRequest();
            }

            try
            {
                // Create the user's question message
                var question = new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "CHAT_MESSAGE",
                    ThreadId = threadId,
                    UserId = userId,
                    Role = "user",
                    Content = messageRequest.Message,
                    Context = null,
                    Created = DateTime.Now
                };

                List<ThreadMessage> messages = await _threadRepository.GetMessagesAsync(userId, threadId);

                ChatHistory history = _aiService.BuildConversationHistory(messages, messageRequest.Message);
                string rewrittenQuery = await _aiService.RewriteQueryAsync(history);

                var searchResults = await _search.GetSearchResultsAsync(rewrittenQuery, threadId);

                _aiService.AugmentHistoryWithSearchResults(history, searchResults);

                var assistantAnswer = await _aiService.GetChatCompletion(history);
                thoughts.Add(new Thoughts("Answer", assistantAnswer.Thoughts));

                // Get follow-up questions
                string[] followUpQuestionList = null;
                if (suggestFollowupQuestions)
                {
                    followUpQuestionList = await _aiService.GenerateFollowUpQuestionsAsync(
                        history, assistantAnswer.Answer, messageRequest.Message);
                }

                var responseMessage = new ResponseMessage("assistant", assistantAnswer.Answer);
                var responseContext = new ResponseContext(
                       FollowupQuestions: followUpQuestionList ?? Array.Empty<string>(),
                       DataPointsContent: null,
                       Thoughts: thoughts.ToArray());

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

                // Post the messages to the repository
                await _threadRepository.PostMessageAsync(userId, question);
                await _threadRepository.PostMessageAsync(userId, answer);

                return Ok(answer);
            }
            catch (HttpOperationException httpOperationException)
            {
                _logger.LogError("An error occurred: {0}", httpOperationException.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred: {0}", ex.Message);
            }
            return new EmptyResult();
        }
    }
}
