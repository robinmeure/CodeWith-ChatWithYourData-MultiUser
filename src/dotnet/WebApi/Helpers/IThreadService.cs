using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi.Helpers
{
    public interface IThreadService
    {
        Task<ThreadMessage> HandleUserMessageAsync(string userId, string threadId, string message, CancellationToken cancellationToken);
        Task<ChatHistory> BuildConversationHistoryAsync(string userId, string threadId, string message, CancellationToken cancellationToken);
        Task<string> RewriteQuestionAsync(ChatHistory history, CancellationToken cancellationToken);
        Task<List<IndexDoc>> PerformSearchAsync(ChatHistory history, string query, string threadId, CancellationToken cancellationToken);
        Task<ThreadMessage> GenerateAndSaveAssistantResponseAsync(string userId, string threadId, ChatHistory history, List<IndexDoc> searchResults, CancellationToken cancellationToken);
    }
}
