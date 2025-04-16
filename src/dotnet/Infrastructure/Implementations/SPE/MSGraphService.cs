using Microsoft.Azure.Cosmos.Core.Collections;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Graph.Models;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Invite;
using Permission = Microsoft.Graph.Models.Permission;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using System.Text.Json;

namespace Infrastructure.Implementations.SPE
{
    public class Container
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "displayName")]
        public required string DisplayName { get; set; }
        [JsonProperty(PropertyName = "description")]
        public string? Description { get; set; }
        [JsonProperty(PropertyName = "containerTypeId")]
        public required string ContainerTypeId { get; set; }
        [JsonProperty(PropertyName = "createdDateTime")]
        public DateTime CreatedDateTime { get; set; }
        [JsonProperty(PropertyName = "status")]
        public required string Status { get; set; }
        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }
    }
    public class MSGraphService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MSGraphService> _logger;
        private const long SmallFileSizeBoundary = 4000000;
        private const string GraphContainersEndpoint = "beta/storage/fileStorage/containers";

        const string graphResource = "https://graph.microsoft.com";

        private HttpClient client = new HttpClient();

        public MSGraphService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<MSGraphService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            client.BaseAddress = new Uri("https://graph.microsoft.com");
        }

        public async Task<Container> AddContainerAsync(string accessToken, string containerName, string containerTypeId, string threadId)
        {
            // Create a dictionary for custom properties if needed
            var customProperties = new Dictionary<string, object>();

            // Add threadId as a custom property if provided
            if (!string.IsNullOrEmpty(threadId))
            {
                customProperties["threadId"] = new
                {
                    value = threadId,
                    isSearchable = true
                };
            }

            Container container = new Container()
            {
                DisplayName = containerName,
                ContainerTypeId = containerTypeId,
                Status = "active",
                CreatedDateTime = DateTime.UtcNow,
                Size = 0,
                Description = threadId,
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.PostAsJsonAsync($"{GraphContainersEndpoint}", container);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't create the container, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            container = response.Content.ReadFromJsonAsync<Container>().Result;

            await AddCustomPropertyToContainer(accessToken, container.Id, "threadId", threadId);

            return container;
        }

        internal async Task<FileStorageContainer> AddContainerAsync(string accessToken, FileStorageContainer container)
        {
            var response = await client.PostAsJsonAsync($"{GraphContainersEndpoint}", container);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't create the container, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            return response.Content.ReadFromJsonAsync<FileStorageContainer>().Result;
        }

        public async Task ActivateContainer(string accessToken, string containerId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.PostAsync($"{GraphContainersEndpoint}/{containerId}/activate", null);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't activate the container, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
        }

        public async Task<FileStorageContainer> GetContainer(string accessToken, string containerId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync($"{GraphContainersEndpoint}/{containerId}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't get the container, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            return response.Content.ReadFromJsonAsync<FileStorageContainer>().Result;
        }

        public async Task<FileStorageContainer> UpdateContainer(string accessToken, string containerId, FileStorageContainer container)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            string serialized = JsonConvert.SerializeObject(container, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            HttpContent content = new StringContent(serialized, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"{GraphContainersEndpoint}/{containerId}", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't update the container, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            return response.Content.ReadFromJsonAsync<FileStorageContainer>().Result;
        }

        public async Task DeleteContainer(string accessToken, string containerId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.DeleteAsync($"{GraphContainersEndpoint}/{containerId}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't delete the container. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
        }

        public async Task<IEnumerable<FileStorageContainer>> GetAllContainers(string accessToken, string containerTypeId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync($"{GraphContainersEndpoint}?$filter=containerTypeId eq {containerTypeId}");

            _logger.LogInformation("Request url {1}", response.RequestMessage.RequestUri.ToString());
            _logger.LogInformation("containerTypeId {1}", containerTypeId);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"We couldn't get the list of containers. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
            string content = await response.Content.ReadAsStringAsync();
            JObject deserialized = JsonConvert.DeserializeObject<JObject>(content);
            JArray array = deserialized.Value<JArray>("value");
            return array.ToObject<List<FileStorageContainer>>();
        }

        public async Task<bool> AddCustomPropertyToContainer(string accessToken, string containerId, string propertyName, object propertyValue)
        {
            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

                // Create the custom property dictionary with the specified property
                var customProperty = new
                {
                    value = propertyValue,
                    isSearchable = true
                };

                var requestBody = new Dictionary<string, object>
        {
            { propertyName, customProperty }
        };

                string serialized = JsonConvert.SerializeObject(requestBody);
                HttpContent content = new StringContent(serialized, Encoding.UTF8, "application/json");

                // Use the specific endpoint for custom properties
                var response = await client.PatchAsync($"{GraphContainersEndpoint}/{containerId}/customProperties", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to add custom property to container. Status code: {StatusCode}, Reason: {Reason}",
                        (int)response.StatusCode, response.ReasonPhrase);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom property to container");
                throw;
            }
        }

        public async Task<bool> AddThreadIdToContainer(string accessToken, string containerId, string threadId)
        {
            // Add the threadId as a custom property named "threadId"
            return await AddCustomPropertyToContainer(accessToken, containerId, "threadId", threadId);
        }

        public async Task<IEnumerable<FileStorageContainer>> GetContainersByThreadId(string accessToken, string threadId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

            // This will filter containers where the threadId custom property equals the provided threadId
            var endpoint = $"{GraphContainersEndpoint}?$filter=customProperties/threadId/value eq '{threadId}'";
            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get containers by threadId. Status code: {StatusCode}, Reason: {Reason}",
                    (int)response.StatusCode, response.ReasonPhrase);
                return Enumerable.Empty<FileStorageContainer>();
            }

            string content = await response.Content.ReadAsStringAsync();
            JObject deserialized = JsonConvert.DeserializeObject<JObject>(content);
            JArray array = deserialized.Value<JArray>("value");
            return array.ToObject<List<FileStorageContainer>>();
        }

        public async Task<IEnumerable<Permission>> GetContainerPermissions(string accessToken, string containerId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync($"{GraphContainersEndpoint}/{containerId}/permissions");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't get the container's permissions. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
            string content = await response.Content.ReadAsStringAsync();
            JObject deserialized = JsonConvert.DeserializeObject<JObject>(content);
            JArray array = deserialized.Value<JArray>("value");
            return array.ToObject<List<Permission>>();
        }

        public async Task<Permission> UpdateContainerPermission(string accessToken, string containerId, string permissionId, string role)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var json = $@"{{ ""roles"":[""{role}""]}}";
            HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"{GraphContainersEndpoint}/{containerId}/permissions/{permissionId}", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't update the container's permission. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
            return response.Content.ReadFromJsonAsync<Permission>().Result;
        }

        public async Task<Permission> AddContainerPermission(string accessToken, string containerId, Permission permission)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            string serialized = JsonConvert.SerializeObject(permission, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            HttpContent content = new StringContent(serialized, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{GraphContainersEndpoint}/{containerId}/permissions", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't add the permission to the container. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
            return response.Content.ReadFromJsonAsync<Permission>().Result;
        }

        public async Task DeleteContainerPermission(string accessToken, string containerId, string permissionId)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.DeleteAsync($"{GraphContainersEndpoint}/{containerId}/permissions/{permissionId}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't delete the permission. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }
        }

        public async Task<Drive> GetDrive(string accessToken, string driveId)
        {
            var graphServiceClient = getGraphClient(accessToken);
            Drive drive = await graphServiceClient.Drives[driveId]
            .GetAsync();

            var json = JsonConvert.SerializeObject(drive);
            Console.WriteLine(json);
            return drive;
        }

        public Task<Drive> UpdateDrive(string accessToken, string driveId, Drive drive)
        {
            throw new NotSupportedException();
        }

        public Task DeleteDrive(string accessToken, string driveId)
        {
            throw new NotImplementedException();
        }

        //public async Task<DriveItem> GetDriveRoot(string accessToken, string driveId)
        //{
        //    var graphServiceClient = getGraphClient(accessToken);
        //    return await graphServiceClient.Drives[driveId].Root.GetAsync();

        //}

        public async Task<DriveItem> GetDriveRoot(string accessToken, string driveId)
        {
            string endPoint = $"/beta/drives/{driveId}/root";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync(endPoint);

            _logger.LogInformation("Request url {1}", response.RequestMessage.RequestUri.ToString());
            _logger.LogInformation("Drive {1}", driveId);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't get the list of driveItems. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            return await response.Content.ReadFromJsonAsync<DriveItem>();
        }

        public async Task<ICollection<DriveItem>> GetDriveRootItems(string accessToken, string driveId)
        {
            string endPoint = $"/beta/drives/{driveId}/items/root/children";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync(endPoint);

            _logger.LogInformation("Request url {1}", response.RequestMessage.RequestUri.ToString());
            _logger.LogInformation("Drive {1}", driveId);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't get the list of driveItems. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            string content = await response.Content.ReadAsStringAsync();
            JObject deserialized = JsonConvert.DeserializeObject<JObject>(content);
            JArray array = deserialized.Value<JArray>("value");
            return array.ToObject<List<DriveItem>>();

            // return await response.Content.ReadFromJsonAsync<ICollection<DriveItem>>();
        }

        public async Task<ICollection<DriveItem>> GetDriveItems(string accessToken, string driveId, string itemId)
        {
            string endPoint = $"/beta/drives/{driveId}/items/{itemId}/children";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            var response = await client.GetAsync(endPoint);

            _logger.LogInformation("Request url {1}", response.RequestMessage.RequestUri.ToString());
            _logger.LogInformation("Drive {1}", driveId);
            _logger.LogInformation("ItemId {1}", itemId);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't get the list of driveItems. Status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            string content = await response.Content.ReadAsStringAsync();
            JObject deserialized = JsonConvert.DeserializeObject<JObject>(content);
            JArray array = deserialized.Value<JArray>("value");
            return array.ToObject<List<DriveItem>>();

            //return await response.Content.ReadFromJsonAsync<ICollection<DriveItem>>();

        }

        //public async Task<ICollection<DriveItem>> GetDriveItems(string accessToken, string driveId, string itemId)
        //{
        //    var graphServiceClient = getGraphClient(accessToken);
        //    var driveItems = await graphServiceClient.Drives[driveId].Items[itemId].Children
        //        .GetAsync();

        //    return driveItems.Value!;
        //    //List<DriveItem> driveItemsWithPermissions = new List<DriveItem>();

        //    //foreach (var driveItem in driveItems)
        //    //{
        //    //    var permissions = await graphServiceClient.Drives[driveId].Items[driveItem.Id].Permissions
        //    //        .Request()
        //    //        .GetAsync();

        //    //    driveItem.Permissions = permissions;
        //    //    driveItemsWithPermissions.Add(driveItem);
        //    //}

        //    //return driveItemsWithPermissions;
        //}

        public async Task<DriveItem> GetDriveItem(string accessToken, string driveId, string id)
        {
            var graphServiceClient = getGraphClient(accessToken);
            var driveItem = await graphServiceClient.Drives[driveId].Items[id].GetAsync();
            //.
            //.GetAsync(requestConfiguration => requestConfiguration.QueryParameters.Select = new string[] { "id", "createdDateTime" });

            //List<User> userList = usersResponse.Value;

            return driveItem;
            // return await graphServiceClient.Drives[driveId].Items.GetAsync(new Action<Microsoft.Kiota.Abstractions.RequestConfiguration<Microsoft.Graph.Drives.Item.Items.ItemsRequestBuilder.ItemsRequestBuilderGetQueryParameters>>().);
        }

        public async Task<string> GetItemPreview(string accessToken, string driveId, string itemId)
        {
            var graphServiceClient = getGraphClient(accessToken);
            ItemPreviewInfo preview = await graphServiceClient
                .Drives[driveId]
                .Items[itemId]
                .Preview
                .PostAsync(
                new Microsoft.Graph.Drives.Item.Items.Item.Preview.PreviewPostRequestBody(),
                null,
                new CancellationToken()
                );
            return preview.GetUrl;
        }

        public async Task<DriveItem> UpdateDriveItem(string accessToken, string driveId, string itemId, DriveItem driveItem)
        {
            await Task.CompletedTask;
            return new DriveItem();

            //var graphServiceClient = getGraphClient(accessToken);
            //return await graphServiceClient.Drives[driveId].Items[itemId]
            //    .ToPatchRequestInformation(driveItem);
        }

        public async Task<DriveItem> CopyDriveItem(string accessToken, string driveId, string itemId, string name, ItemReference parentReference)
        {
            await Task.CompletedTask;
            return new DriveItem();
            //var graphServiceClient = getGraphClient(accessToken);
            //return await graphServiceClient.Drives[driveId].Items[itemId]
            //    .Copy(name, parentReference)
            //    .Request()
            //    .PostAsync();
        }

        public async Task<DriveItem> AddFile(string accessToken, string driveId, string parentId, string name, Stream stream)
        {
            //Upload small file> https://docs.microsoft.com/en-us/graph/api/driveitem-put-content
            //Upload large file> https://docs.microsoft.com/en-us/graph/sdks/large-file-upload

            return await UploadSmallFile(accessToken, driveId, parentId, name, stream);
        }

        /// <summary>
        /// Uploads a small file (less than 4MB) directly using Graph API endpoint without GraphServiceClient
        /// </summary>
        /// <param name="accessToken">The access token for authentication</param>
        /// <param name="driveId">The ID of the drive</param>
        /// <param name="parentId">The ID of the parent folder</param>
        /// <param name="name">The name of the file to upload</param>
        /// <param name="stream">The stream containing the file data</param>
        /// <returns>The created DriveItem</returns>
        public async Task<DriveItem> UploadSmallFile(string accessToken, string driveId, string parentId, string name, Stream stream)
        {
            _logger.LogInformation("Starting small file upload with direct API: {Name} to drive {DriveId}, parent folder {ParentId}",
                name, driveId, parentId);

            try
            {
                // Create HttpClient and set authorization header
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Prepare the URL with conflict behavior parameter
                string apiUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{parentId}:/{name}:/content?@microsoft.graph.conflictBehavior=rename";

                // Read the stream content into a byte array
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                byte[] fileData = memoryStream.ToArray();

                // Create the HTTP content from the file data
                using var content = new ByteArrayContent(fileData);

                // Send the PUT request to upload the file
                var response = await httpClient.PutAsync(apiUrl, content);

                // Check if the request was successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error uploading file: Status code {StatusCode}, Content: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Error uploading file: {response.StatusCode}");
                }

                // Deserialize the response to get the created DriveItem
                string responseJson = await response.Content.ReadAsStringAsync();
                var driveItem = JsonConvert.DeserializeObject<DriveItem>(responseJson);

                _logger.LogInformation("Successfully uploaded file with direct API: {Name}, Item ID: {ItemId}",
                    name, driveItem?.Id);

                return driveItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file with direct API: {FileName}", name);
                throw;
            }
        }


        /// <summary>
        /// Uploads a large file (4MB or larger) to OneDrive/SharePoint using Microsoft Graph API upload session
        /// </summary>
        /// <summary>
        /// Uploads a large file (4MB or larger) to OneDrive/SharePoint using direct HTTP requests to Microsoft Graph API
        /// </summary>
        private async Task<DriveItem> UploadLargeFile(string accessToken, string driveId, string parentId, string name, Stream fileStream)
        {
            _logger.LogInformation("Starting large file upload with direct HTTP: {Name}", name);

            try
            {
                // Get the access token from the graph client
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Step 1: Create an upload session
                string createSessionUrl = $"https://graph.microsoft.com/beta/drives/{driveId}/items/{parentId}:/{name}:/createUploadSession";
                var requestBody = new
                {
                    item = new
                    {
                        name,
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "rename" }
                        }
                    }
                };

                StringContent content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                HttpResponseMessage sessionResponse = await client.PostAsync(createSessionUrl, content);

                if (!sessionResponse.IsSuccessStatusCode)
                {
                    string errorContent = await sessionResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Error creating upload session: {StatusCode}, {ErrorContent}",
                        sessionResponse.StatusCode, errorContent);
                    throw new HttpRequestException($"Error creating upload session: {sessionResponse.StatusCode}");
                }

                // Parse the upload session from the response
                string sessionResponseContent = await sessionResponse.Content.ReadAsStringAsync();
                var sessionInfo = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(sessionResponseContent);
                string uploadUrl = sessionInfo.GetProperty("uploadUrl").GetString();

                if (string.IsNullOrEmpty(uploadUrl))
                    throw new InvalidOperationException("Failed to get upload URL from session response");

                _logger.LogInformation("Created upload session: {UploadUrl}", uploadUrl);

                // The recommended fragment size is between 5-10 MiB
                int maxSliceSize = 5 * 1024 * 1024; // 5 MB
                long totalFileSize = fileStream.Length;
                long totalBytesUploaded = 0;

                // Reset the stream position
                fileStream.Position = 0;

                // Continue uploading until we've sent the entire file
                while (totalBytesUploaded < totalFileSize)
                {
                    // Calculate the size of this slice
                    int bytesToRead = (int)Math.Min(
                        maxSliceSize,
                        totalFileSize - totalBytesUploaded);

                    // Read that many bytes from the stream
                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead);

                    // If we couldn't read as many bytes as expected, the stream might have ended early
                    if (bytesRead == 0)
                        break;

                    // Create the content range header value
                    long rangeStart = totalBytesUploaded;
                    long rangeEnd = totalBytesUploaded + bytesRead - 1;
                    string contentRange = $"bytes {rangeStart}-{rangeEnd}/{totalFileSize}";

                    // Log the upload progress
                    double progressPercentage = (double)totalBytesUploaded / totalFileSize * 100;
                    _logger.LogInformation("Uploading slice: {RangeStart}-{RangeEnd}/{TotalSize} ({ProgressPercentage:F2}%)",
                        rangeStart, rangeEnd, totalFileSize, progressPercentage);

                    // Create the HTTP request for this slice
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                    request.Headers.Add("Content-Range", contentRange);
                    request.Content = new ByteArrayContent(buffer, 0, bytesRead);

                    // Send the request
                    HttpResponseMessage response = await client.SendAsync(request);

                    // Handle the response
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        totalBytesUploaded += bytesRead;

                        // If we've uploaded the entire file, the response should contain the item metadata
                        if (totalBytesUploaded >= totalFileSize)
                        {
                            DriveItem driveItem = System.Text.Json.JsonSerializer.Deserialize<DriveItem>(responseContent);
                            _logger.LogInformation("Successfully uploaded large file: {Name}, Item ID: {ItemId}",
                                name, driveItem?.Id);
                            return driveItem;
                        }
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Error uploading file slice: {StatusCode}, {ErrorContent}",
                            response.StatusCode, errorContent);
                        throw new HttpRequestException($"Error uploading file slice: {response.StatusCode}");
                    }
                }

                throw new InvalidOperationException("Upload completed without receiving final DriveItem response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading large file {FileName}", name);
                throw;
            }
        }

        public async Task DeleteDriveItem(string accessToken, string driveId, string itemId)
        {
            var graphServiceClient = getGraphClient(accessToken);
            await graphServiceClient.Drives[driveId].Items[itemId]
                .DeleteAsync();
        }

        public Task<ICollection<Container>> GetContainers(string uri, string accessToken)
        {
            //Not implemented, return an empty list
            ICollection<Container> containers = new List<Container>();
            return Task.FromResult(containers);
        }

        public async Task AddFolder(string accessToken, string driveId, string parentId, string name)
        {
            var driveItem = new DriveItem
            {
                Name = name,
                Folder = new Folder
                {
                },
                AdditionalData = new Dictionary<string, object>()
            {
                {"@microsoft.graph.conflictBehavior", "rename"}
            }
            };
            var graphClient = getGraphClient(accessToken);

            try
            {
                await graphClient.Drives[driveId].Items[parentId].Children
                        .PostAsync(driveItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task<ICollection<Permission>> GetPermissions(string accessToken, string driveId, string itemId)
        {
            var graphClient = getGraphClient(accessToken);
            var permissions = await graphClient.Drives[driveId].Items[itemId].Permissions.GetAsync();
            return permissions.Value;
        }

        public async Task<Permission> GetPermission(string accessToken, string driveId, string itemId, string permissionId)
        {
            var graphClient = getGraphClient(accessToken);
            var permission = await graphClient.Drives[driveId].Items[itemId].Permissions[permissionId]
                .GetAsync();
            return permission;
        }

        // Graph reference on permision update
        // https://learn.microsoft.com/en-us/graph/api/permission-update?view=graph-rest-beta
        //
        public async Task UpdatePermission(string accessToken, string driveId, string itemId, string permissionId, List<string> roles)
        {
            var permission = new Permission
            {
                Roles = roles
            };

            var graphClient = getGraphClient(accessToken);
            await graphClient.Drives[driveId].Items[itemId].Permissions[permissionId].PatchAsync(permission);
        }

        // Graph reference on permision delete
        // https://learn.microsoft.com/en-us/graph/api/permission-delete?view=graph-rest-beta
        //
        public async Task DeletePermission(string accessToken, string driveId, string itemId, string permissionId)
        {
            var graphClient = getGraphClient(accessToken);
            await graphClient.Drives[driveId].Items[itemId].Permissions[permissionId]
                .DeleteAsync();
        }


        // Graph reference on permisions
        // https://docs.microsoft.com/en-us/graph/api/driveitem-invite
        public async Task AddPermissions(string accessToken, string driveId, string itemId, IEnumerable<string> roles, IEnumerable<DriveRecipient> recipients)
        {
            var graphClient = getGraphClient(accessToken);

            var requireSignIn = true;
            var sendInvitation = false;
            InvitePostRequestBody invitePostRequestBody = new InvitePostRequestBody();
            invitePostRequestBody.Roles = roles.ToList();
            invitePostRequestBody.Recipients = recipients.ToList();
            invitePostRequestBody.RequireSignIn = requireSignIn;
            invitePostRequestBody.SendInvitation = sendInvitation;


            await graphClient.Drives[driveId].Items[itemId]
                 .Invite.PostAsInvitePostResponseAsync(invitePostRequestBody);


        }

        public async Task<Uri> GetFileDownloadUrl(string accessToken, string driveId, string itemId)
        {
            HttpClient client = GetHttpClient(accessToken, "application/json");
            var response = await client.GetAsync($"v1.0/drives/{driveId}/items/{itemId}/content");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"We couldn't download the file, status code {(int)response.StatusCode}, reason: {response.ReasonPhrase}''");
            }

            return response.RequestMessage.RequestUri;
        }

        /// <summary>
        /// Searches for drive items using the Microsoft Graph search API with direct HTTP calls.
        /// </summary>
        /// <param name="accessToken">The access token for authentication</param>
        /// <param name="searchString">The search query string</param>
        /// <param name="containerTypeId">Optional container type ID to filter results (defaults to null)</param>
        /// <param name="maxResults">Maximum number of results to return (defaults to 100)</param>
        /// <returns>A list of DriveItems matching the search criteria</returns>
        public async Task<List<DriveItem>> SearchDriveItems(string accessToken, string searchString, string containerTypeId = null, int maxResults = 100)
        {
            _logger.LogInformation("Searching drive items with direct API. Query: '{SearchString}'", searchString);

            try
            {
                // Create the request URL
                string endpoint = "https://graph.microsoft.com/beta/search/query";

                // Build the query string
                string queryString = !string.IsNullOrEmpty(containerTypeId)
                    ? $"({searchString}) AND (containerTypeId:{containerTypeId})"
                    : searchString;

                // Create the request payload
                var requestBody = new
                {
                    requests = new[]
                    {
                        new
                        {
                            entityTypes = new[] { "driveItem" },
                            query = new
                            {
                                queryString
                            },
                            size = maxResults,
                            sharePointOneDriveOptions = new
                            {
                                includeHiddenContent = true
                            },
                            from = 0,
                            fields =  new [] { "id", "name", "createdDateTime", "lastModifiedDateTime", "size", "threadIdOWSTEXT", "parentReference" }
                        }
                    }
                };

                // Serialize the request body
                string jsonRequest = JsonConvert.SerializeObject(requestBody);

                // Create HTTP client and set headers
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Send the POST request
                using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(endpoint, content);

                // Check if request was successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Search request failed. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Search request failed with status code {response.StatusCode}");
                }

                // Parse the response
                string responseContent = await response.Content.ReadAsStringAsync();
                JObject responseObject = JObject.Parse(responseContent);

                // Extract drive items from the search results
                var driveItems = new List<DriveItem>();

                // Navigate through the response structure
                var valueArray = responseObject["value"] as JArray;
                if (valueArray != null && valueArray.Count > 0)
                {
                    var hitsContainers = valueArray[0]["hitsContainers"] as JArray;
                    if (hitsContainers != null && hitsContainers.Count > 0)
                    {
                        var hits = hitsContainers[0]["hits"] as JArray;
                        if (hits != null)
                        {
                            foreach (var hit in hits)
                            {
                                // Extract the DriveItem from the resource property
                                var resourceObject = hit["resource"];
                                if (resourceObject != null && resourceObject["@odata.type"]?.ToString() == "#microsoft.graph.driveItem")
                                {
                                    try
                                    {
                                        // Convert to DriveItem
                                        var driveItem = resourceObject.ToObject<DriveItem>();

                                        // Ensure AdditionalData dictionary exists
                                        if (driveItem.AdditionalData == null)
                                        {
                                            driveItem.AdditionalData = new Dictionary<string, object>();
                                        }

                                        // Extract listItem fields if they exist
                                        if (resourceObject["listItem"] != null &&
                                            resourceObject["listItem"]["fields"] != null)
                                        {
                                            var fields = resourceObject["listItem"]["fields"];

                                            // Add threadId to AdditionalData if it exists
                                            if (fields["threadIdOWSTEXT"] != null)
                                            {
                                                string threadId = fields["threadIdOWSTEXT"].ToString();
                                                driveItem.AdditionalData["threadId"] = threadId;

                                                // Log for debugging
                                                _logger.LogDebug("Found threadId: {ThreadId} for file: {FileName}",
                                                    threadId, driveItem.Name);
                                            }

                                            // Store the entire fields object for completeness
                                            driveItem.AdditionalData["listItemFields"] = fields.ToObject<Dictionary<string, object>>();

                                            // If there's a listItem ID, store it too
                                            if (resourceObject["listItem"]["id"] != null)
                                            {
                                                driveItem.AdditionalData["listItemId"] = resourceObject["listItem"]["id"].ToString();
                                            }
                                        }

                                        // Store the hit ID and rank for reference
                                        if (hit["hitId"] != null)
                                        {
                                            driveItem.AdditionalData["hitId"] = hit["hitId"].ToString();
                                        }

                                        if (hit["rank"] != null && hit["rank"].Type == JTokenType.Integer)
                                        {
                                            driveItem.AdditionalData["searchRank"] = hit["rank"].Value<int>();
                                        }

                                        driveItems.Add(driveItem);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error processing search hit: {Message}", ex.Message);
                                        // Continue processing other hits even if one fails
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Search completed successfully. Found {Count} items.", driveItems.Count);
                return driveItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching drive items: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<ICollection<DriveItem>> SearchInDrive(string accessToken, string driveId, string searchString)
        {
            await Task.CompletedTask;
            //var graphServiceClient = getGraphClient(accessToken);
            //var driveItems = await graphServiceClient.Drives[driveId].Root
            //    .Search(searchString)
            //    .Request()
            //    .GetAsync();

            return new List<DriveItem>();
        }

        public async Task<ICollection<DriveItem>> SearchForCurrentUser(string accessToken, string searchString)
        {
            //var graphServiceClient = getGraphClient(accessToken);
            //var driveItems = await graphServiceClient.Me.Drive.Root
            //    .Search(searchString)
            //    .Request()
            //    .GetAsync();

            //return driveItems;

            await Task.CompletedTask;
            return new List<DriveItem>();
        }


        /// <summary>
        /// Prepares an authenticated HTTP client.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        private HttpClient GetHttpClient(string token, string responseMediaType = null)
        {
            HttpClient client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://graph.microsoft.com/beta");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
            if (responseMediaType != null)
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(responseMediaType));
            return client;
        }

        /// <summary>
        /// Prepares the authenticated client.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        private GraphServiceClient getGraphClient(string accessToken)
        {
            /***
            //Microsoft Azure AD Graph API endpoint,
            'https://graph.microsoft.com'   Microsoft Graph global service
            'https://graph.microsoft.us' Microsoft Graph for US Government
            'https://graph.microsoft.de' Microsoft Graph Germany
            'https://microsoftgraph.chinacloudapi.cn' Microsoft Graph China
                ***/

            //string graphEndpoint = _configuration.GetValue<string>("GraphAPI:Endpoint");
            //return new GraphServiceClient(graphEndpoint,
            //                new DelegateAuthenticationProvider(
            //                    async (requestMessage) =>
            //                    {
            //                        await Task.Run(() =>
            //                        {
            //                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            //                        });
            //                    }));

            var graphServiceClient = new GraphServiceClient(GetHttpClient(accessToken));
            return graphServiceClient;
        }
    }
}
