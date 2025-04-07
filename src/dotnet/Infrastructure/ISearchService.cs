using Domain.Cosmos;
using Domain.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

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
}
