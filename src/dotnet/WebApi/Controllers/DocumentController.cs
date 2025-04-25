using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Domain.Cosmos;
using Infrastructure.Implementations.KernelMemory;
using Infrastructure.Implementations.SPE;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Resource;
using Microsoft.KernelMemory.Models;
using System.Reflection.Metadata;
using System.Xml.Linq;
using WebApi.Helpers; // Ensure GetUserId extension method is available
using System.IO;
using Domain.Chat; // For Path.GetExtension

namespace WebApi.Controllers
{
    [Route("/threads/{threadId}/documents")]
    [Authorize]
    [ApiController]
    [RequiredScope("chat")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IDocumentRegistry _documentRegistry;
        private readonly ISearchService _searchService;
        private readonly ILogger<DocumentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDocumentProcessorQueue _documentProcessorQueue;
        private readonly IAIService _aIService; 

        private readonly string _containerName;
        private readonly HashSet<string> _blockedFileExtensions;

        public DocumentController(
            ILogger<DocumentController> logger,
            IDocumentStore blobDocumentStore,
            IDocumentRegistry cosmosDocumentRegistry,
            ISearchService aISearchService,
            IConfiguration configuration,
            IDocumentProcessorQueue documentProcessorQueue,
            IAIService aIService
            )
        {
            _documentStore = blobDocumentStore;
            _documentRegistry = cosmosDocumentRegistry;
            _searchService = aISearchService;
            _configuration = configuration;
            _logger = logger;
            _documentProcessorQueue = documentProcessorQueue;
            _aIService = aIService;
            // Read the container name from configuration
            _containerName = _configuration.GetValue<string>("Storage:ContainerName") ?? "documents";

            // Read blocked file extensions from configuration
            var blockedExtensions = _configuration.GetSection("BlockedFileExtensions").Get<List<string>>() ?? new List<string>();
            _blockedFileExtensions = new HashSet<string>(blockedExtensions.Select(ext => ext.ToLower()));

        }

        [HttpGet("{documentId}/chunks")]
        public async Task<IActionResult> GetChunks([FromRoute] string documentId)
        {
            try
            {
                // Use GetUserId extension method for consistency
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("GetChunks called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                _logger.LogInformation("Fetching chunks for documentId: {DocumentId}", documentId);
                var documents = await _searchService.GetDocumentAsync(documentId);
                _logger.LogInformation("Successfully fetched chunks for documentId: {DocumentId}", documentId);

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetChunks for documentId: {DocumentId}", documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching chunks", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("{documentId}/extract")]
        public async Task<IActionResult> GetExtract([FromRoute] string threadId, [FromRoute] string documentId)
        {
            try
            {
                // Use GetUserId extension method for consistency
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                    _logger.LogWarning("GetChunks called with null userId");
                    return Unauthorized(new { error = "UserId is required" });
                }

                _logger.LogInformation("Fetching chunks for documentId: {DocumentId}", documentId);
                var document = await _searchService.GetExtractedResultsAsync(threadId, documentId);
              
                var extract = document?.Extract;
                _logger.LogInformation("Successfully fetched chunks for documentId: {DocumentId}", documentId);

                return Ok(extract);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetChunks for documentId: {DocumentId}", documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching chunks", requestId = HttpContext.TraceIdentifier });
            }
        }


