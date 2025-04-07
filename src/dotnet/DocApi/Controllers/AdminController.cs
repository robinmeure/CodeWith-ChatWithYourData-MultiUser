using Domain.Chat;
using Domain.Cosmos;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Resource;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

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
        private Settings _settings;

        public AdminController(
           ILogger<DocumentController> logger,
           IDocumentStore blobDocumentStore,
           IDocumentRegistry cosmosDocumentRegistry,
           IThreadRepository threadRepository,
           ISearchService aISearchService,
           IConfiguration configuration,
           IAIService aiservice,
            Settings settings
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
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();

            return Ok(_settings);
        }

        [HttpPatch("settings")]
        [Consumes("application/json")]
        public IActionResult UpdateSettings([FromBody] Settings settings)
        {
            string? userId = HttpContext.GetUserId();

            if (userId == null)
                return BadRequest();

            if (settings == null)
                return BadRequest();

            _logger.LogInformation("Updating settings");

            _settings.AllowFollowUpPrompts = settings.AllowFollowUpPrompts;
            _settings.AllowInitialPromptRewrite = settings.AllowInitialPromptRewrite;
            _settings.UseSemanticRanker = settings.UseSemanticRanker;
            _settings.AllowInitialPromptToHelpUser = settings.AllowInitialPromptToHelpUser;
            _settings.PredefinedPrompts = settings.PredefinedPrompts;

            return Ok(_settings);
        }

        [HttpGet("documents")]
        public async Task<IActionResult> GetDocuments()
        {
            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return Unauthorized();
            }

            var documentsInBlob = await _documentStore.GetAllDocumentsAsync("documents");
            return Ok(documentsInBlob);
        }

        [HttpGet("documents/{documentId}/chunks")]
        public async Task<IActionResult> GetChunks([FromRoute] string documentId)
        {
            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return Unauthorized();
            }

            var documents = await _searchService.GetDocumentAsync(documentId);

            return Ok(documents);
        }

        [HttpGet("threads")]
        public async Task<IActionResult> GetThreads()
        {
            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return Unauthorized();
            }

            var threads = await _threadRepository.GetAllThreads();

            return Ok(threads);
        }

        [HttpGet("check")]
        public async Task<IActionResult> Check()
        {
            string userId = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (userId == null)
            {
                return Unauthorized();
            }

            try
            {
                // Define the health check result structure
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
                    var threadId = threads.FirstOrDefault()?.Id;

                    bool hasDocuments = false;
                    string message = "No threads available to check documents";

                    if (!string.IsNullOrEmpty(threadId))
                    {
                        var documents = await _documentRegistry.GetDocsPerThreadAsync(threadId);
                        hasDocuments = documents != null && documents.Any();
                        message = hasDocuments ?
                            $"Successfully retrieved {documents.Count} documents from thread" :
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
        public Dictionary<string, ComponentStatus> Components { get; set; }
    }

    public class ComponentStatus
    {
        public bool IsWorking { get; set; }
        public string Message { get; set; }
    }
}
