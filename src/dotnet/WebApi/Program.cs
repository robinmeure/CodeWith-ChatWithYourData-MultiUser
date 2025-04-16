using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Infrastructure.Helpers;
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
using Infrastructure.Interfaces;
using Microsoft.Identity.Client;
using Infrastructure.Implementations.KernelMemory;
using Infrastructure.Implementations.SPE;
using Infrastructure.Implementations.Cosmos;
using Infrastructure.Implementations.SemanticKernel;
using Infrastructure.Implementations.AISearch;
using Infrastructure.Implementations.Blob;
using WebApi.HealthChecks;
using WebApi.Helpers;

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
            // builder.Services.AddScoped<IDocumentStore, SPEDocumentStore>(); // this makes use of SharePoint Embedded
            // builder.Services.AddSingleton<MSGraphService>(); // this is needed to work with SharePoint Embedded
            // -----------------------------------------------------
            
            // ---------- Azure Blob Implementation ----------------
            // Setting up the Azure Blob Storage client
            builder.Services.AddAzureClients(clientBuilder =>
            {
                var blobConfig = builder.Configuration.GetSection("Storage");
                var serviceUri = new Uri(blobConfig["ServiceUri"]);
                clientBuilder.AddBlobServiceClient(serviceUri).WithCredential(azureCredential);
            });
            builder.Services.AddScoped<IDocumentStore, BlobDocumentStore>(); // thid makes use of Azure Blob Storage
            // -----------------------------------------------------

            // Setting up the Semantic Kernel and AI Search
            builder.AddSemanticKernel(azureCredential);
            builder.AddKernelMemory(azureCredential);

            builder.Services.AddSingleton<ISearchService, AISearchService>();
            builder.Services.AddSingleton<IThreadRepository, CosmosThreadRepository>();
            builder.Services.AddSingleton<IAIService, SemanticKernelService>();
            builder.Services.AddSingleton<IThreadService, ThreadService>();

            builder.Services.AddHttpClient();  //Enable direct http client calls
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

            // Auth
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches();

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