using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WebApi.Helpers;
using Infrastructure.Interfaces;
using Microsoft.SemanticKernel.ChatCompletion;
using Infrastructure.Helpers;

namespace WebApi.Helpers
{
    public class ThreadService : IThreadService
    {
        private readonly IThreadRepository _threadRepository;
        private readonly IAIService _aiService;
        private readonly ISearchService _searchService;
        private readonly ThreadSafeSettings _settings;
        private readonly ILogger<ThreadService> _logger;
        private readonly IMemoryCache _memoryCache;
        // Optionally, inject a distributed cache for hybrid caching

        public ThreadService(
            IThreadRepository threadRepository,
            IAIService aiService,
            ISearchService searchService,
            ThreadSafeSettings settings,
            ILogger<ThreadService> logger,
            IMemoryCache memoryCache)
        {
            _threadRepository = threadRepository;
            _aiService = aiService;
            _searchService = searchService;
            _settings = settings;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<ThreadMessage> HandleUserMessageAsync(string userId, string threadId, string message, CancellationToken cancellationToken)
        {
            var question = await CreateAndSaveUserMessage(userId, threadId, message, cancellationToken);
            var history = await BuildConversationHistoryAsync(userId, threadId, message, cancellationToken);
            string query = (_settings.GetSettings().AllowInitialPromptRewrite)
                ? await RewriteQuestionAsync(history, cancellationToken)
                : message;
            var searchResults = await PerformSearchAsync(history, query, threadId, cancellationToken);
            var answer = await GenerateAndSaveAssistantResponseAsync(userId, threadId, history, searchResults, cancellationToken);
            return answer;
        }

        public async Task<ChatHistory> BuildConversationHistoryAsync(string userId, string threadId, string message, CancellationToken cancellationToken)
        {
            string cacheKey = $"history:{userId}:{threadId}:{message}";
            var messages = await _threadRepository.GetMessagesAsync(userId, threadId, cancellationToken);
            var history = _aiService.BuildConversationHistory(messages, message);
            return history;
        }

        public async Task<string> RewriteQuestionAsync(ChatHistory history, CancellationToken cancellationToken)
        {
            string query = await _aiService.RewriteQueryAsync(history);
            _logger.LogInformation("Query rewritten to: {Query}", query);
            return query;
        }

        public async Task<List<IndexDoc>> PerformSearchAsync(ChatHistory history, string query, string threadId, CancellationToken cancellationToken)
        {
            string sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(query, @"[^\w\s]", string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");
            sanitarizedQuery = System.Text.RegularExpressions.Regex.Replace(sanitarizedQuery, @"\s+", " ").Trim();
            var searchResults = await _searchService.GetSearchResultsAsync(sanitarizedQuery, threadId);
            _aiService.AugmentHistoryWithSearchResults(history, searchResults);
            return searchResults;
        }

        public async Task<ThreadMessage> GenerateAndSaveAssistantResponseAsync(string userId, string threadId, ChatHistory history, List<IndexDoc> searchResults, CancellationToken cancellationToken)
        {
            var assistantAnswer = await _aiService.GetChatCompletion(history);
            if (assistantAnswer == null)
            {
                throw new ServiceException("Failed to generate assistant response", ServiceType.AIService);
            }
            var followUpQuestionList = _settings.GetSettings().AllowFollowUpPrompts
                ? await _aiService.GenerateFollowUpQuestionsAsync(history, assistantAnswer.Answer, assistantAnswer.Answer)
                : Array.Empty<string>();
            var thoughts = new List<Thoughts>();
            if (!string.IsNullOrEmpty(assistantAnswer.Thoughts))
            {
                thoughts.Add(new Thoughts("Answer", assistantAnswer.Thoughts));
            }
            var answer = new ThreadMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = "assistant",
                Content = assistantAnswer.Answer,
                Context = new ResponseContext(
                    FollowupQuestions: followUpQuestionList,
                    DataPointsContent: null,
                    Thoughts: thoughts.ToArray()),
                Created = DateTime.UtcNow
            };
            await _threadRepository.PostMessageAsync(userId, answer, cancellationToken);
            return answer;
        }

        private async Task<ThreadMessage> CreateAndSaveUserMessage(string userId, string threadId, string message, CancellationToken cancellationToken)
        {
            var question = new ThreadMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = "CHAT_MESSAGE",
                ThreadId = threadId,
                UserId = userId,
                Role = "user",
                Content = message,
                Context = null,
                Created = DateTime.UtcNow
            };
            await _threadRepository.PostMessageAsync(userId, question, cancellationToken);
            return question;
        }
    }
}
