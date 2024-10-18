using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Domain;

namespace Infrastructure
{
    public class AISearchService : ISearchService
    {
        private SearchIndexClient _indexClient;
        private SearchClient _searchClient;

        public AISearchService(SearchIndexClient indexClient, SearchClient searchClient)
        {
            _indexClient = indexClient;
            _searchClient = searchClient;
        }


        public Task<bool> HasIndexingStarted(string threadId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsChunkingComplete(DocsPerThread docsPerThread)
        {
            SearchOptions options = new SearchOptions();
            options.Filter = $"threadId eq '{docsPerThread.ThreadId}'";
            options.Select.Add("chunk_id");
            options.Select.Add("title");
            options.Select.Add("id");

            var searchResponse = _searchClient.Search<DocsPerThread>(docsPerThread.ThreadId, options);
            if (searchResponse.Value.TotalCount > 0)
            {
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }
}
