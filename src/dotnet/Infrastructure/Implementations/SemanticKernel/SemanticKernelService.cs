using Domain.Chat;
using Domain.Cosmos;
using Domain.Search;
using Infrastructure.Helpers;
using Infrastructure.Implementations.SemanticKernel.Tools;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infrastructure.Implementations.SemanticKernel.Agents;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using static Domain.Chat.Enums;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Newtonsoft.Json.Linq;

namespace Infrastructure.Implementations.SemanticKernel
{
    public class SemanticKernelService : IAIService
    {
        private readonly Kernel _kernel;
        private IChatCompletionService _chatCompletionService;
        private IChatCompletionService? _reasoningCompletionService;
        private IServiceProvider _serviceProvider;

        private readonly ILogger<SemanticKernelService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ThreadSafeSettings _settings;

        // Rate limit regex pattern to extract the retry time
        private static readonly Regex _rateLimitRegex = new Regex(@"Try again in (\d+) seconds", RegexOptions.Compiled);

      

        public SemanticKernelService(
            Kernel kernel,
            IConfiguration configuration,
            ILogger<SemanticKernelService> logger,
            IServiceProvider serviceProvider,
            ThreadSafeSettings settings)
        {
            _kernel = kernel;
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>("completion");
            _reasoningCompletionService = _kernel.GetRequiredService<IChatCompletionService>("reasoning");
            _serviceProvider = serviceProvider;
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


            var executionSettings = new AzureAIInferencePromptExecutionSettings()
            {
                //ResponseFormat = typeof(FollowUpResponse),
                
              //  Temperature = float.Parse(_settings.GetSettings().Temperature),
                Seed = _settings.GetSettings().Seed,
                ServiceId = "completion"
            };

            // Adding prompt for follow-up questions
            history.AddUserMessage($@"
                        # Answer
                        {assistantResponse}
                        
                        # Instruction  
                        {Prompts.GPT4Prompts.FollowUpPrompt}
                    ");

            var completionResponse = await ExecuteChatCompletionWithRetryAsync(
                _chatCompletionService,
                history,
                executionSettings);

            if (!completionResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to generate follow-up questions: {Message}", completionResponse.Error.Message);
                return Array.Empty<string>();
            }

            try
            {
                var followUp = JsonSerializer.Deserialize<List<string>>(completionResponse.Content);
                if (followUp == null)
                {
                    _logger.LogWarning("Failed to deserialize follow-up questions response");
                    return Array.Empty<string>();
                }
                string[] followUpQuestions = followUp
                    .Select(q => q.ToString())
                    .ToArray();

                return followUpQuestions;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize follow-up questions response");
                return Array.Empty<string>();
            }
        }

        public async Task<string> RewriteQueryAsync(ChatHistory history)
        {
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = _settings.GetSettings().Temperature,
                Seed = _settings.GetSettings().Seed,
                ServiceId = "completion"
            };

            string rewritePrompt = Prompts.GPT4Prompts.RewritePrompt;
            history.AddSystemMessage(rewritePrompt);
            
            var completionResponse = await ExecuteChatCompletionWithRetryAsync(
                _chatCompletionService,
                history,
                executionSettings);

            // Remove the temporary system prompt
            if (history.Count > 0)
                history.RemoveAt(history.Count - 1);
                
            if (!completionResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to rewrite query: {Message}", completionResponse.Error.Message);
                return string.Empty;
            }

            return completionResponse.Content;
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
                Temperature = _settings.GetSettings().Temperature,
                Seed = _settings.GetSettings().Seed,
                ServiceId = "completion",
            };

            IAsyncEnumerable<StreamingChatMessageContent> streamingAnswer = null;

            try
            {
                streamingAnswer = _chatCompletionService.GetStreamingChatMessageContentsAsync(history, executionSettings, _kernel);
            }
            catch (HttpOperationException ex)
            {
                _logger.LogError(ex, "Error during streaming chat completion");

                // Parse rate limit errors
                var rateLimitInfo = ParseRateLimitError(ex);
                if (rateLimitInfo != null)
                {
                    _logger.LogWarning("Rate limit exceeded. Try again in {Seconds} seconds", rateLimitInfo.RetryAfterSeconds);
                }

                // Re-throw to let the caller handle the error
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during streaming chat completion");
                throw;
            }