        [HttpGet()]
        // Change return type
        public async Task<ActionResult<IEnumerable<DocsPerThread>>> Get([FromRoute] string threadId)
        {
            try
            {
                _logger.LogInformation("Fetching documents from CosmosDb for threadId : {0}", threadId);

                // fetch the documents from cosmos which belong to this thread
                var documents = await _documentRegistry.GetDocsPerThreadAsync(threadId);

                _logger.LogInformation("Comparing documents from Cosmos against Search for threadId : {0}", threadId);

                // if there are no documents with the current thread, return an empty collection
                if (documents == null || !documents.Any())
                    return Ok(Enumerable.Empty<DocsPerThread>()); // Use Ok()

                // check for the uploaded docs if they are chunked
                var results = await _searchService.IsChunkingComplete(documents);
                _logger.LogInformation("Successfully checked chunking status for {Count} documents for threadId: {ThreadId}", results.Count(), threadId);
                return Ok(results); // Use Ok()
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Get documents for threadId: {ThreadId}", threadId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while fetching documents", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPost("{documentId}", Name = "ExtractDocument")]
        public async Task<IActionResult> ExtractDocument([FromRoute] string threadId, string documentId)
        {
            // Use GetUserId extension method for consistency
            string? userId = HttpContext.GetUserId();

            if (userId == null)
            {
                _logger.LogWarning("UploadDocuments called with null userId");
                return Unauthorized(new { error = "UserId is required" });
            }

            if (documentId == null)
            {
                _logger.LogWarning("No documentId provided.");
                return BadRequest("No documentId provided.");
            }

            try
            {
                _logger.LogInformation("Extracting document with ID: {DocumentId}", documentId);
                var documents = await _searchService.GetDocumentAsync(documentId);
                if (documents == null || !documents.Any())
                {
                    _logger.LogWarning("No documents found for ID: {DocumentId}", documentId);
                    return NotFound(new { error = "No documents found." });
                }

                // need to do this via the document processor queue later.. 
                var extracted = await _aIService.ExtractDocument(documents);

                if (string.IsNullOrEmpty(extracted))
                {
                    _logger.LogWarning("No extracted content found for documentId: {DocumentId}", documentId);
                    return NotFound(new { error = "No extracted content found." });
                }

                // ingest the extracted content into the search index
                bool ingested = await _searchService.IngestExtractedDocumentIntoIndex(extracted, documentId);
                if (ingested)
                {
                    _logger.LogInformation("Successfully ingested extracted content for documentId: {DocumentId}", documentId);
                }
                else
                {
                    _logger.LogWarning("Failed to ingest extracted content for documentId: {DocumentId}", documentId);
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to ingest extracted content." });
                }

                // Return the extracted documents
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ExtractDocument for documentId: {DocumentId}", documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while extracting the document", requestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpPost()]
        public async Task<IActionResult> UploadDocuments(List<IFormFile> documents, [FromRoute] string threadId)
        {
            // Use GetUserId extension method for consistency
            string? userId = HttpContext.GetUserId();

            if (userId == null)
            {
                _logger.LogWarning("UploadDocuments called with null userId");
                return Unauthorized(new { error = "UserId is required" });
            }

            if (documents == null || !documents.Any())
            {
                _logger.LogWarning("No files uploaded.");
                return BadRequest("No files uploaded.");
            }

            // Define the maximum file size limit (e.g., 100 MB)
            const long maxFileSize = 100 * 1024 * 1024;

            // Use structured result list
            var uploadResults = new List<UploadResult>();

            // this is the default container name
            // if the document store is a SharePoint document store, we need to determine the container name (e.g. the driveId)
            // if there's no driveId yet, we need to pass an empty string for the first upload to create a container
            string containerName = _containerName;
            if (_documentStore is SPEDocumentStore)
            {
                try
                {
                    containerName = await DetermineContainerNameAsync(threadId);
                    if (!IsDriveId(containerName))
                        containerName = string.Empty; // Signal to create container on first upload
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Error determining container name for thread {ThreadId}", threadId);
                     // Return a general error if container determination fails critically
                     return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to determine storage location.", requestId = HttpContext.TraceIdentifier });
                }
            }


            foreach (var document in documents)
            {
                var result = new UploadResult { FileName = document.FileName, Success = false };
                try
                {
                    // Check if the file size exceeds the limit
                    if (document.Length > maxFileSize)
                    {
                        _logger.LogWarning("File size exceeds the limit: {0}", document.FileName);
                        result.ErrorMessage = "File size exceeds the limit.";
                        uploadResults.Add(result);
                        continue;
                    }

                    // check if the file extension is blocked
                    var fileExtension = Path.GetExtension(document.FileName)?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(fileExtension) || _blockedFileExtensions.Contains(fileExtension))
                    {
                        _logger.LogWarning("Filetype blocked or invalid: {0}", document.FileName);
                        result.ErrorMessage = "File type is blocked or invalid.";
                        uploadResults.Add(result);
                        continue;
                    }

                    // Sanitize the filename
                    var fileName = Utilities.SanitizeFileName(document.FileName);

                    _logger.LogInformation("Uploading document: {0}", fileName);

                    // Step 1: Upload to document store
                    DocsPerThread docsPerThread = await _documentStore.AddDocumentAsync(userId, document, fileName, threadId, containerName);
                    if (docsPerThread == null)
                    {
                        throw new Exception("File upload to store failed.");
                    }
                    // SPE ONLY: Update containerName if it was created during the first upload
                    if (string.IsNullOrEmpty(containerName) && !string.IsNullOrEmpty(docsPerThread.Folder))
                    {
                         containerName = docsPerThread.Folder;
                         _logger.LogInformation("Container created/identified for thread {ThreadId}: {ContainerName}", threadId, containerName);
                    }

                    _logger.LogInformation("Document uploaded to storage: {0}", fileName);

                    // Step 2: Add metadata to registry
                    var registryResult = await _documentRegistry.AddDocumentToThreadAsync(docsPerThread);
                     if (string.IsNullOrEmpty(registryResult)) 
                     {
                         // Consider compensating action: delete from storage?
                         throw new Exception("Failed to add document metadata to registry.");
                     }
                    _logger.LogInformation("Document added to Cosmos DB: {0}", fileName);

                    // Step 3A: Enqueue for using Kernel Memory for chunking/embedding and ingesting into the search index
                    _logger.LogInformation("Enqueueing document for processing: {0}", fileName);
                    using (var memoryStream = new MemoryStream())
                    {
                        // Copy the document stream to memory
                        await document.CopyToAsync(memoryStream);

                        // Enqueue the document for processing
                        _documentProcessorQueue.EnqueueDocumentUri(new DocumentToProcess(
                            memoryStream.ToArray(),
                            fileName, // Use sanitized name
                            document.ContentType,
                            docsPerThread));
                    }
                    _logger.LogInformation("Document enqueued: {0}", fileName);

                    result.Success = true;
                    uploadResults.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading document: {FileName}", document.FileName);
                    result.ErrorMessage = $"An error occurred: {ex.Message}"; // Provide a generic error
                    uploadResults.Add(result);
                    // Continue with the next document
                }
                finally
                {
                    // Dispose of the document stream if necessary
                    if (document is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            // Step 3B: when making use of the Azure AI Search indexer that picks up the documents from the azure blob storage
            //if (uploadResults != null)
            //{
            //    // Third step is to kick off the indexer
            //    _logger.LogInformation("Starting indexing process.");
            //    var chunks = await _searchService.StartIndexing();
            //    _logger.LogInformation("Indexing process started.");
            //    return Ok(uploadResults);
            //}

            return Ok(uploadResults);
        }

        [HttpDelete("{documentId}", Name = "DeleteDocumentFromThread")]
        public async Task<IActionResult> RemoveDocumentAsync([FromRoute] string threadId, string documentId)
        {
            try
            {
                _logger.LogInformation("Attempting to soft delete document from CosmosDb for threadId {0} and documentId {1}", threadId, documentId);
                string? userId = HttpContext.GetUserId(); 

                if (userId == null)
                {
                     _logger.LogWarning("RemoveDocumentAsync called with null userId");
                     return Unauthorized(new { error = "UserId is required" });
                }

                var documentToDelete = await _documentRegistry.GetDocPerThreadAsync(threadId, documentId); 

                if (documentToDelete != null)
                {
                    documentToDelete.Deleted = true;
                    var removedFromSearch = await _searchService.DeleteDocumentAsync(documentToDelete);
                    _logger.LogInformation("Document removed from index for threadId {ThreadId} and documentId {DocumentId}", threadId, documentId);

                    if (removedFromSearch)
                        await _documentRegistry.RemoveDocumentAsync(documentToDelete);
                    _logger.LogInformation("Successfully soft deleted document {DocumentId} for threadId {ThreadId}", documentId, threadId);

                    // Consider also deleting from search index and blob storage asynchronously via a queue
                    return Ok(new { success = true, message = "Document marked for deletion." });
                }

                _logger.LogWarning("Document not found or already deleted for threadId {ThreadId} and documentId {DocumentId}", threadId, documentId);
                return NotFound(new { error = "Document not found or already deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RemoveDocumentAsync for threadId: {ThreadId}, documentId: {DocumentId}", threadId, documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while deleting the document", requestId = HttpContext.TraceIdentifier });
            }
        }

        // This route deletes ALL documents for a thread. Use with caution.
        [HttpDelete("", Name = "DeleteAllDocumentsInThread")] // Renamed for clarity
        public async Task<IActionResult> RemoveAllDocumentsFromThreadAsync([FromRoute] string threadId)
        {
             try
             {
                _logger.LogInformation("Attempting to soft delete all documents from CosmosDb for threadId : {0}", threadId);
                string? userId = HttpContext.GetUserId();

                if (userId == null)
                {
                     _logger.LogWarning("RemoveAllDocumentsFromThreadAsync called with null userId");
                     return Unauthorized(new { error = "UserId is required" });
                }

                // Fetch all non-deleted documents for the thread
                var results = await _documentRegistry.GetDocsPerThreadAsync(threadId);
                var documentsToDelete = results?.Where(doc => !doc.Deleted).ToList();

                if (documentsToDelete != null && documentsToDelete.Any())
                {
                    _logger.LogInformation("Soft deleting {Count} documents from CosmosDb for threadId {ThreadId}", documentsToDelete.Count, threadId);

                    // Check if _documentRegistry supports bulk delete/update
                    // Option 1: If bulk update exists (preferred)
                    // await _documentRegistry.RemoveDocumentsAsync(documentsToDelete.Select(doc => { doc.Deleted = true; return doc; }));

                    // Option 2: Iterate if no bulk method exists
                    foreach (var doc in documentsToDelete)
                    {
                        doc.Deleted = true;
                        await _documentRegistry.RemoveDocumentAsync(doc); 
                    }
                    _logger.LogInformation("Successfully soft deleted {Count} documents for threadId {ThreadId}", documentsToDelete.Count, threadId);
                    return Ok(new { success = true, message = $"{documentsToDelete.Count} documents marked for deletion." });
                }

                _logger.LogInformation("No active documents found to delete for threadId {ThreadId}", threadId);
                return Ok(new { success = true, message = "No active documents found to delete." }); // Return Ok even if none found
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Unexpected error in RemoveAllDocumentsFromThreadAsync for threadId: {ThreadId}", threadId);
                 return StatusCode(StatusCodes.Status500InternalServerError,
                     new { error = "An unexpected error occurred while deleting documents", requestId = HttpContext.TraceIdentifier });
             }
        }

        private bool IsDriveId(string containerName)
        {
            // SharePoint drive IDs typically start with "b!" and contain a mix of letters, numbers, dashes, and underscores
            return !string.IsNullOrEmpty(containerName) &&
                   containerName.StartsWith("b!") &&
                   containerName.Length > 10 &&
                   containerName.All(c => char.IsLetterOrDigit(c) || c == '!' || c == '-' || c == '_');
        }

        private async Task<string> DetermineContainerNameAsync(string threadId)
        {
            // First check if there's a drive ID specifically associated with this thread
            try
            {
                var threadDriveId = await _documentRegistry.GetFolderForThreadAsync(threadId);
                if (!string.IsNullOrEmpty(threadDriveId))
                {
                    _logger.LogInformation("Using thread-specific drive ID: {DriveId}", threadDriveId);
                    return threadDriveId;
                }
                 _logger.LogInformation("No specific drive ID found for thread {ThreadId}, will use default or create.", threadId);
            }
            catch (NotSupportedException)
            {
                _logger.LogDebug("GetFolderForThreadAsync not supported by the current document registry implementation. Using configured container.");
            }
            catch (Exception ex)
            {
                // Log but don't necessarily fail the whole upload yet, fallback to default.
                _logger.LogWarning(ex, "Failed to get drive ID for thread {ThreadId}. Falling back to default.", threadId);
            }

            // Fall back to the configured container name
            string containerName = _configuration.GetValue<string>("Storage:ContainerName") ?? "documents";
            _logger.LogInformation("Using container name from configuration or default: {ContainerName}", containerName);
            return containerName; // Return default/configured name
        }

    }
}
