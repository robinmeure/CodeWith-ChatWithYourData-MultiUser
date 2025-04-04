﻿using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class SemanticKernelService : IAIService
    {
        private readonly Kernel _kernel;
        private readonly AzureOpenAIChatCompletionService _azureOpenAIChatCompletionService; //o1-3 model
        private IChatCompletionService _chatCompletionService; //gpt4 model
        private readonly ILogger<SemanticKernelService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Settings _settings;

        public SemanticKernelService(
            Kernel kernel,
            IConfiguration configuration,
            ILogger<SemanticKernelService> logger,
            AzureOpenAIChatCompletionService azureOpenAIChatCompletionService,
            Settings settings)
        {
            _kernel = kernel;
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            _azureOpenAIChatCompletionService = azureOpenAIChatCompletionService;
            _configuration = configuration;
            _logger = logger;
            _settings = settings;
        }

        public ChatHistory BuildConversationHistory(List<ThreadMessage> messages, string newMessage)
        {
            // Create an empty ChatHistory (assumed to be list-like)
            ChatHistory history = new ChatHistory();
            if (messages != null)
            {
                foreach (ThreadMessage message in messages)
                {
                    switch (message.Role)
                    {
                        case "user":
                            history.AddUserMessage(message.Content);
                            break;
                        case "assistant":
                            history.AddAssistantMessage(message.Content);
                            break;
                        case "system":
                            history.AddSystemMessage(message.Content);
                            break;
                    }
                }
            }
            history.AddUserMessage(newMessage);
            return history;
        }

        public async Task<string[]> GenerateFollowUpQuestionsAsync(ChatHistory history, string assistantResponse, string question)
        {

            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                ModelId = "gpt-4o",
                ResponseFormat = typeof(FollowUpResponse),
                Temperature = _settings.Temperature,
                Seed = _settings.Seed,
            };

            // Adding prompt for follow-up questions
            history.AddUserMessage($@"
                        # Answer
                        {assistantResponse}
                        
                        # Instruction  
                        {Prompts.GPT4Prompts.FollowUpPrompt}
                    ");

            var chatResponse = await _chatCompletionService.GetChatMessageContentsAsync(
                                   executionSettings: executionSettings,
                                   chatHistory: history,
                                   kernel: _kernel);

            string processedResponse = ProcessChatResponse(chatResponse);
            var followUp = JsonSerializer.Deserialize<FollowUpResponse>(processedResponse);
            return followUp?.FollowUpQuestions ?? Array.Empty<string>();
        }

        public async Task<string> RewriteQueryAsync(ChatHistory history)
        {
            string rewritePrompt = Prompts.GPT4Prompts.RewritePrompt;
            history.AddSystemMessage(rewritePrompt);
            var rewrittenResponse = await _chatCompletionService.GetChatMessageContentsAsync(
                chatHistory: history,
                kernel: _kernel);
            
            // Remove the temporary system prompt
            if (history.Count > 0)
                history.RemoveAt(history.Count - 1);
            return rewrittenResponse.FirstOrDefault()?.Content ?? string.Empty;
        }

        public ChatHistory AugmentHistoryWithSearchResults(ChatHistory history, List<IndexDoc> searchResults)
        {
            StringBuilder documents = new StringBuilder();
            foreach (IndexDoc doc in searchResults)
            {
                string[] parts = doc.ChunkId.Split("_pages_");
                string pageNumber = parts.Length > 1 ? parts[1] : "N/A";
                documents.AppendLine($"PageNumber: {pageNumber}");
                documents.AppendLine($"Document ID: {doc.DocumentId}");
                documents.AppendLine($"File Name: {doc.FileName}");
                documents.AppendLine($"Content: {doc.Content}");
                documents.AppendLine();
                documents.AppendLine("------");
                documents.AppendLine();
            }

            string systemPrompt = $@"
            Analyze the following documents to answer the user's question:
            -------    
            {documents}
            ";
            history.AddUserMessage(systemPrompt);
            return history;
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetChatCompletionStreaming(ChatHistory history)
        {
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = _settings.Temperature,
                Seed = _settings.Seed,
            };
            var streamingAnswer = _chatCompletionService.GetStreamingChatMessageContentsAsync(history, executionSettings, _kernel);

            await foreach (var chunk in streamingAnswer)
            {
                // Centralized logging for token usage if available.
                if (chunk.InnerContent is OpenAI.Chat.ChatCompletion chatCompletion)
                {
                    _logger.LogInformation("Streaming chunk --- Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                        chatCompletion.Usage.InputTokenCount, chatCompletion.Usage.OutputTokenCount);
                }
                yield return chunk;
            }
        }

        public async Task<AnswerAndThougthsResponse> GetChatCompletion(ChatHistory history)
        {
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = _settings.Temperature,
                Seed = _settings.Seed,
            };

            IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent> chatResponse = new List<Microsoft.SemanticKernel.ChatMessageContent>();
            try
            {
                chatResponse = await _azureOpenAIChatCompletionService.GetChatMessageContentsAsync(
                                    executionSettings: executionSettings,
                                    chatHistory: history,
                                    kernel: _kernel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting document data.");
            }

            string assistantResponse = ProcessChatResponse(chatResponse);
            if (string.IsNullOrEmpty(assistantResponse))
                return null;

            var response = new AnswerAndThougthsResponse
            {
                Answer = assistantResponse,
                Thoughts = "No thoughts available.",
                References = Array.Empty<string>()
            };
            
            return response;
        }

        /// <summary>
        /// Processes chat message contents by accumulating the text and logging token usage.
        /// </summary>
        /// <param name="chatContents">The collection of chat message contents.</param>
        /// <returns>The combined text output.</returns>
        private string ProcessChatResponse(IEnumerable<Microsoft.SemanticKernel.ChatMessageContent> chatContents)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var chunk in chatContents)
            {
                if (chunk.InnerContent is OpenAI.Chat.ChatCompletion chatCompletion)
                {
                    _logger.LogInformation("Processed chunk --- Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                        chatCompletion.Usage.InputTokenCount, chatCompletion.Usage.OutputTokenCount);
                }
                builder.Append(chunk.Content);
            }
            return builder.ToString();
        }
    }
}