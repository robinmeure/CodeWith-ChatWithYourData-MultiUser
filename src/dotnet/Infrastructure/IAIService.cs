using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    public interface IAIService
    {
        ChatHistory BuildConversationHistory(List<ThreadMessage> messages, string newMessage);
        Task<string[]> GenerateFollowUpQuestionsAsync(ChatHistory history, string assistantResponse, string question);
        Task<string> RewriteQueryAsync(ChatHistory history);
        ChatHistory AugmentHistoryWithSearchResults(ChatHistory history, List<IndexDoc> searchResults);
        Task<AnswerAndThougthsResponse> GetChatCompletion(ChatHistory history);
    }
}
