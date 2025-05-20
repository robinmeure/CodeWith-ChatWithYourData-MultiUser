using Azure.AI.Inference;
using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web.Resource;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebApi.Helpers;
using ResponseMessage = Domain.Chat.ResponseMessage;
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
        private readonly ThreadSafeSettings _settings;
        private readonly IMemoryCache _memoryCache;
        private readonly IDocumentRegistry _documentRegistry;

        public ThreadController(
            ILogger<ThreadController> logger,
            IThreadRepository cosmosThreadRepository,
            IConfiguration configuration,
            ISearchService search,
            IAIService aIService,
            ThreadSafeSettings settings,
            IMemoryCache memoryCache,
            IDocumentRegistry documentRegistry
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadRepository = cosmosThreadRepository ?? throw new ArgumentNullException(nameof(cosmosThreadRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _aiService = aIService ?? throw new ArgumentNullException(nameof(aIService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _documentRegistry = documentRegistry ?? throw new ArgumentNullException(nameof(documentRegistry));

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
                           UsageMetrics: null,                        
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

            _logger.LogInformation("Processing new message for threadId: {ThreadId}", threadId);            try
            {
                var answer = await HandleUserMessageAsync(userId, threadId, messageRequest.Message, cancellationToken);
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
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.InternalServerError), 
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
        
        [HttpPost("{threadId}/messages/compliancy/stream")]
        [Produces("text/event-stream")]
        public async Task<IActionResult> StreamCompliancyResponse(
            [FromRoute] string threadId, 
            [FromBody] MessageRequest messageRequest, 
            CancellationToken cancellationToken)
        {
            string? userId = HttpContext.GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Stream compliancy called with null userId");
                return BadRequest(new { error = "User ID is required." });
            }

            if (string.IsNullOrEmpty(threadId))
            {
                _logger.LogWarning("Stream compliancy called with null or empty threadId");
                return BadRequest(new { error = "Thread ID is required." });
            }

            if (messageRequest == null || string.IsNullOrEmpty(messageRequest.Message))
            {
                _logger.LogWarning("Stream compliancy called with null or empty message");
                return BadRequest(new { error = "Message content is required." });
            }

            _logger.LogInformation("Processing streaming compliancy response for threadId: {ThreadId}", threadId);

            try
            {
                // Save the user's message
                await _threadRepository.PostMessageAsync(
                    userId,
                    new ThreadMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        ThreadId = threadId,
                        UserId = userId,
                        Role = "user",
                        Content = messageRequest.Message,
                        Type = "CHAT_MESSAGE",
                        Created = DateTime.UtcNow
                    },
                    cancellationToken);

                // Get search results
                var history = _aiService.BuildConversationHistory(
                    await _threadRepository.GetMessagesAsync(userId, threadId, cancellationToken), 
                    messageRequest.Message);
                    
                var query = messageRequest.Message;

                string uniqueExtractsString = string.Empty;
                var extractedDocs = await _search.GetExtractedResultsAsync(threadId);
                foreach (var extractedDoc in extractedDocs)
                {
                    uniqueExtractsString += string.Join(Environment.NewLine, extractedDoc.Extract);
                }
                
                // Get the agent stream
                //var stream = _aiService.GetCompliancyResponseStreamingViaAgentsAsync(threadId, uniqueExtractsString, cancellationToken);

                // Get the chatcompletion stream
                var stream = _aiService.GetCompliancyResponseStreamingViaCompletionAsync(threadId, uniqueExtractsString, cancellationToken);

                string finalContent = null;

                // Process and send each chunk in the stream
                await foreach (var chatResponse in stream.WithCancellation(cancellationToken))
                {
                    string payload = System.Text.Json.JsonSerializer.Serialize(
                        new { role = chatResponse.Role.ToString(), content = chatResponse.Content, final = false });
                    await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);

                    finalContent += chatResponse.Content;
                }



                // Send a final message with follow-up questions as part of the SSE stream
                if (!string.IsNullOrWhiteSpace(finalContent))
                {
                    // Generate follow-up questions if enabled
                    var followUpQuestionList = _settings.GetSettings().AllowFollowUpPrompts
                        ? await _aiService.GenerateFollowUpQuestionsAsync(history, finalContent, finalContent)
                        : Array.Empty<string>();

                    // Send the final message with follow-up questions
                    string finalPayload = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            role = "assistant",
                            content = finalContent,
                            followupQuestions = followUpQuestionList,
                            final = true
                        });
                    await Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);

                    // Fire and forget saving the final assistant message to the database
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _threadRepository.PostMessageAsync(
                                userId,
                                new ThreadMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ThreadId = threadId,
                                    UserId = userId,
                                    Role = "assistant",
                                    Content = finalContent,
                                    Type = "CHAT_MESSAGE",
                                    Created = DateTime.UtcNow,
                                    Context = new ResponseContext(
                                        DataPointsContent: null,
                                        FollowupQuestions: followUpQuestionList,
                                        Thoughts: Array.Empty<Thoughts>(),
                                        UsageMetrics: null)
                                },
                                CancellationToken.None);
                            _logger.LogInformation("Successfully saved final compliancy response for threadId: {ThreadId}", threadId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving final compliancy response for threadId: {ThreadId}", threadId);
                        }
                    });
                }

                return new EmptyResult();
            
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in stream compliancy: {Type} - {Message}", ex.ServiceType, ex.Message);
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
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.InternalServerError), 
                    new { 
                        error = "Service temporarily unavailable", 
                        details = httpEx.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing streaming compliancy for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { 
                        error = "An unexpected error occurred", 
                        details = ex.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
        }

        [HttpPost("{threadId}/messages/stream")]
        [Produces("text/event-stream")]
        public async Task<IActionResult> StreamChatResponse(
            [FromRoute] string threadId, 
            [FromBody] MessageRequest messageRequest, 
            CancellationToken cancellationToken)
        {
            string? userId = HttpContext.GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Stream chat called with null userId");
                return BadRequest(new { error = "User ID is required." });
            }

            if (string.IsNullOrEmpty(threadId))
            {
                _logger.LogWarning("Stream chat called with null or empty threadId");
                return BadRequest(new { error = "Thread ID is required." });
            }

            if (messageRequest == null || string.IsNullOrEmpty(messageRequest.Message))
            {
                _logger.LogWarning("Stream chat called with null or empty message");
                return BadRequest(new { error = "Message content is required." });
            }

            _logger.LogInformation("Processing streaming chat response for threadId: {ThreadId}", threadId);

            try
            {                // Save the user's message
                await _threadRepository.PostMessageAsync(
                    userId,
                    new ThreadMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        ThreadId = threadId,
                        UserId = userId,
                        Role = "user",
                        Content = messageRequest.Message,
                        Type = "CHAT_MESSAGE",
                        Created = DateTime.UtcNow
                    },
                    cancellationToken);

                // Get conversation history
                var history = await BuildConversationHistoryAsync(
                    userId, threadId, messageRequest.Message, cancellationToken);
                    
                // Perform search with potential query rewrite
                string query = (_settings.GetSettings().AllowInitialPromptRewrite)
                    ? await RewriteQuestionAsync(history, cancellationToken)
                    : messageRequest.Message;

                string toolName = string.Empty;
                if (messageRequest.Tools != null && messageRequest.Tools.Count > 0)
                {
                    toolName = messageRequest.Tools.FirstOrDefault();
                }

                IAsyncEnumerable<StreamingChatMessageContent> stream = null;

                if (toolName == "incose")
                {
                    // Perform specific logic for "incose" tool
                    // Example: Implement a special search or processing here
                    var uniqueExtractsString = string.Empty;
                    var extractedDocs = await _search.GetExtractedResultsAsync(threadId);
                    foreach (var extractedDoc in extractedDocs)
                    {
                        uniqueExtractsString += string.Join(Environment.NewLine, extractedDoc.Extract);
                    }
                    stream = _aiService.GetCompliancyResponseStreamingViaCompletionAsync(threadId, uniqueExtractsString, cancellationToken);
                }
                else
                { 
                    // if a document is selected, we need to search for that document only
                    var searchResults = (messageRequest.DocumentIds.Count > 0) ?
                        await PerformSearchAsync(history, query, threadId, messageRequest.DocumentIds, cancellationToken) :
                        await PerformSearchAsync(history, query, threadId, cancellationToken);

                    stream = _aiService.GetChatCompletionStreaming(history);
                } 

                string finalContent = string.Empty;
                UsageMetrics? usageMetrics = new();

                // Process and send each chunk in the stream
                await foreach (var chatResponse in stream.WithCancellation(cancellationToken))
                {
                    string payload = System.Text.Json.JsonSerializer.Serialize(
                        new { role = chatResponse.Role.ToString(), content = chatResponse.Content, final = false });
                    await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);

                    // get usage
                    if (chatResponse.InnerContent is StreamingChatCompletionUpdate chatCompletion)
                    {
                        if (chatCompletion.Usage != null)
                        {
                            usageMetrics.InputTokens = chatCompletion.Usage.InputTokenCount;
                            usageMetrics.OutputTokens = chatCompletion.Usage.OutputTokenCount;
                        }
                    }
                    finalContent += chatResponse.Content;
                }                
                


                // Send a final message with follow-up questions as part of the SSE stream
                if (!string.IsNullOrWhiteSpace(finalContent))
                {
                    // Generate follow-up questions if enabled
                    var followUpQuestionList = _settings.GetSettings().AllowFollowUpPrompts
                        ? await _aiService.GenerateFollowUpQuestionsAsync(history, finalContent, finalContent)
                        : Array.Empty<string>();

                    // Send the final message with follow-up questions
                    string finalPayload = System.Text.Json.JsonSerializer.Serialize(
                        new { 
                            role = "assistant", 
                            content = finalContent, 
                            followupQuestions = followUpQuestionList,
                            usageMetrics = usageMetrics,
                            final = true 
                        });
                    await Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    
                    // Fire and forget saving the final assistant message
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _threadRepository.PostMessageAsync(
                                userId,
                                new ThreadMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ThreadId = threadId,
                                    UserId = userId,
                                    Role = "assistant",
                                    Content = finalContent,
                                    Type = "CHAT_MESSAGE",
                                    Created = DateTime.UtcNow,
                                    Context = new ResponseContext(
                                        DataPointsContent: null,
                                        FollowupQuestions: followUpQuestionList,
                                        Thoughts: Array.Empty<Thoughts>(),
                                        UsageMetrics: usageMetrics)
                                },
                                CancellationToken.None);
                            _logger.LogInformation("Successfully saved final chat response for threadId: {ThreadId}", threadId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving final chat response for threadId: {ThreadId}", threadId);
                        }
                    });
                }

                return new EmptyResult();
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Service error in stream chat: {Type} - {Message}", ex.ServiceType, ex.Message);
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
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.InternalServerError), 
                    new { 
                        error = "Service temporarily unavailable", 
                        details = httpEx.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing streaming chat for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { 
                        error = "An unexpected error occurred", 
                        details = ex.Message,
                        requestId = HttpContext.TraceIdentifier
                    });
            }
        }

        #region Message Processing Methods
        
        private async Task<ThreadMessage> CreateAndSaveUserMessage(string userId, string threadId, string message, CancellationToken cancellationToken)
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
            await _threadRepository.PostMessageAsync(userId, question, cancellationToken);
            return question;
        }

        private async Task<ChatHistory> BuildConversationHistoryAsync(string userId, string threadId, string message, CancellationToken cancellationToken)
        {
            string cacheKey = $"history:{userId}:{threadId}:{message}";
            var messages = await _threadRepository.GetMessagesAsync(userId, threadId, cancellationToken);
            var history = _aiService.BuildConversationHistory(messages, message);
            return history;
        }

        private async Task<string> RewriteQuestionAsync(ChatHistory history, CancellationToken cancellationToken)
        {
            string query = await _aiService.RewriteQueryAsync(history);
            _logger.LogInformation("Query rewritten to: {Query}", query);
            return query;
        }

        private async Task<List<IndexDoc>> PerformSearchAsync(ChatHistory history, string query, string threadId, CancellationToken cancellationToken)
        {
            string sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(query, @"[^\w\s]", string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");
            sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(sanitarizedQuery, @"\s+", " ").Trim();
            var searchResults = await _search.GetSearchResultsAsync(sanitarizedQuery, threadId);
            _aiService.AugmentHistoryWithSearchResults(history, searchResults);
            return searchResults;
        }

        private async Task<List<IndexDoc>> PerformSearchAsync(ChatHistory history, string query, string threadId, List<string> documentIds, CancellationToken cancellationToken)
        {
            string sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(query, @"[^\w\s]", string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");
            sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(sanitarizedQuery, @"\s+", " ").Trim();
            var searchResults = await _search.GetSearchResultsAsync(sanitarizedQuery, threadId, documentIds);
            _aiService.AugmentHistoryWithSearchResults(history, searchResults);
            return searchResults;
        }

        private async Task<ThreadMessage> GenerateAndSaveAssistantResponseAsync(string userId, string threadId, ChatHistory history, List<IndexDoc> searchResults, CancellationToken cancellationToken)
        {
            var assistantAnswer = await _aiService.GetChatCompletion(history, Enums.CompletionType.Chat);
            if (!assistantAnswer.IsSuccess)
            {
                if (assistantAnswer.Error.IsRateLimit)
                {
                    _logger.LogWarning("Rate limit exceeded for user {UserId} in thread {ThreadId}", userId, threadId);
                    throw new HttpOperationException(System.Net.HttpStatusCode.TooManyRequests, assistantAnswer.Error.Message, assistantAnswer.Error.Message, null);
                }
                else
                {
                    _logger.LogError("Error generating response for user {UserId} in thread {ThreadId}: {Error}", userId, threadId, assistantAnswer.Error.Message);
                    throw new ServiceException(assistantAnswer.Error.Message, ServiceType.AIService);
                }
            }
            
            var followUpQuestionList = _settings.GetSettings().AllowFollowUpPrompts
                ? await _aiService.GenerateFollowUpQuestionsAsync(history, assistantAnswer.Content, assistantAnswer.Content)
                : Array.Empty<string>();
            
            var answer = new ThreadMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = "assistant",
                Content = assistantAnswer.Content,
                Context = new ResponseContext(
                    FollowupQuestions: followUpQuestionList,
                    DataPointsContent: null,
                    Thoughts: null!,
                    UsageMetrics: assistantAnswer.Usage),
                Created = DateTime.UtcNow
            };
            await _threadRepository.PostMessageAsync(userId, answer, cancellationToken);
            return answer;
        }
        
        private async Task<ThreadMessage> HandleUserMessageAsync(string userId, string threadId, string message, CancellationToken cancellationToken)
        {
            var question = await CreateAndSaveUserMessage(userId, threadId, message, cancellationToken);
            var history = await BuildConversationHistoryAsync(userId, threadId, message, cancellationToken);
            string query = (_settings.GetSettings().AllowInitialPromptRewrite)
                ? await RewriteQuestionAsync(history, cancellationToken)
                : message;

            var documents = await _documentRegistry.GetDocsPerThreadAsync(threadId);
            var searchResults = await PerformSearchAsync(history, query, threadId, cancellationToken);
            var answer = await GenerateAndSaveAssistantResponseAsync(userId, threadId, history, searchResults, cancellationToken);
            return answer;
        }
        
        #endregion
    }
}
