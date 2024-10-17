using Azure.Identity;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class BlobDocumentStore : IDocumentStore
    {
        private BlobServiceClient _blobServiceClient;

        public BlobDocumentStore(BlobServiceClient client) {
            _blobServiceClient = client;
        }

        public async Task<string> AddDocumentAsync(string document, string threadId, string folder)
        {
            var documentId = Guid.NewGuid().ToString();
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(folder);
            var documentName = System.IO.Path.GetFileName(document);
            var blobClient = blobContainerClient.GetBlobClient(documentId);

            //Upload the document
            await blobClient.UploadAsync(document, true);

            //set meta data
            var metadata = new Dictionary<string, string>
            {               
                { "threadId", threadId },
                { "documentId", documentId },
                { "originalFilename", documentName }
            };
            blobClient.SetMetadata(metadata);

           
            return documentId;
        }

        public Task DeleteDocumentAsync(string documentName, string folder)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DocumentExistsAsync(string documentName, string folder)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetDocumentsAsync(string threadId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateDocumentAsync(string documentName, string documentUri)
        {
            throw new NotImplementedException();
        }
    }
}
