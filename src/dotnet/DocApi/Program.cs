using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Infrastructure.Helpers;
using Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Http.Features;
using WebApi.Extensions;
using Domain.Chat;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

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

            // The following line enables Azure Monitor Distro.
            builder.Services.AddOpenTelemetry().UseAzureMonitor();

            // Setting up the Azure Blob Storage client
            builder.Services.AddAzureClients(clientBuilder =>
            {
                var blobConfig = builder.Configuration.GetSection("Storage");
                var serviceUri = new Uri(blobConfig["ServiceUri"]);
                clientBuilder.AddBlobServiceClient(serviceUri).WithCredential(azureCredential);
            });

            // Setting up the Cosmosdb client
            builder.Services.AddSingleton(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var accountEndpoint = configuration["Cosmos:cvbc"];
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

            // Setting up the Semantic Kernel and AI Search
            builder.AddSemanticKernel(azureCredential);

            // Setting up the interfaces and implentations to be used in the controllers
            builder.Services.AddSingleton<IDocumentRegistry, CosmosDocumentRegistry>();
            builder.Services.AddSingleton<IDocumentStore, BlobDocumentStore>();
            builder.Services.AddSingleton<ISearchService, AISearchService>();
            builder.Services.AddSingleton<IThreadRepository, CosmosThreadRepository>();
            builder.Services.AddSingleton<IAIService, SemanticKernelService>();

            // Add health checks
            builder.Services.AddHealthChecks()
                .AddCheck<CosmosHealthCheck>("cosmos_health_check", tags: new[] { "ready" })
                .AddCheck<SearchServiceHealthCheck>("search_service_health_check", tags: new[] { "ready" })
                .AddCheck<AIServiceHealthCheck>("ai_service_health_check", tags: new[] { "ready" });

            Settings settings = new Settings();
            settings.AllowInitialPromptRewrite = false;
            settings.AllowFollowUpPrompts = true;
            settings.UseSemanticRanker = false;
            settings.AllowInitialPromptToHelpUser = true;
            settings.PredefinedPrompts = new List<PredefinedPrompt>()
            {
                new PredefinedPrompt()
                {
                    Id = "1",
                    Name = "Default",
                    Prompt = "You are a helpful assistant."
                },
                new PredefinedPrompt()
                {
                    Id = "2",
                    Name = "Default with Semantic Ranker",
                    Prompt = "You are a helpful assistant. Use semantic ranker to find the most relevant document."
                }
            };
            settings.Seed = 0;
            settings.Temperature = 1.0;

            builder.Services.AddSingleton(settings);

            // file upload limit -- need to work on this, still limited to 30MB
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Auth
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

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

    // Health check implementations
    public class CosmosHealthCheck : IHealthCheck
    {
        private readonly CosmosClient _cosmosClient;
        private readonly IConfiguration _configuration;

        public CosmosHealthCheck(CosmosClient cosmosClient, IConfiguration configuration)
        {
            _cosmosClient = cosmosClient;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var databaseName = _configuration["Cosmos:DatabaseName"];
                var database = _cosmosClient.GetDatabase(databaseName);
                await database.ReadAsync(cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy("Cosmos DB connection is healthy.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Cosmos DB connection is unhealthy.", ex);
            }
        }
    }

    public class SearchServiceHealthCheck : IHealthCheck
    {
        private readonly ISearchService _searchService;

        public SearchServiceHealthCheck(ISearchService searchService)
        {
            _searchService = searchService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Call a lightweight operation to verify search service is working
                await _searchService.IsHealthyAsync();
                return HealthCheckResult.Healthy("Search service is healthy.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Search service is unhealthy.", ex);
            }
        }
    }

    public class AIServiceHealthCheck : IHealthCheck
    {
        private readonly IAIService _aiService;

        public AIServiceHealthCheck(IAIService aiService)
        {
            _aiService = aiService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Call a lightweight operation to verify AI service is working
                await _aiService.IsHealthyAsync();
                return HealthCheckResult.Healthy("AI service is healthy.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("AI service is unhealthy.", ex);
            }
        }
    }
}