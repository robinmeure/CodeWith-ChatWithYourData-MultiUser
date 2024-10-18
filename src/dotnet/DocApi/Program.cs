
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage;
using Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using System.Runtime.CompilerServices;

namespace DocApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAzureClients(clientBuilder =>
            {
#if DEBUG
                // Register clients for each service
                var blobConfig = builder.Configuration.GetSection("Storage");
                string accountName = blobConfig["AccountName"];
                Uri serviceUri = new Uri(blobConfig["ServiceUri"]);
                clientBuilder.AddBlobServiceClient(serviceUri, new StorageSharedKeyCredential(accountName, blobConfig["key"]));
#endif
#if !DEBUG
                clientBuilder.AddBlobServiceClient(builder.Configuration.GetSection("Storage"));
#endif
                clientBuilder.UseCredential(new DefaultAzureCredential());
            });
            var cosmosConfig = builder.Configuration.GetSection("Cosmos");
            if (cosmosConfig != null)
            {
                builder.Services.AddSingleton<CosmosClient>(sp =>
                {
                    string accountEndpoint = cosmosConfig["AccountEndpoint"];
                    string accountKey = cosmosConfig["AccountKey"];

                    // Create and configure CosmosClientOptions
                    var cosmosClientOptions = new CosmosClientOptions
                    {
                        // Example: Customize the connection mode
                        ConnectionMode = ConnectionMode.Direct,
                        // Example: Customize the request timeout
                        RequestTimeout = TimeSpan.FromSeconds(30)
                        // Add more options as needed
                    };
                    CosmosClient client = new CosmosClient(accountEndpoint, accountKey);   
                    return client;
                });
            }
            var searchConfig = builder.Configuration.GetSection("Search");
            if (searchConfig != null)
            { 
                builder.Services.AddSingleton<SearchClient>(sp =>
                {
                    string searchServiceName = searchConfig["ServiceName"];
                    string indexName = searchConfig["IndexName"];
                    string apiKey = searchConfig["ApiKey"];
                    Uri serviceUri = new Uri(searchConfig["EndPoint"]);
                    return new SearchClient(serviceUri, indexName, new AzureKeyCredential(apiKey));
                });
                builder.Services.AddSingleton<SearchIndexClient>(sp =>
                {
                    string searchServiceName = searchConfig["ServiceName"];
                    string apiKey = searchConfig["ApiKey"];
                    Uri serviceUri = new Uri(searchConfig["EndPoint"]);
                    return new SearchIndexClient(serviceUri, new AzureKeyCredential(apiKey));
                });
            }

            builder.Services.AddScoped<CosmosDocumentRegistry>();
            builder.Services.AddScoped<BlobDocumentStore>();
            builder.Services.AddScoped<AISearchService>();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
