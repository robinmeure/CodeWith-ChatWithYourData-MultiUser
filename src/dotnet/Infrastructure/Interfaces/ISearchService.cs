using Domain.Cosmos;
using Domain.Search;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Interfaces;

public interface ISearchService
{
    //Task<bool> IsChunkingComplete(string threadId);
    Task<List<DocsPerThread>> IsChunkingComplete(List<DocsPerThread> docsPerThreads);
    Task<DocsPerThread> IsChunkingComplete(DocsPerThread docsPerThreads);
    Task<bool> StartIndexing();
    Task<bool> DeleteDocumentAsync(DocsPerThread document);
    Task<List<IndexDoc>> GetSearchResultsAsync(string query, string threadId);
    Task<long> GetSearchResultsCountAsync();
    Task<List<IndexDoc>> GetDocumentAsync(string documentId);

    /// <summary>
    /// Performs a health check on the search service
    /// </summary>
    /// <returns>Task that completes when the health check is done</returns>
    Task IsHealthyAsync();

    Task IngestDocumentIntoIndex(List<IFormFile> documents, string threadId, string containerName, string userId);
}
