using Domain.Cosmos;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thread = Domain.Cosmos.Thread;
namespace Infrastructure;
public interface IThreadRepository
{
    Task<List<Domain.Cosmos.Thread>> GetThreadsAsync(string userId);
    Task<List<Domain.Cosmos.Thread>> GetSoftDeletedThreadAsync(string threadId);
    Task<Domain.Cosmos.Thread> CreateThreadAsync(string userId);
    Task<bool> DeleteThreadAsync(string userId, string threadId);
    Task<List<ThreadMessage>> GetMessagesAsync(string userId, string threadId);
    Task<bool> PostMessageAsync(string userId, string threadId, string message, string role);
    Task<bool> PostMessageAsync(string userId, ThreadMessage message);
    Task<List<ThreadMessage>> GetAllThreads(DateTime expirationDate);
    Task<List<Thread>> GetAllThreads();
    Task<List<string>> GetAllThreadIds(DateTime expirationDate);
    Task<bool> MarkThreadAsDeletedAsync(string userId, string threadId);
    Task<bool> DeleteMessages(string userId, string threadId);
    Task<bool> UpdateThreadFieldsAsync(string threadId, string userId, Dictionary<string, object> fieldsToUpdate);
    Task<Thread> GetThreadAsync(string userId, string threadId);
}