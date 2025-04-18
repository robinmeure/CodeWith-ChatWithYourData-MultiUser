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
using System.Net;
using System.Runtime.ExceptionServices;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using System.ComponentModel.DataAnnotations; // For validation attributes

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
        private readonly ThreadSafeSettings _settings;
        private readonly IThreadService _threadService;


        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRepository cosmosThreadRepository,
            IConfiguration configuration,
            ISearchService search,
            IAIService aIService,
            ThreadSafeSettings settings,
            IThreadService threadService
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadRepository = cosmosThreadRepository ?? throw new ArgumentNullException(nameof(cosmosThreadRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _aiService = aIService ?? throw new ArgumentNullException(nameof(aIService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _threadService = threadService;

            _logger.LogInformation("ThreadController initialized successfully");
        }

        [HttpGet("")]
        public async Task<IActionResult> GetThreads()
        {
            try
            {
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("GetThreads called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }

                _logger.LogInformation("Fetching threads from CosmosDb for userId: {UserId}", userId);
                
                List<Domain.Cosmos.Thread> threads = await _threadRepository.GetThreadsAsync(userId);
                threads = threads.OrderByDescending(t => t.LastUpdated).ToList();

                _logger.LogInformation("Successfully fetched {Count} threads for userId: {UserId}", threads.Count, userId);
                return Ok(threads);
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in GetThreads: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetThreads");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateThread()
        {
            try
            {
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("CreateThread called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }
                
                _logger.LogInformation("Creating thread in CosmosDb for userId: {UserId}", userId);

                Domain.Cosmos.Thread thread = await _threadRepository.CreateThreadAsync(userId);

                if(thread == null)
                {
                    _logger.LogWarning("Failed to create thread in CosmosDb for userId: {UserId}", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, 
                        new { error = "Failed to create thread", requestId = HttpContext.TraceIdentifier });
                }

                _logger.LogInformation("Successfully created thread {ThreadId} for userId: {UserId}", thread.Id, userId);
                return Ok(thread);
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in CreateThread: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateThread");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread([FromRoute] string threadId)
        {
            try
            {
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("DeleteThread called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }

                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogWarning("DeleteThread called with null or empty threadId");
                    return BadRequest(new { error = "ThreadId is required" });
                }

                _logger.LogInformation("Deleting thread {ThreadId} for userId: {UserId}", threadId, userId);
                bool result = await _threadRepository.MarkThreadAsDeletedAsync(userId, threadId);

                if (result)
                {
                    _logger.LogInformation("Successfully deleted thread {ThreadId} for userId: {UserId}", threadId, userId);
                    return Ok(new { success = true });
                }

                _logger.LogWarning("Failed to delete thread {ThreadId} for userId: {UserId}", threadId, userId);
                return NotFound(new { error = "Thread not found or delete operation failed" });
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in DeleteThread: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteThread for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPatch("{threadId}")]
        public async Task<IActionResult> UpdateThread([FromRoute] string threadId, [FromBody] string title)
        {
            try
            {
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("UpdateThread called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }

                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogWarning("UpdateThread called with null or empty threadId");
                    return BadRequest(new { error = "ThreadId is required" });
                }

                if (string.IsNullOrEmpty(title))
                {
                    _logger.LogWarning("UpdateThread called with null or empty title");
                    return BadRequest(new { error = "Title is required" });
                }

                _logger.LogInformation("Updating thread {ThreadId} for userId: {UserId}", threadId, userId);
                Dictionary<string, object> fieldsToUpdate = new Dictionary<string, object>
                {
                    { "threadName", title }
                };

                bool success = await _threadRepository.UpdateThreadFieldsAsync(threadId, userId, fieldsToUpdate);

                if (success)
                {
                    Thread updatedThread = await _threadRepository.GetThreadAsync(userId, threadId);
                    _logger.LogInformation("Successfully updated thread {ThreadId} for userId: {UserId}", threadId, userId);
                    return Ok(updatedThread);
                }

                _logger.LogWarning("Failed to update thread {ThreadId} for userId: {UserId}", threadId, userId);
                return NotFound(new { error = "Thread not found or update operation failed" });
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in UpdateThread: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in UpdateThread for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("{threadId}/messages")]
        public async Task<IActionResult> Get([FromRoute] string threadId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Fetching thread messages from CosmosDb for threadId: {ThreadId}", threadId);
                string? userId = HttpContext.GetUserId();
                
                if (userId == null)
                {
                    _logger.LogWarning("Get messages called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }

                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogWarning("Get messages called with null or empty threadId");
                    return BadRequest(new { error = "ThreadId is required" });
                }

                List<ThreadMessage> result = await _threadRepository.GetMessagesAsync(userId, threadId, cancellationToken);
                _logger.LogInformation("Successfully fetched {Count} messages for threadId: {ThreadId}", result.Count, threadId);

                if (result.Count <= 1 && _settings.GetSettings().AllowInitialPromptToHelpUser)
                {
                    string[] followUpQuestionList = new string[] { "Help me generate questions about the document", "Summarize the document" };
                    var responseMessage = new ResponseMessage("assistant", "Do you need help?");
                    var responseContext = new ResponseContext(
                           FollowupQuestions: followUpQuestionList ?? Array.Empty<string>(),
                           DataPointsContent: null,
                           Thoughts: System.Array.Empty<Thoughts>());
                    var answer = new ThreadMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "CHAT_MESSAGE",
                        ThreadId = threadId,
                        UserId = userId,
                        Role = responseMessage.Role,
                        Content = responseMessage.Content,
                        Context = responseContext,
                        Created = DateTime.UtcNow
                    };
                    result.Add(answer);
                    _logger.LogInformation("Added initial welcome message for new thread {ThreadId}", threadId);
                }
                return Ok(result);
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in Get messages: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Get messages for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpDelete("{threadId}/messages")]
        public async Task<IActionResult> DeleteMessages([FromRoute] string threadId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Deleting messages in CosmosDb for threadId: {ThreadId}", threadId);
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("DeleteMessages called with null userId");
                    return BadRequest(new { error = "UserId is required" });
                }
                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogWarning("DeleteMessages called with null or empty threadId");
                    return BadRequest(new { error = "ThreadId is required" });
                }
                bool result = await _threadRepository.DeleteMessages(userId, threadId, cancellationToken);
                if (result)
                {
                    _logger.LogInformation("Successfully deleted all messages for threadId: {ThreadId}", threadId);
                    return Ok(new { success = true });
                }
                _logger.LogWarning("Failed to delete messages for threadId: {ThreadId}", threadId);
                return NotFound(new { error = "Thread not found or delete messages operation failed" });
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in DeleteMessages: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = $"Service unavailable: {ex.ServiceType}", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteMessages for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An unexpected error occurred", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPost("{threadId}/messages")]
        [Produces("application/json")]
        [Consumes("application/json")]
        public async Task<IActionResult> Post([FromRoute] string threadId, [FromBody] MessageRequest messageRequest, CancellationToken cancellationToken)
        {
            string? userId = HttpContext.GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Post message called with null userId");
                return BadRequest(new { error = "User ID is required." });
            }

            if (string.IsNullOrEmpty(threadId))
            {
                _logger.LogWarning("Post message called with null or empty threadId");
                return BadRequest(new { error = "Thread ID is required." });
            }

            if (messageRequest == null || string.IsNullOrEmpty(messageRequest.Message))
            {
                _logger.LogWarning("Post message called with null or empty message");
                return BadRequest(new { error = "Message content is required." });
            }

            _logger.LogInformation("Processing new message for threadId: {ThreadId}", threadId);

            try
            {
                var answer = await _threadService.HandleUserMessageAsync(userId, threadId, messageRequest.Message, cancellationToken);
                _logger.LogInformation("Successfully processed message for threadId: {ThreadId}", threadId);
                return Ok(answer);
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in Post message: {Type} - {Message}", ex.ServiceType, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { 
                        error = $"Service unavailable: {ex.ServiceType}", 
                        details = ex.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
            catch (HttpOperationException httpEx)
            {
                _logger.LogError(httpEx, "HTTP operation failed: {Message}", httpEx.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { 
                        error = "Service temporarily unavailable", 
                        details = httpEx.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { 
                        error = "An unexpected error occurred", 
                        details = ex.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
        }
    }
}
