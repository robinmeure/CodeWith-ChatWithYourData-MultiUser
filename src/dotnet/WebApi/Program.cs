using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Infrastructure.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Http.Features;
using WebApi.Extensions;
using Domain.Chat;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Infrastructure.Interfaces;
using Infrastructure.Implementations.SPE;
using Infrastructure.Implementations.Cosmos;
using Infrastructure.Implementations.SemanticKernel;
using Infrastructure.Implementations.AISearch;
using WebApi.HealthChecks;
using Microsoft.SemanticKernel;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Azure.Core.Pipeline;
using Azure.Core;

using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi
{
    public class Program
    {
    
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // setting the managed identity for the app (locally this reverts back to the VS/VSCode/AZCli credentials)
            DefaultAzureCredentialOptions azureCredentialOptions = DefaultCredentialOptions.GetDefaultAzureCredentialOptions(builder.Environment.EnvironmentName);

            var azureCredential = new DefaultAzureCredential(azureCredentialOptions);

            builder.AddServiceDefaults();
            //builder.AddSemanticKernelLogging();

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10),
            };

            // Add HTTP client configuration with extended timeout
            builder.Services.AddHttpClient("LongTimeout", httpClient =>
            {
            });
          
            // Auth
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddMicrosoftGraph(builder.Configuration.GetSection("DownstreamApi"))
                .AddInMemoryTokenCaches();

            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
            // Setting up the Cosmosdb client
            builder.Services.AddSingleton(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var accountEndpoint = configuration["Cosmos:AccountEndpoint"];
                var cosmosClientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    RequestTimeout = TimeSpan.FromSeconds(30)
                };
                return new CosmosClient(accountEndpoint, azureCredential, cosmosClientOptions);
            });

            //Setting up the Search client
            builder.Services.AddSingleton(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var serviceUri = new Uri(configuration["Search:EndPoint"]);
                return new SearchIndexClient(serviceUri, azureCredential);
            });
            
            // Setting up the interfaces and implentations to be used in the controllers
            builder.Services.AddSingleton<IDocumentRegistry, CosmosDocumentRegistry>();

            // ---------- SharePoint Embedded Implementation ----------------
            builder.Services.AddScoped<MSGraphService>(); // this is needed to work with SharePoint Embedded
            builder.Services.AddScoped<IDocumentStore, SPEDocumentStore>(); // this makes use of SharePoint Embedded
            
            // -----------------------------------------------------

            // ---------- Azure Blob Implementation ----------------
            // Setting up the Azure Blob Storage client
            //builder.Services.AddAzureClients(clientBuilder =>
            //{
            //    var blobConfig = builder.Configuration.GetSection("Storage");
            //    var serviceUri = new Uri(blobConfig["ServiceUri"]);
            //    clientBuilder.AddBlobServiceClient(serviceUri).WithCredential(azureCredential);
            //});
            //builder.Services.AddScoped<IDocumentStore, BlobDocumentStore>(); // thid makes use of Azure Blob Storage
            // -----------------------------------------------------

            // Setting up the Semantic Kernel and AI Search
            string? endpoint = builder.Configuration["OpenAI:EndPoint"];
            string? completionModel = builder.Configuration["OpenAI:CompletionModel"];
            string? reasoningModel = builder.Configuration["OpenAI:ReasoningModel"];
            string? embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"];

            // ----------  Azure OpenAI with Semantic Kernel setup ----------------
            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(endpoint:endpoint, deploymentName:completionModel, credentials:azureCredential, serviceId: "completion")
                .AddAzureOpenAIChatCompletion(endpoint:endpoint, deploymentName:reasoningModel, credentials:azureCredential, serviceId: "reasoning")
                .Build();

            builder.Services.AddSingleton(kernel);
            builder.AddKernelMemory(azureCredential);            
            builder.Services.AddSingleton<ISearchService, AISearchService>();
            builder.Services.AddSingleton<IThreadRepository, CosmosThreadRepository>();
            builder.Services.AddSingleton<IAIService, SemanticKernelService>();

            // Add health checks
            builder.Services.AddHealthChecks()
                .AddCheck<CosmosHealthCheck>("cosmos_health_check", tags: new[] { "ready" })
                .AddCheck<SearchServiceHealthCheck>("search_service_health_check", tags: new[] { "ready" })
                .AddCheck<AIServiceHealthCheck>("ai_service_health_check", tags: new[] { "ready" });

            Settings initialSettings = builder.Configuration.GetSection("Settings").Get<Settings>();
            if (initialSettings == null)
            {
                throw new ArgumentNullException("Settings section is missing in appsettings.json");
            }
            builder.Services.AddSingleton<ThreadSafeSettings>(new ThreadSafeSettings(initialSettings));

            

            // file upload limit -- need to work on this, still limited to 30MB
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();

            // CORS.
            app.UseCors(builder => builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed((host) => true)
                .AllowCredentials()
            );

            app.UseAuthentication();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            // Configure health check endpoint with detailed response
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    var result = JsonSerializer.Serialize(
                        new
                        {
                            status = report.Status.ToString(),
                            components = report.Entries.Select(e => new
                            {
                                component = e.Key,
                                status = e.Value.Status.ToString(),
                                description = e.Value.Description,
                                error = e.Value.Exception?.Message
                            })
                        },
                        new JsonSerializerOptions { WriteIndented = true });

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(result);
                }
            });

            app.Run();
        }
    }
}