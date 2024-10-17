using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Reflection.Metadata;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("/chats/{chatId}/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly BlobDocumentStore _blobDocumentStore;
        private readonly CosmosDocumentRegistry _cosmosDocumentRegistry;
        private readonly ILogger<DocumentController> _logger;
        private string _containerName = "documents";

        public DocumentController(
            ILogger<DocumentController> logger, 
            BlobDocumentStore blobDocumentStore, 
            CosmosDocumentRegistry cosmosDocumentRegistry)
        {
            _blobDocumentStore = blobDocumentStore;
            _cosmosDocumentRegistry = cosmosDocumentRegistry;
            _logger = logger;
        }

        [HttpGet(Name = "GetMyDocuments")]
        public async Task<IEnumerable<string>> Get(string threadId)
        {
            var results = await _cosmosDocumentRegistry.GetDocsPerThread(threadId);

            List<string> fileNames = new List<string>();
            foreach (var document in results)
            {
                fileNames.Add(document.DocumentName);
            }
            return fileNames;
        }

        [HttpPost(Name = "Upload")]
        public async Task<string> UploadDocument(string? document, string userId, string threadId)
        {
            if (string.IsNullOrEmpty(document))
                document = "C:\\Users\\rmeure\\downloads\\Brochure Elektrisch Rijden nieuw.pdf";


            // first step is to upload the document to the blob storage
            DocsPerThread docsPerThread = await _blobDocumentStore.AddDocumentAsync(userId, document, threadId, _containerName);
            if (docsPerThread == null)
                throw new Exception("File upload failed");

            // second step is to add the document to the cosmos db
            var result = await _cosmosDocumentRegistry.AddDocumentToThreadAsync(docsPerThread);

            // third step is to kick off the indexer
            //todo

            return result;
        }
    }
}
