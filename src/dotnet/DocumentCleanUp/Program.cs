using Azure.Identity;
using Infrastructure;
using Infrastructure.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();

        //read configuration
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

        var config = builder.Build();

        DefaultAzureCredentialOptions azureCredentialOptions =
            DefaultCredentialOptions.GetDefaultAzureCredentialOptions(config["ASPNETCORE_ENVIRONMENT"]);

        var azureCredential = new DefaultAzureCredential(azureCredentialOptions);
        // Add services to the container.
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
            Uri serviceUri = new Uri(config["StorageServiceUri"]);
            clientBuilder.AddBlobServiceClient(serviceUri);
            clientBuilder.UseCredential(azureCredential);
        });

        
        services.AddSingleton(sp =>
        {
            string accountEndpoint = config["CosmosDbConnection__accountEndpoint"];

            // Create and configure CosmosClientOptions
            var cosmosClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                RequestTimeout = TimeSpan.FromSeconds(30)
            };
            return new CosmosClient(accountEndpoint, azureCredential, cosmosClientOptions);
        });
      



        services.AddScoped<IDocumentStore, BlobDocumentStore>();
        services.AddScoped<IDocumentRegistry, CosmosDocumentRegistry>();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
