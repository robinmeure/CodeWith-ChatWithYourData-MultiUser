using Domain.Chat;
using Domain.Cosmos;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Resource;
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
        private readonly string _containerName;
        private Settings _settings;

        public AdminController(
           ILogger<DocumentController> logger,
           IDocumentStore blobDocumentStore,
           IDocumentRegistry cosmosDocumentRegistry,
           IThreadRepository threadRepository,
           ISearchService aISearchService,
           IConfiguration configuration,
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

    }
}
