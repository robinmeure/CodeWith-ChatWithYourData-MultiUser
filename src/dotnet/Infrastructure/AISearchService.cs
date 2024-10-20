﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Domain;

namespace Infrastructure
{
    public class AISearchService : ISearchService
    {
        private SearchIndexClient _indexClient;
        private SearchClient _searchClient;
        private SearchIndexerClient _indexerClient;

        private readonly string indexerName = "onyourdata-indexer";

        public AISearchService(SearchIndexClient indexClient, SearchClient searchClient, SearchIndexerClient indexerClient)
        {
            _indexClient = indexClient;
            _searchClient = searchClient;
            _indexerClient = indexerClient;
        }


        public async Task<bool> StartIndexing()
        {
            var response = await _indexerClient.RunIndexerAsync(indexerName);
            if (response.IsError)
                return false;
            return true;
        }

        //public Task<bool> IsChunkingComplete(string threadId)
        //{ 
        //    List<DocsPerThread> docsPerThread = new List<DocsPerThread>();
        //    docsPerThread.Add(new DocsPerThread { ThreadId = threadId });
        //    return IsChunkingComplete(docsPerThread);
        //}
        public async Task<List<DocsPerThread>> IsChunkingComplete(List<DocsPerThread> docsPerThreads)
        {
            var query = new VectorizableTextQuery("thread_id = '1234'");
            query.Fields.Add("content_vector");

            var searchOptions = new SearchOptions
            {
                VectorSearch = new()
                {
                    Queries = { query }
                },
                Size = 10,
                Select = { "chunk_id", "content_vector", "file_name", "document_id", "thread_id" },
            };

            SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(null, searchOptions);

            // temporary list to store the documents that are found in the search index
            var fromSearch = new HashSet<string>();
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                fromSearch.Add(result.Document["document_id"].ToString());
            }

            // Use LINQ to update the AvailableInSearchIndex property
            var returnSet = docsPerThreads.Select(dpt =>
            {
                dpt.AvailableInSearchIndex = fromSearch.Contains(dpt.Id);
                return dpt;
            }).ToList();

            return returnSet;
        }
    }
}
