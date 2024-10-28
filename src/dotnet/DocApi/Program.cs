using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage;
using Infrastructure.Helpers;
using Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using DocApi.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace DocApi
{
    public class Program
    {
    
    public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            DefaultAzureCredentialOptions azureCredentialOptions =
            DefaultCredentialOptions.GetDefaultAzureCredentialOptions(builder.Environment.EnvironmentName);

            var azureCredential = new DefaultAzureCredential(azureCredentialOptions);
            // Add services to the container.
            builder.Services.AddAzureClients(clientBuilder =>
            {
                // Register clients for each service
                var blobConfig = builder.Configuration.GetSection("Storage");
                Uri serviceUri = new Uri(blobConfig["ServiceUri"]);
                clientBuilder.AddBlobServiceClient(serviceUri);
                clientBuilder.UseCredential(azureCredential);
            });

            
            var cosmosConfig = builder.Configuration.GetSection("Cosmos");
            if (cosmosConfig != null)
            {
                builder.Services.AddSingleton(sp =>
                {
                    string accountEndpoint = cosmosConfig["AccountEndpoint"];
                    string databaseName = cosmosConfig["DatabaseName"];
                    string containerName = cosmosConfig["ContainerName"];

                    // Create and configure CosmosClientOptions
                    var cosmosClientOptions = new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        RequestTimeout = TimeSpan.FromSeconds(30)
                    };
                    var client = new CosmosClient(accountEndpoint, azureCredential, cosmosClientOptions);
                    var database = client.GetDatabase(databaseName);
                    return database.GetContainer(containerName);
                });
            }

            // AI Search.
            var searchConfig = builder.Configuration.GetSection("Search");
            if (searchConfig != null)
            {
                Uri serviceUri = new Uri(searchConfig["EndPoint"]);
                string indexName = searchConfig["IndexName"];

                builder.Services.AddSingleton(sp => new SearchClient(serviceUri, indexName, azureCredential));
                builder.Services.AddSingleton(sp => new SearchIndexClient(serviceUri, azureCredential));
                builder.Services.AddSingleton(sp => new SearchIndexerClient(serviceUri, azureCredential));
            }

            // Semantic kernel.
            var openAIConfig = builder.Configuration.GetSection("OpenAI");
            if(openAIConfig != null)
            {
                string endpoint = openAIConfig["EndPoint"];
                string completionModel = openAIConfig["CompletionModel"];
                string embeddingModel = openAIConfig["EmbeddingModel"];
                Uri serviceUri = new Uri(searchConfig["EndPoint"]);
                
                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddAzureOpenAIChatCompletion(completionModel, endpoint, azureCredential);
                var kernel = kernelBuilder.Build();
                builder.Services.AddSingleton(kernel);
                builder.Services.AddSingleton(new PromptHelper(kernel));

                // Search
                var embedding = new AzureOpenAITextEmbeddingGenerationService(embeddingModel, endpoint, azureCredential);
                var collection = new AzureAISearchVectorStoreRecordCollection<IndexDoc>(new SearchIndexClient(serviceUri, azureCredential), "onyourdata");               
                builder.Services.AddSingleton(new VectorStoreTextSearch<IndexDoc>(collection, embedding));
            }

            builder.Services.AddScoped<IDocumentRegistry, CosmosDocumentRegistry>();
            builder.Services.AddScoped<IDocumentStore, BlobDocumentStore>();
            builder.Services.AddScoped<ISearchService, AISearchService>();
            builder.Services.AddSingleton<IThreadRepository>(sp =>
            {
                string accountEndpoint = cosmosConfig["AccountEndpoint"];
                string databaseName = cosmosConfig["DatabaseName"];
                string containerName = cosmosConfig["ThreadHistoryContainerName"];

                // Create and configure CosmosClientOptions
                var cosmosClientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    RequestTimeout = TimeSpan.FromSeconds(30)
                };
                var client = new CosmosClient(accountEndpoint, azureCredential, cosmosClientOptions);
                var database = client.GetDatabase(databaseName);
                var container = database.GetContainer(containerName);
                return new CosmosThreadRepository(container);
            });
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // CORS.
            builder.Services.AddCors(options =>
            {
            options.AddPolicy("AllowLocalhost8000",
                builder => builder.WithOrigins("http://localhost:8000")
                                  .AllowAnyHeader()
                                  .AllowAnyMethod()
                                  .AllowCredentials());
            });

            // Auth
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));


            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("AllowLocalhost8000");

            app.UseAuthentication();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}