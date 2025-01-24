using Azure.Identity;
using Microsoft.SemanticKernel;

namespace WebApi.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        public static void AddSemanticKernel(this WebApplicationBuilder builder, DefaultAzureCredential azureCredential, HttpClient? httpClient)
        {
            var searchConfig = builder.Configuration.GetSection("Search");
            var openAIConfig = builder.Configuration.GetSection("OpenAI");

            if (searchConfig != null && openAIConfig != null)
            {
                Uri serviceUri = new Uri(searchConfig["EndPoint"]);
                string? indexName = searchConfig["IndexName"];
                string? endpoint = openAIConfig["EndPoint"];
                string? completionModel = openAIConfig["CompletionModel"];
                string? embeddingModel = openAIConfig["EmbeddingModel"];
                string? apiKey = openAIConfig["ApiKey"];


                var kernelBuilder = Kernel.CreateBuilder();
                if (apiKey == null)
                    kernelBuilder.AddAzureOpenAIChatCompletion(completionModel, endpoint, azureCredential, httpClient:httpClient);
                else
                    kernelBuilder.AddAzureOpenAIChatCompletion(completionModel, endpoint, apiKey,httpClient:httpClient);

                var kernel = kernelBuilder.Build();
                builder.Services.AddSingleton(kernel);
            }
        }

    }
}
