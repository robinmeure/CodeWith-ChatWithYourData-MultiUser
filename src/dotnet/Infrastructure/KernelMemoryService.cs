using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    internal class KernelMemoryService : IAIService
    {
        ChatHistory IAIService.AugmentHistoryWithSearchResults(ChatHistory history, List<IndexDoc> searchResults)
        {
            throw new NotImplementedException();
        }

        ChatHistory IAIService.BuildConversationHistory(List<ThreadMessage> messages, string newMessage)
        {
            throw new NotImplementedException();
        }

        Task<string[]> IAIService.GenerateFollowUpQuestionsAsync(ChatHistory history, string assistantResponse, string question)
        {
            throw new NotImplementedException();
        }

        Task<AnswerAndThougthsResponse> IAIService.GetChatCompletion(ChatHistory history)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<StreamingChatMessageContent> IAIService.GetChatCompletionStreaming(ChatHistory history)
        {
            throw new NotImplementedException();
        }

        Task<string> IAIService.RewriteQueryAsync(ChatHistory history)
        {
            throw new NotImplementedException();
        }
    }
}
