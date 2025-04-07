using Azure;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace WebApi.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        public static void AddSemanticKernel(this WebApplicationBuilder builder, DefaultAzureCredential azureCredential)
        {
            var openAIConfig = builder.Configuration.GetSection("OpenAI");

            if (openAIConfig != null)
            {
                string? endpoint = openAIConfig["EndPoint"];
                string? completionModel = openAIConfig["CompletionModel"];
                string? reasoningModel = openAIConfig["ReasoningModel"];
                string? embeddingModel = openAIConfig["EmbeddingModel"];
                string? apiKey = openAIConfig["ApiKey"];


                var kernelBuilder = Kernel.CreateBuilder();
                if (apiKey == null)
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(completionModel, endpoint, azureCredential, serviceId:"completion");
                    kernelBuilder.AddAzureOpenAIChatCompletion(reasoningModel, endpoint, azureCredential, serviceId: "reasoning");
                }
                else
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(completionModel, endpoint, apiKey, serviceId: "completion");
                    kernelBuilder.AddAzureOpenAIChatCompletion(reasoningModel, endpoint, apiKey, serviceId: "reasoning");
                }

                var kernel = kernelBuilder.Build();
                builder.Services.AddSingleton(kernel);
            }
        }

    }
}
