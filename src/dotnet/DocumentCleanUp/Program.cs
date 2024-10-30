using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Infrastructure;
using Infrastructure.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using DocumentCleanUp;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()

   .ConfigureServices((hostContext, services) =>
   {

       DefaultAzureCredentialOptions azureCredentialOptions =
             DefaultCredentialOptions.GetDefaultAzureCredentialOptions(hostContext.Configuration["ASPNETCORE_ENVIRONMENT"]);
       var azureCredential = new DefaultAzureCredential(azureCredentialOptions);


       services.AddAzureClients(clientBuilder =>
       {
           // Register clients for each service
           Uri serviceUri = new Uri(hostContext.Configuration["StorageServiceUri"]);
           clientBuilder.AddBlobServiceClient(serviceUri);
           clientBuilder.UseCredential(azureCredential);
       });

       services.AddSingleton(sp =>
       {
           string accountEndpoint = hostContext.Configuration["MyCosmosConnection"];
           string cosmosDBDatabase = hostContext.Configuration["CosmosDbDatabase"];
           string cosmosDBContainer = hostContext.Configuration["CosmosDbDocumentContainer"];

           // Create and configure CosmosClientOptions
           var cosmosClientOptions = new CosmosClientOptions
           {
               ConnectionMode = ConnectionMode.Direct,
               RequestTimeout = TimeSpan.FromSeconds(30)
           };

           var client = new CosmosClient(accountEndpoint, azureCredential, cosmosClientOptions);
           var database = client.GetDatabase(cosmosDBDatabase);
           return database.GetContainer(cosmosDBContainer);
       });
       Uri serviceUri = new Uri(hostContext.Configuration["SearchEndPoint"]);
       string indexName = hostContext.Configuration["SearchIndexName"];

       services.AddSingleton(sp => new SearchClient(serviceUri, indexName, azureCredential));
       services.AddSingleton(sp => new SearchIndexClient(serviceUri, azureCredential));
       services.AddSingleton(sp => new SearchIndexerClient(serviceUri, azureCredential));

       
       services.AddSingleton<ISearchService, AISearchService>();
       services.AddSingleton<IDocumentStore, BlobDocumentStore>();
       services.AddSingleton<IDocumentRegistry, CosmosDocumentRegistry>();
       services.AddSingleton<IThreadRepository>(sp =>
       {
           string accountEndpoint = hostContext.Configuration["MyCosmosConnection"];
           string databaseName = hostContext.Configuration["CosmosDbDatabase"];
           string containerName = hostContext.Configuration["CosmosDbThreadContainer"];

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

       services.AddSingleton<ThreadCleanup>();
       services.AddSingleton<DocumentCleanUpFunction>();
       services.AddSingleton<ThreadCleanUpFunctionFromCosmos>();
       services.AddSingleton<ThreadCleanUpFunctionTimerTrigger>();

   })
   .Build();

host.Run();