            if (streamingAnswer != null)
            {
                await foreach (var chunk in streamingAnswer)
                {
                    // Centralized logging for token usage if available.
                    if (chunk.InnerContent is StreamingChatCompletionUpdate chatCompletion)
                    {
                        if (chatCompletion.Usage != null)
                        {

                            _logger.LogInformation("Streaming chunk --- Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                                chatCompletion.Usage.InputTokenCount, chatCompletion.Usage.OutputTokenCount);
                        }
                    }
                    yield return chunk;
                }
            }
        }

        public async Task<CompletionResponse> GetChatCompletion(ChatHistory history, CompletionType completionType)
        {
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = _settings.GetSettings().Temperature,
                Seed = _settings.GetSettings().Seed,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            CompletionResponse completionResponse = new CompletionResponse();

            switch (completionType)
            {
                case CompletionType.Chat:
                    completionResponse = await ExecuteChatCompletionWithRetryAsync(
                                               _chatCompletionService,
                                               history,
                                               executionSettings);
                    break;
                case CompletionType.Reasoning:
                    completionResponse = await ExecuteChatCompletionWithRetryAsync(
                                               _reasoningCompletionService,
                                               history,
                                               executionSettings);
                    break;
                default:
                    break;
            }

            if (!completionResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to get chat completion: {Message}", completionResponse.Error.Message);
                return completionResponse;
            }
            
            // Add usage metrics to the response as metadata if needed
            if (completionResponse.Usage != null)
            {
                _logger.LogInformation("Chat completion metrics --- Input tokens: {InputTokens}, Output tokens: {OutputTokens}, Total: {TotalTokens}",
                    completionResponse.Usage.InputTokens,
                    completionResponse.Usage.OutputTokens,
                    completionResponse.Usage.TotalTokens);
            }
            
            return completionResponse;
        }

        public async Task<string> ExtractDocument(List<IndexDoc> searchResults)
        {
            StringBuilder batchContent = new StringBuilder();
            batchContent.AppendLine("-------DOCUMENTS------");
            foreach (IndexDoc doc in searchResults)
            {
                string[] parts = doc.ChunkId.Split("_");
                string pageNumber = parts.Length > 1 ? parts[1] : "N/A";
                batchContent.AppendLine($"PageNumber: {pageNumber}");
                batchContent.AppendLine($"Document ID: {doc.DocumentId}");
                batchContent.AppendLine($"File Name: {doc.FileName}");
                batchContent.AppendLine($"Content: {doc.Content}");
                batchContent.AppendLine("------");
                batchContent.AppendLine();
            }

            ChatHistory history = new ChatHistory();
            history.AddSystemMessage("You are an document extractor expert. " +
                "Your job is to extract requirements from the document. " +
                "Some of the chunks provided can overlap each other, keep that in mind. " +
                "Please make sure you extract all the requirements and categorize them. " +
                "The output must be in markdown table format.");
            history.AddUserMessage(batchContent.ToString());

            var response = await GetChatCompletion(history, CompletionType.Chat);
            return response.Content;
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetCompliancyResponseStreamingViaCompletionAsync(string threadId, string extractedText, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string instruction = $"""
                Formatting re-enabled.
                You are the orchestrator tasked with validating the document compliancy.
    
                Your tasks:
                1. Process all the requirements for a given document
                2. Retrieve a list of the INCOSE guidelines
                3. Validate each requirement against the INCOSE guidelines (get_incose_rules).
                4. Return a markdown table with each requirement and its validation result.
                DO NOT MODIFY THE REQUIREMENTS, ONLY VALIDATE THEM.
                """;

            string userPrompt = $"""
                Please process the following extracted requirements (one per file):
                {extractedText}
                """;

            if (!_kernel.Plugins.Contains("get_incose_rules"))
            {
                _kernel.Plugins.AddFromType<IncoseGuideLinesTool>("get_incose_rules");
            }
            if (!_kernel.Plugins.Contains("get_incose_template"))
            {
                _kernel.Plugins.AddFromType<IncoseTemplateTool>("get_incose_template");
            }


            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = _settings.GetSettings().Temperature,
                Seed = _settings.GetSettings().Seed,
                ServiceId = "reasoning",
                ChatSystemPrompt = instruction,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            ChatHistory history = [];
            history.AddUserMessage(userPrompt);
            var response = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                chatHistory: history,
                kernel: _kernel,
                executionSettings: executionSettings,
                cancellationToken: cancellationToken
            );

            string lastChunk = string.Empty;
            await foreach (StreamingChatMessageContent chunk in response)
            {
                yield return chunk;
            }
        }

