using Domain.Chat;
using Domain.Cosmos;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Resource;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;
using WebApi.Helpers; // Ensure GetUserId extension method is available

namespace WebApi.Controllers
{
    [Route("admin")]
    [Authorize]
    [ApiController]
    [RequiredScope("chat")]
    public class AdminController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IDocumentRegistry _documentRegistry;
        private readonly IThreadRepository _threadRepository;
        private readonly ISearchService _searchService;
        private readonly ILogger<DocumentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAIService _aiservice;
        private readonly string _containerName;
        private readonly ThreadSafeSettings _settings;

        public AdminController(
            ILogger<DocumentController> logger,
            IDocumentStore blobDocumentStore,
            IDocumentRegistry cosmosDocumentRegistry,
            IThreadRepository threadRepository,
            ISearchService aISearchService,
            IConfiguration configuration,
            IAIService aiservice,
            ThreadSafeSettings settings
           )
        {
            _documentStore = blobDocumentStore;
            _documentRegistry = cosmosDocumentRegistry;
            _threadRepository = threadRepository;
            _searchService = aISearchService;
            _configuration = configuration;
            _logger = logger;
            _settings = settings;
            _aiservice = aiservice;

            // Read the container name from configuration
            _containerName = _configuration.GetValue<string>("Storage:ContainerName") ?? "documents";
        }

        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            try
            {
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("GetSettings called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                _logger.LogInformation("Fetching settings for user {UserId}", userId); // Assuming settings might be user-specific later
                return Ok(_settings.GetSettings());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetSettings");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching settings", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPatch("settings")]
        [Consumes("application/json")]
        public IActionResult UpdateSettings([FromBody] Settings settings)
        {
            try
            {
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("UpdateSettings called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                if (settings == null)
                {
                    _logger.LogWarning("UpdateSettings called with null settings payload");
                    return BadRequest(new { error = "Settings payload is required" });
                }

                _logger.LogInformation("Updating settings by user {UserId}", userId);

                _settings.UpdateSettings(settings);

                _logger.LogInformation("Settings updated successfully by user {UserId}", userId);
                return Ok(_settings.GetSettings());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in UpdateSettings");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while updating settings", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("documents")]
        public async Task<IActionResult> GetDocuments()
        {
            try
            {
                // Use GetUserId extension method
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("GetDocuments (admin) called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                _logger.LogInformation("Admin {UserId} fetching all documents from store '{ContainerName}'", userId, _containerName);
                var documentsInBlob = await _documentStore.GetAllDocumentsAsync(_containerName);
                // Add null check for documentsInBlob before accessing Count()
                _logger.LogInformation("Successfully fetched {Count} documents for admin {UserId}", documentsInBlob?.Count() ?? 0, userId);
                return Ok(documentsInBlob ?? Enumerable.Empty<DocsPerThread>()); // Return empty if null
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDocuments (admin)");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching documents", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("documents/{documentId}/chunks")]
        public async Task<IActionResult> GetChunks([FromRoute] string documentId)
        {
            try
            {
                // Use GetUserId extension method
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("GetChunks (admin) called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                if (string.IsNullOrEmpty(documentId))
                {
                    return BadRequest(new { error = "DocumentId is required" });
                }

                _logger.LogInformation("Admin {UserId} fetching chunks for documentId: {DocumentId}", userId, documentId);
                var documents = await _searchService.GetDocumentAsync(documentId);
                _logger.LogInformation("Successfully fetched chunks for documentId: {DocumentId} for admin {UserId}", documentId, userId);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetChunks (admin) for documentId: {DocumentId}", documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching chunks", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("threads")]
        public async Task<IActionResult> GetThreads()
        {
            try
            {
                // Use GetUserId extension method
                string? userId = HttpContext.GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("GetThreads (admin) called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                _logger.LogInformation("Admin {UserId} fetching all threads", userId);
                // Assuming GetAllThreads might need admin privileges handled elsewhere or is safe
                // Fully qualify Domain.Cosmos.Thread to resolve ambiguity
                var threads = await _threadRepository.GetAllThreads();
                _logger.LogInformation("Successfully fetched {Count} threads for admin {UserId}", threads?.Count() ?? 0, userId);
                // Use fully qualified name for Enumerable.Empty
                return Ok(threads ?? Enumerable.Empty<Domain.Cosmos.Thread>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetThreads (admin)");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching threads", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("check")]
        public async Task<IActionResult> Check()
        {
            // Use GetUserId extension method
            string? userId = HttpContext.GetUserId();
            if (userId == null)
            {
                 _logger.LogWarning("Check (admin) called with null userId");
                return Unauthorized(new { error = "UserId is required" });
            }

            try
            {
                // Initialize Components dictionary
                var healthStatus = new SystemHealthStatus
                {
                    Timestamp = DateTime.UtcNow,
                    Components = new Dictionary<string, ComponentStatus>()
                };

                // 1. Check AI Service
                try
                {
                    var chatHistory = new ChatHistory("health check test", AuthorRole.User);
                    var chatCompletion = await _aiservice.GetChatCompletion(chatHistory);

                    healthStatus.Components["AIService"] = new ComponentStatus
                    {
                        IsWorking = chatCompletion != null,
                        Message = chatCompletion != null ?
                            "Successfully generated chat completion" :
                            "Failed to generate chat completion"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI Service health check failed");
                    healthStatus.Components["AIService"] = new ComponentStatus
                    {
                        IsWorking = false,
                        Message = $"Error: {ex.Message}"
                    };
                }

                // 2. Check Thread Repository
                try
                {
                    var threads = await _threadRepository.GetAllThreads();

                    healthStatus.Components["ThreadRepository"] = new ComponentStatus
                    {
                        IsWorking = threads != null,
                        Message = threads != null ?
                            $"Successfully retrieved {threads.Count} threads" :
                            "Failed to retrieve threads"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Thread Repository health check failed");
                    healthStatus.Components["ThreadRepository"] = new ComponentStatus
                    {
                        IsWorking = false,
                        Message = $"Error: {ex.Message}"
                    };
                }

                // 3. Check Search Service
                try
                {
                    // Try to get any document - this assumes at least one document exists
                    long documentsInIndex = await _searchService.GetSearchResultsCountAsync();

                    healthStatus.Components["SearchService"] = new ComponentStatus
                    {
                        IsWorking = (documentsInIndex > 0),
                        Message = (documentsInIndex > 0) ?
                            $"Successfully retrieved {documentsInIndex} search results" :
                            "No search results found"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Search Service health check failed");
                    healthStatus.Components["SearchService"] = new ComponentStatus
                    {
                        IsWorking = false,
                        Message = $"Error: {ex.Message}"
                    };
                }

                // 4. Check Document Registry
                try
                {
                    // Get docs from a thread if any exist
                    var threads = await _threadRepository.GetAllThreads();
                    var threadId = threads?.FirstOrDefault()?.Id; // Safe navigation

                    bool hasDocuments = false;
                    string message = "No threads available to check documents";
                    int documentCount = 0;

                    if (!string.IsNullOrEmpty(threadId))
                    {
                        var documents = await _documentRegistry.GetDocsPerThreadAsync(threadId);
                        documentCount = documents?.Count ?? 0; // Null check
                        hasDocuments = documentCount > 0;
                        message = hasDocuments ?
                            // Use documentCount
                            $"Successfully retrieved {documentCount} documents from thread" :
                            "No documents found in thread";
                    }

                    healthStatus.Components["DocumentRegistry"] = new ComponentStatus
                    {
                        IsWorking = threadId != null, // At least the service responded
                        Message = message
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Document Registry health check failed");
                    healthStatus.Components["DocumentRegistry"] = new ComponentStatus
                    {
                        IsWorking = false,
                        Message = $"Error: {ex.Message}"
                    };
                }

                // 5. Check Document Store
                try
                {
                    var blobDocuments = await _documentStore.GetAllDocumentsAsync(_containerName);

                    healthStatus.Components["DocumentStore"] = new ComponentStatus
                    {
                        IsWorking = blobDocuments != null,
                        Message = blobDocuments != null ?
                            $"Successfully retrieved documents from blob storage" :
                            "Failed to retrieve documents from blob storage"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Document Store health check failed");
                    healthStatus.Components["DocumentStore"] = new ComponentStatus
                    {
                        IsWorking = false,
                        Message = $"Error: {ex.Message}"
                    };
                }

                // Calculate overall health status
                healthStatus.IsHealthy = healthStatus.Components.Values.All(c => c.IsWorking);

                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with an unexpected error");
                return StatusCode(500, new { Error = "Health check failed with an unexpected error", Message = ex.Message });
            }
        }

    }

    // Define the health check response models
    public class SystemHealthStatus
    {
        public bool IsHealthy { get; set; }
        public DateTime Timestamp { get; set; }
        // Initialize dictionary or make nullable
        public Dictionary<string, ComponentStatus> Components { get; set; } = new Dictionary<string, ComponentStatus>();
    }

    public class ComponentStatus
    {
        public bool IsWorking { get; set; }
        // Make Message nullable
        public string? Message { get; set; }
    }
}
