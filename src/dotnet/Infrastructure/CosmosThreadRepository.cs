using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Domain;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Thread = Domain.Thread;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Infrastructure
{

    public class CosmosThreadRepository : IThreadRepository
    {
        private Container _container;

        public CosmosThreadRepository(Container cosmosDbContainer)
        {
            _container = cosmosDbContainer;
        }

        public List<ThreadMessage> GetAllThreads(DateTime expirationDate)
        {
            IQueryable<ThreadMessage> threads = _container
                .GetItemLinqQueryable<ThreadMessage>(allowSynchronousQueryExecution: true)
                .Where(o => o.Created <= expirationDate);

            return threads.ToList();
        }

        public async Task<List<Thread>> GetThreadsAsync(string userId)
        {

            List<Thread> threads = new List<Thread>();
            string query = string.Format("SELECT * FROM c WHERE c.userId = '{0}' AND c.type = 'CHAT_THREAD' AND c.deleted = false ORDER BY c._ts DESC", userId);
            var queryDefinition = new QueryDefinition(query);
            var queryOptions = new QueryRequestOptions
            {
                MaxItemCount = 500
            };

            using (var iterator = _container.GetItemQueryIterator<Domain.Thread>(queryDefinition, requestOptions: queryOptions))
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        threads.Add(item);
                    }
                }
            }

            return threads;
        }

        public async Task<bool> DeleteThreadAsync(string userId, string threadId)
        {
            Domain.Thread thread = await _container.ReadItemAsync<Domain.Thread>(threadId, new PartitionKey(userId));
            if (thread == null)
            {
                return false;
            }

            var messages = await this.GetMessagesAsync(userId, threadId);

            foreach (ThreadMessage message in messages)
            {
                await _container.DeleteItemAsync<ThreadMessage>(message.Id, new PartitionKey(userId));
            }

            thread.Deleted = true;

            var response = await _container.ReplaceItemAsync<Domain.Thread>(thread, threadId, new PartitionKey(userId));

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return false;
            }
            return true;

        }

        public async Task<Domain.Thread> CreateThreadAsync(string userId)
        {

            string threadId = Guid.NewGuid().ToString();
            DateTime now = DateTime.Now;
            string threadName = now.ToString("dd MMM yyyy, HH:mm");
            Domain.Thread newThread = new()
            {

                Id = threadId,
                Type = "CHAT_THREAD",
                UserId = userId,
                ThreadName = threadName
            };


            var response = await _container.CreateItemAsync<Domain.Thread>(newThread, new PartitionKey(userId));
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception("Failed to create a new thread.");
            }
            return response;

        }

        public async Task<List<ThreadMessage>> GetMessagesAsync(string userId, string threadId)
        {

            List<ThreadMessage> messages = new List<ThreadMessage>();
            string query = string.Format("SELECT * FROM m WHERE m.threadId = '{0}' ORDER BY m._ts ASC", threadId);
            var queryDefinition = new QueryDefinition(query);
            var queryOptions = new QueryRequestOptions
            {
                MaxItemCount = 500
            };

            using (var iterator = _container.GetItemQueryIterator<ThreadMessage>(queryDefinition, requestOptions: queryOptions))
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        messages.Add(item);
                    }
                }
            }

            return messages;
        }

        public async Task<bool> PostMessageAsync(string userId, string threadId, string message, string role)
        {
            string messageId = Guid.NewGuid().ToString();
            DateTime now = DateTime.Now;

            ThreadMessage newMessage = new()
            {

                Id = messageId,
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = role,
                Content = message,
                Created = DateTime.Now
            };

            var response = await _container.CreateItemAsync<ThreadMessage>(newMessage, new PartitionKey(userId));
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception("Failed to create a new thread.");
            }
            return true;
        }

    }
}