        public async IAsyncEnumerable<AgentChatResponse> GetCompliancyResponseStreamingViaAgentsAsync(string threadId, string extractedText, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Set up agents
            var chat = SetupAgentsAsync(extractedText);

            // Create a channel for both agent responses and heartbeats
            var channel = System.Threading.Channels.Channel.CreateUnbounded<AgentChatResponse>();

            // Start a task to send heartbeats every 5 seconds
            var heartbeatTask = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(5000, cancellationToken);
                        // Use TryWrite so that if the channel is closed, we break out of the loop instead of throwing.
                        if (!channel.Writer.TryWrite(AgentChatResponse.CreateHeartbeat()))
                        {
                            _logger.LogTrace("Channel closed, stopping heartbeat task");
                            break;
                        }
                        _logger.LogTrace("Heartbeat sent at {Time}", DateTime.UtcNow);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected; do nothing.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in heartbeat task");
                }
            }, cancellationToken);

            // Start a task to process agent responses
            var agentTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (StreamingChatMessageContent response in chat.InvokeStreamingAsync().WithCancellation(cancellationToken))
                    {
                        await channel.Writer.WriteAsync(
                            AgentChatResponse.CreateAgentMessage(
                                response.AuthorName,
                                response.Content,
                                false), // Not the final message
                            cancellationToken);
                    }

                    // Get the full chat history to find the last Reviewer message
                    var messages = new List<ChatMessageContent>();
                    await foreach (var message in chat.GetChatMessagesAsync().WithCancellation(cancellationToken))
                    {
                        messages.Add(message);
                    }

                    // Find and send the final message from the Reviewer
                    var finalMessage = messages
                        .Where(m => m.AuthorName == "Orchestrator")
                        .FirstOrDefault();

                    if (finalMessage != null)
                    {
                        await channel.Writer.WriteAsync(
                            AgentChatResponse.CreateAgentMessage(
                                finalMessage.AuthorName,
                                finalMessage.Content,
                                true), // This is the final message
                            cancellationToken);
                    }
                    else
                    {
                        // If no final message from reviewer, send a fallback message
                        await channel.Writer.WriteAsync(
                            AgentChatResponse.CreateAgentMessage(
                                "System",
                                "Processing complete, but no final response was generated.",
                                true), // Final message
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing agent responses");

                    // Send an error message so the client knows something went wrong
                    try
                    {
                        await channel.Writer.WriteAsync(
                            AgentChatResponse.CreateAgentMessage(
                                "System",
                                "An error occurred while processing your request.",
                                true), // Final message
                            CancellationToken.None); // Use None to ensure the error gets sent
                    }
                    catch
                    {
                        // Ignore any errors from sending the error message
                    }
                }
                finally
                {
                    // Complete the channel when done to signal no more messages
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Read all messages from the channel and yield them to the caller
            await foreach (var response in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return response;
            }

            // Clean up the heartbeat task when done
            try
            {
                await heartbeatTask;
            }
            catch
            {
                // Ignore any errors during cleanup
            }
        }

        private AgentGroupChat SetupAgentsAsync(string extractedText)
        {
            Kernel toolKernel = _kernel.Clone();

            toolKernel.Plugins.AddFromType<IncoseGuideLinesTool>();
            toolKernel.Plugins.AddFromType<IncoseTemplateTool>();

            ChatCompletionAgent agentReviewer = new Reviewer().CreateAgent(toolKernel, "Reviewer");
            ChatCompletionAgent agentOrchestrator = new Orchestrator().CreateAgent(toolKernel, "Orchestrator", extractedText);

            // Define a kernel function for the selection strategy
            KernelFunction terminationFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    Determine if the reviewer has approved.  If so, respond with a single word: yes

                    History:
                    {{$history}}
                    """,
                    safeParameterNames: "history");

            // Define the termination strategy
            KernelFunctionTerminationStrategy terminationStrategy =
              new(terminationFunction, _kernel)
              {
                  // Only the reviewer may give approval.
                  Agents = [agentReviewer],
                  // Parse the function response.
                  ResultParser = (result) =>
                    result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
                  // The prompt variable name for the history argument.
                  HistoryVariableName = "history",
                  // Save tokens by not including the entire history in the prompt
                  //HistoryReducer = new ChatHistoryTruncationReducer(1),
                  // Limit total number of turns no matter what
                  MaximumIterations = 10,
              };

            // Create a chat using the defined termination strategy.
            AgentGroupChat chat =
                new(agentOrchestrator, agentReviewer)
                {
                    ExecutionSettings = new() { TerminationStrategy = terminationStrategy }
                };

            return chat;
        }

        public async Task IsHealthyAsync()
        {
            try
            {
                // Test kernel by asking a simple question
                var prompt = "Test health check. Respond with 'OK'";
                var chat = _chatCompletionService;

                var history = new ChatHistory();
                history.AddUserMessage(prompt);
                
                // Execute a minimal request to verify connectivity
                var result = await chat.GetChatMessageContentAsync(history, executionSettings: null);
                if (string.IsNullOrEmpty(result.Content))
                {
                    throw new Exception("AI service returned empty response");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for AI service");
                throw new ServiceException("AI service health check failed", ex, ServiceType.AIService);
            }
        }

        private async Task<CompletionResponse> ExecuteChatCompletionWithRetryAsync(IChatCompletionService service, ChatHistory history,PromptExecutionSettings executionSettings, int maxRetries = 3)
        {
            int retryCount = 0;
            int baseDelayMs = 1000; // Start with 1 second delay

            while (true)
            {
                try
                {
                    var chatResponse = await service.GetChatMessageContentsAsync(
                        executionSettings: executionSettings,
                        chatHistory: history,
                        kernel: _kernel);

                    // Process successful response
                    return ProcessChatResponseToCompletionResponse(chatResponse);
                }
                // Need to determine if we want to handle the 429 in the backend or in the frontend of the application
                // the below catch logic makes sure the 429 is being handled in the backend
                // the other catch makes sure that the 429 is being sent back to the frontend to deal with it

                //catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && retryCount < maxRetries)
                //{
                //    retryCount++;

                //    // Parse rate limit error to get retry-after time
                //    var errorDetails = ParseRateLimitError(ex);
                //    int delaySeconds = errorDetails?.RetryAfterSeconds ?? (int)Math.Pow(2, retryCount) * baseDelayMs / 1000;

                //    _logger.LogWarning("Rate limit exceeded. Retry {RetryCount}/{MaxRetries} after {DelaySeconds} seconds",
                //        retryCount, maxRetries, delaySeconds);

                //    await Task.Delay(delaySeconds * 1000);
                //}
                //catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && retryCount >= maxRetries)
                //{
                //    // Handle max retries exceeded
                //    _logger.LogError(ex, "Max retries exceeded for chat completion");
                //    return new CompletionResponse
                //    {
                //        IsSuccess = false,
                //        Error = new ErrorDetails
                //        {
                //            StatusCode = 429,
                //            Message = "Max retries exceeded for rate limit"
                //        }
                //    };
                //}
                catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests) // sending the 429 back to the frontend
                {
                    // Handle max retries exceeded
                    _logger.LogError(ex, "Max retries exceeded for chat completion");
                    return new CompletionResponse
                    {
                        IsSuccess = false,
                        Error = new ErrorDetails
                        {
                            StatusCode = 429,
                            Message = "Max retries exceeded for rate limit"
                        }
                    };
                }
                catch (HttpRequestException ex)
                {
                    // Handle other HTTP errors
                    _logger.LogError(ex, "HTTP error during chat completion: {StatusCode}", ex.StatusCode);

                    return new CompletionResponse
                    {
                        IsSuccess = false,
                        Error = new ErrorDetails
                        {
                            StatusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError),
                            Message = ex.Message
                        }
                    };
                }
                catch (Exception ex)
                {
                    // Handle unexpected errors
                    _logger.LogError(ex, "Unexpected error during chat completion");

                    return new CompletionResponse
                    {
                        IsSuccess = false,
                        Error = new ErrorDetails
                        {
                            StatusCode = (int) HttpStatusCode.InternalServerError,
                            Message = $"Internal error: {ex.Message}"
                        }
                    };
                }
            }
        }

        /// <summary>
        /// Processes chat message contents into a standardized completion response
        /// </summary>
        private CompletionResponse ProcessChatResponseToCompletionResponse(IEnumerable<Microsoft.SemanticKernel.ChatMessageContent> chatContents)
        {
            var response = new CompletionResponse
            {
                IsSuccess = true,
                Usage = new UsageMetrics()
            };

            StringBuilder contentBuilder = new StringBuilder();
            
            foreach (var chunk in chatContents)
            {
                if (chunk.InnerContent is ChatCompletion chatCompletion)
                {
                    response.Usage.InputTokens = chatCompletion.Usage.InputTokenCount;
                    response.Usage.OutputTokens = chatCompletion.Usage.OutputTokenCount;
                    
                    // Capture any other relevant metadata
                    foreach (var property in chunk.Metadata ?? Enumerable.Empty<KeyValuePair<string, object>>())
                    {
                        if (!response.Metadata.ContainsKey(property.Key))
                        {
                            response.Metadata.Add(property.Key, property.Value);
                        }
                    }
                }
                
                contentBuilder.Append(chunk.Content);
            }
            
            response.Content = contentBuilder.ToString();
            return response;
        }

        /// <summary>
        /// Parses rate limit error messages to extract retry information
        /// </summary>
        private ErrorDetails ParseRateLimitError(HttpOperationException ex)
        {
            try
            {
                // Try to parse JSON error message like: {"error":{"code":"429","message": "Rate limit is exceeded. Try again in 60 seconds."}}
                var errorContent = ex.Message;
                
                // Check if we have a JSON response
                if (errorContent.Contains("{\"error\""))
                {
                    var errorJson = JsonDocument.Parse(errorContent);
                    var errorMessage = errorJson.RootElement
                        .GetProperty("error")
                        .GetProperty("message")
                        .GetString();

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        var match = _rateLimitRegex.Match(errorMessage);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int retrySeconds))
                        {
                            return new ErrorDetails
                            {
                                StatusCode = 429,
                                Message = errorMessage,
                                RetryAfterSeconds = retrySeconds
                            };
                        }
                        
                        // Error message doesn't contain retry time
                        return new ErrorDetails
                        {
                            StatusCode = 429,
                            Message = errorMessage,
                            RetryAfterSeconds = 60 // Default to 60 seconds if not specified
                        };
                    }
                }
                
                // Check for Retry-After header from exception data
                if (ex.Data.Contains("Retry-After") && ex.Data["Retry-After"] is int retryAfter)
                {
                    return new ErrorDetails
                    {
                        StatusCode = 429,
                        Message = "Rate limit exceeded",
                        RetryAfterSeconds = retryAfter
                    };
                }
                
                // Fallback
                return new ErrorDetails
                {
                    StatusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError),
                    Message = ex.Message,
                    RetryAfterSeconds = 60 // Default fallback
                };
            }
            catch (Exception parseEx)
            {
                // If we fail to parse, return a basic error
                return new ErrorDetails
                {
                    StatusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError),
                    Message = ex.Message
                };
            }
        }
    }
}
