﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Data;

namespace Infrastructure
{
    public class AISearchService : ISearchService
    {
        private SearchClient _searchClient;
        private SearchIndexClient _indexClient;
        private SearchIndexerClient _indexerClient;
        private readonly ILogger<AISearchService> _logger;
        private readonly IConfiguration _configuration;

        private string indexerName;
        private string indexName;

        public AISearchService(SearchIndexClient indexClient, IConfiguration configuration, ILogger<AISearchService> logger)
        {
            _logger = logger;
            _configuration = configuration;

            indexerName = configuration.GetValue<string>("Search:IndexerName") ?? "onyourdata-indexer";
            indexName = configuration.GetValue<string>("Search:IndexName") ?? "onyourdata";

            _indexClient = indexClient;
            _searchClient = indexClient.GetSearchClient(indexName);
            _indexerClient = new SearchIndexerClient(_searchClient.Endpoint, new DefaultAzureCredential());
        }

        public async Task<bool> StartIndexing()
        {
            var response = await _indexerClient.RunIndexerAsync(indexerName);
            if (response.IsError)
                return false;
            return true;
        }

        public async Task<bool> DeleteDocumentAsync(DocsPerThread document)
        {
            try
            {
                
                var searchOptions = new SearchOptions
                {
                    Size = 500,
                    Select = { "chunk_id", "document_id", "thread_id" },
                    Filter = string.Format("document_id eq '{0}'", document.Id)
                };
                SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions);

                IndexDocumentsBatch<SearchDocument> batch = new IndexDocumentsBatch<SearchDocument>();
                await foreach (SearchResult<SearchDocument> searchResult in response.GetResultsAsync())
                {
                    var deleteAction = IndexDocumentsAction.Delete("chunk_id", searchResult.Document["chunk_id"].ToString());
                    batch.Actions.Add(deleteAction);
                }

                IndexDocumentsResult result = await _searchClient.IndexDocumentsAsync(batch);

            }
            catch (RequestFailedException ex)
            {
                return false;
            }

            return true;
        }

        public async Task<DocsPerThread> IsChunkingComplete(DocsPerThread docPerThread)
        {
            List<DocsPerThread> docsPerThread = new List<DocsPerThread> { docPerThread };
            var result = await IsChunkingComplete(docsPerThread);
            return result.First();
        }

        public async Task<List<DocsPerThread>> IsChunkingComplete(List<DocsPerThread> docsPerThreads)
        {

            for ( int x = 0; x < docsPerThreads.Count; x++)
            {
                var doc = docsPerThreads[x];
                var searchOptions = new SearchOptions
                {
                    Size = 1,
                    IncludeTotalCount = true,
                    Select = { "chunk_id", "document_id", "thread_id" },
                    Filter = string.Format("document_id eq '{0}'", doc.Id)
                };
                SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions);
                doc.AvailableInSearchIndex = response.TotalCount > 0;
            }

            return docsPerThreads;
        }

        public async Task<long> GetSearchResultsCountAsync()
        {
            var searchOptions = new SearchOptions
            {
                Size = 1,
                IncludeTotalCount = true,
                Select = { "chunk_id", "document_id", "thread_id" },
            };
            SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions);
            return response.TotalCount ?? 0;
        }

        public async Task<List<IndexDoc>> GetSearchResultsAsync(string query, string threadId)
        {
            List<IndexDoc> docs = new List<IndexDoc>();

            SearchOptions searchOptions = new SearchOptions
            {
                Size = 100,
                Filter = $"thread_id eq '{threadId}'",
                QueryType = SearchQueryType.Full
                
            };
            searchOptions.VectorSearch = new()
            {
                Queries = {
                        new VectorizableTextQuery(text: query)
                        {
                            KNearestNeighborsCount = 3,
                            Fields = { "content_vector" },
                            Exhaustive = true
                        }
                    },
            };

            SearchResults<IndexDoc> response = await _searchClient.SearchAsync<IndexDoc>(query, searchOptions);

            await foreach (SearchResult<IndexDoc> searchResult in response.GetResultsAsync())
            {
                docs.Add(searchResult.Document);
            }

            return docs;
        }

        public async Task<List<IndexDoc>> GetDocumentAsync(string documentId)
        {
            List<IndexDoc> docs = new List<IndexDoc>();

            SearchOptions searchOptions = new SearchOptions
            {
                Size = 100,
                Filter = $"document_id eq '{documentId}'",
                QueryType = SearchQueryType.Full
            };

            SearchResults<IndexDoc> response = await _searchClient.SearchAsync<IndexDoc>("*", searchOptions);

            await foreach (SearchResult<IndexDoc> searchResult in response.GetResultsAsync())
            {
                IndexDoc indexDoc = new IndexDoc()
                {
                    ChunkId = searchResult.Document.ChunkId,
                    DocumentId = searchResult.Document.DocumentId,
                    ThreadId = searchResult.Document.ThreadId,
                    Content = searchResult.Document.Content,
                    FileName = searchResult.Document.FileName,
                };

                docs.Add(indexDoc);
            }

            docs = docs.OrderBy(x => x.ChunkId).ToList();
            return docs;
        }
    }
}
