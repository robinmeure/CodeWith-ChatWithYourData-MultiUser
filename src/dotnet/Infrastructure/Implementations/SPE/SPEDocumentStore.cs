using Domain.Cosmos;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Implementations.SPE
{
    public class SPEDocumentStore : IDocumentStore
    {
        private readonly ILogger<SPEDocumentStore> _logger;
        private readonly IConfiguration _configuration;
        private readonly MSGraphService _graphService;
        private readonly ITokenAcquisition _tokenAcquisition;

        private string containerTypeId;
        private string tenantId;

        public SPEDocumentStore(ILogger<SPEDocumentStore> logger, 
            IConfiguration configuration,
            MSGraphService graphService,
            ITokenAcquisition tokenAcquisition)
        {
            _logger = logger;
            _configuration = configuration;
            _graphService = graphService;
            _tokenAcquisition = tokenAcquisition;
            tenantId = _configuration.GetValue<string>("SPE:TenantId") ?? "f903e023-a92d-4561-9a3b-d8429e3fa1fd";
            containerTypeId = _configuration.GetValue<string>("SPE:ContainerTypeId") ?? "aaace084-a939-40a0-98f0-919307b365ab";
        }

        public async Task<DocsPerThread> AddDocumentAsync(string userId, IFormFile document, string fileName, string threadId, string folder)
        {
            //create a container for the thread first, if there's no container, we need to provision that.
            var driveId = await GetOrCreateDrive(folder, threadId);

            var documentId = Guid.NewGuid().ToString();
            var graphAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new string[] { GraphScope.Default }, tenantId: tenantId);
            var uploadedFile = await _graphService.AddFile(driveId:driveId.Id, accessToken: graphAccessToken, stream: document.OpenReadStream(), parentId:"root", name: fileName);

            //then create a document in the cosmos db with the metadata of the document
            DocsPerThread docsPerThread = new()
            {
                Id = uploadedFile.Id!.ToString(),
                ThreadId = threadId,
                DocumentName = fileName,
                UserId = userId,
                Folder = driveId.Id,
                FileSize = document.Length,
                UploadDate = DateTime.Now
            };

            return docsPerThread;
        }

        internal async Task<Drive> GetOrCreateDrive(string driveId, string threadId)
        {
            var graphAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new string[] { GraphScope.Default }, tenantId: tenantId);

           
            if (!string.IsNullOrEmpty(driveId))
                return await _graphService.GetDrive(graphAccessToken, driveId);
          
            var container = await _graphService.AddContainerAsync(graphAccessToken, containerName:threadId, containerTypeId, threadId:threadId);
            return await _graphService.GetDrive(graphAccessToken, container.Id);
          
        }

        public Task<bool> DeleteDocumentAsync(string documentName, string folder)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DocumentExistsAsync(string documentName, string folder)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<DocsPerThread>> GetAllDocumentsAsync(string folder)
        {
            var results = new List<DocsPerThread>();
            var graphAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new string[] { GraphScope.Default }, tenantId: tenantId);
            var documents = await _graphService.SearchDriveItems(graphAccessToken, "isDocument:true", containerTypeId, 100);
            foreach (var driveItem in documents)
            {
                DocsPerThread docsPerThread = new DocsPerThread()
                {
                    DocumentName = driveItem.Name!,
                    Folder = folder,
                    FileSize = driveItem.Size ?? 0,
                    ThreadId = driveItem.AdditionalData["threadId"]?.ToString(),
                    UploadDate = driveItem.LastModifiedDateTime?.DateTime ?? DateTime.MinValue, // Fix for CS0029
                    UserId = string.Empty,
                    Id = driveItem.Id!,
                };

                results.Add(docsPerThread);
            }

            return results;
        }

        public async Task<IEnumerable<string>> GetDocumentsAsync(string threadId, string folder)
        {
            var results = new List<string>();
            var graphAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new string[] { GraphScope.Default }, tenantId: tenantId);
            var documents = await _graphService.GetDriveRootItems(graphAccessToken, folder);
            foreach (var document in documents)
            {
                results.Add(document.Name);
            }

            return results;
        }

        public Task UpdateDocumentAsync(string documentName, string documentUri)
        {
            throw new NotImplementedException();
        }
    }
}
