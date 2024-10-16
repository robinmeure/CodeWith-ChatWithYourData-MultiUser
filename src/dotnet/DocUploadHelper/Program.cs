//read the appsettings.json file
using Azure.Identity;
using Azure.Storage.Blobs;
using Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.IO.Enumeration;

var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var configuration = builder.Build();




//create a blob client
var serviceUri = $"https://{configuration["StorageAccountName"]}.blob.core.windows.net";
var blobServiceClient = new BlobServiceClient(new Uri(serviceUri), new AzureCliCredential());
var blobContainerClient = blobServiceClient.GetBlobContainerClient(configuration["StorageContainerName"]);

//create a cosmos client
var cosmosClient = new CosmosClient(configuration["CosmosDbAccountEndpoint"], new AzureCliCredential());
var database = cosmosClient.GetDatabase(configuration["CosmosDBDatabase"]);
var container = database.GetContainer(configuration["CosmosDBContainer"]);



//upload the files in folder Docs
var docsFolder = Directory.GetCurrentDirectory() + "/Docs";
var docs = new DirectoryInfo(docsFolder).GetFiles();
foreach (var doc in docs)
{
    var blobClient = blobContainerClient.GetBlobClient(doc.Name);
    await blobClient.UploadAsync(doc.FullName, true);
    Console.WriteLine($"Uploaded {doc.Name}");

    var docId = Guid.NewGuid().ToString();

       var entry = new DocsPerThread
    {
        Deleted = false,
        DocumentName = doc.Name,
        DocumentUri = blobClient.Uri.ToString(),
        id = docId,
        ThreadId = "1",
        UserId = "test@microsoft.com"

    };

    //upload item to cosmosDb
    await container.UpsertItemAsync(entry, new PartitionKey(entry.id));

   





}

