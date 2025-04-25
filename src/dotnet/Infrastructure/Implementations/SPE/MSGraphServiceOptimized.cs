//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using Microsoft.Graph;
//using Microsoft.Graph.Models;
//using Microsoft.Identity.Web;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Net.Http.Json;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Infrastructure.Implementations.SPE
//{
//    public class MSGraphService
//    {
//        private readonly IConfiguration _configuration;
//        private readonly IHttpClientFactory _httpClientFactory;
//        private readonly ILogger<MSGraphService> _logger;
//        private readonly GraphServiceClient _graphServiceClient;

//        private const long SmallFileSizeBoundary = 4_000_000;
//        private const string GraphContainersEndpoint = "beta/storage/fileStorage/containers";
//        private const string GraphResource = "https://graph.microsoft.com";

//        public MSGraphService(IConfiguration configuration,
//            IHttpClientFactory httpClientFactory,
//            ILogger<MSGraphService> logger,
//            ITokenAcquisition tokenAcquisition,
//            GraphServiceClient graphServiceClient
//            )
//        {
//            _configuration = configuration;
//            _httpClientFactory = httpClientFactory;
//            _logger = logger;
//            _graphServiceClient = graphServiceClient;
//        }

//        private GraphServiceClient GetGraphClient(string accessToken)
//        {
//            return _graphServiceClient;
//        }

//        private HttpClient GetHttpClient(string accessToken)
//        {
//            var client = _httpClientFactory.CreateClient();
//            client.BaseAddress = new Uri(GraphResource);
//            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
//            return client;
//        }

//        // Example: AddContainerAsync using HttpClient (custom endpoint)
//        public async Task<Container> AddContainerAsync(string accessToken, string containerName, string containerTypeId, string threadId)
//        {
//            var container = new Container
//            {
//                DisplayName = containerName,
//                ContainerTypeId = containerTypeId,
//                Status = "active",
//                CreatedDateTime = DateTime.UtcNow,
//                Size = 0,
//                Description = threadId,
//            };

//            using var client = GetHttpClient(accessToken);
//            var response = await client.PostAsJsonAsync(GraphContainersEndpoint, container);
//            if (!response.IsSuccessStatusCode)
//            {
//                var error = await response.Content.ReadAsStringAsync();
//                _logger.LogError("Failed to create container: {StatusCode} {Error}", response.StatusCode, error);
//                throw new Exception($"Failed to create container: {response.StatusCode}");
//            }
//            var created = await response.Content.ReadFromJsonAsync<Container>();
//            await AddCustomPropertyToContainer(accessToken, created.Id, "threadId", threadId);
//            return created;
//        }

//        // Example: ActivateContainer using HttpClient (custom endpoint)
//        public async Task ActivateContainerAsync(string accessToken, string containerId)
//        {
//            using var client = GetHttpClient(accessToken);
//            var response = await client.PostAsync($"{GraphContainersEndpoint}/{containerId}/activate", null);
//            if (!response.IsSuccessStatusCode)
//            {
//                var error = await response.Content.ReadAsStringAsync();
//                _logger.LogError("Failed to activate container: {StatusCode} {Error}", response.StatusCode, error);
//                throw new Exception($"Failed to activate container: {response.StatusCode}");
//            }
//        }

//        // Example: GetContainer using HttpClient (custom endpoint)
//        public async Task<Container> GetContainerAsync(string accessToken, string containerId)
//        {
//            using var client = GetHttpClient(accessToken);
//            var response = await client.GetAsync($"{GraphContainersEndpoint}/{containerId}");
//            if (!response.IsSuccessStatusCode)
//            {
//                var error = await response.Content.ReadAsStringAsync();
//                _logger.LogError("Failed to get container: {StatusCode} {Error}", response.StatusCode, error);
//                throw new Exception($"Failed to get container: {response.StatusCode}");
//            }
//            return await response.Content.ReadFromJsonAsync<Container>();
//        }

//        // Example: AddCustomPropertyToContainer using HttpClient (custom endpoint)
//        public async Task<bool> AddCustomPropertyToContainer(string accessToken, string containerId, string propertyName, object propertyValue)
//        {
//            try
//            {
//                using var client = GetHttpClient(accessToken);
//                var customProperty = new { value = propertyValue, isSearchable = true };
//                var requestBody = new Dictionary<string, object> { { propertyName, customProperty } };
//                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
//                var response = await client.PatchAsync($"{GraphContainersEndpoint}/{containerId}/customProperties", content);
//                if (!response.IsSuccessStatusCode)
//                {
//                    var error = await response.Content.ReadAsStringAsync();
//                    _logger.LogError("Failed to add custom property: {StatusCode} {Error}", response.StatusCode, error);
//                    return false;
//                }
//                return true;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error adding custom property");
//                throw;
//            }
//        }

//        // Example: GetDrive using GraphServiceClient
//        public async Task<Drive> GetDriveAsync(string accessToken, string driveId)
//        {
//            var client = GetGraphClient(accessToken);
//            return await client.Drives[driveId].GetAsync();
//        }

//        // Example: GetDriveRoot using GraphServiceClient
//        public async Task<DriveItem> GetDriveRootAsync(string accessToken, string driveId)
//        {
//            var client = GetGraphClient(accessToken);
//            return await client.Drives[driveId].Root.GetAsync();
//        }

//        // Example: GetDriveRootItems using GraphServiceClient
//        public async Task<ICollection<DriveItem>> GetDriveRootItemsAsync(string accessToken, string driveId)
//        {
//            var client = GetGraphClient(accessToken);
//            var result = await client.Drives[driveId].Root.ItemWithPath("/").Children.GetAsync();
//            return result.Value;
//        }

//        // Example: GetDriveItems using GraphServiceClient
//        public async Task<ICollection<DriveItem>> GetDriveItemsAsync(string accessToken, string driveId, string itemId)
//        {
//            var client = GetGraphClient(accessToken);
//            var result = await client.Drives[driveId].Items[itemId].Children.GetAsync();
//            return result.Value;
//        }

//        // Example: GetDriveItem using GraphServiceClient
//        public async Task<DriveItem> GetDriveItemAsync(string accessToken, string driveId, string itemId)
//        {
//            var client = GetGraphClient(accessToken);
//            return await client.Drives[driveId].Items[itemId].GetAsync();
//        }

//        // Example: GetPermissions using GraphServiceClient
//        public async Task<ICollection<Permission>> GetPermissionsAsync(string accessToken, string driveId, string itemId)
//        {
//            var client = GetGraphClient(accessToken);
//            var result = await client.Drives[driveId].Items[itemId].Permissions.GetAsync();
//            return result.Value;
//        }

//        // Example: AddPermissions using GraphServiceClient
//        public async Task AddPermissionsAsync(string accessToken, string driveId, string itemId, IEnumerable<string> roles, IEnumerable<DriveRecipient> recipients)
//        {
//            var client = GetGraphClient(accessToken);
//            var inviteBody = new Microsoft.Graph.Drives.Item.Items.Item.Invite.InvitePostRequestBody
//            {
//                Roles = roles.ToList(),
//                Recipients = recipients.ToList(),
//                RequireSignIn = true,
//                SendInvitation = false
//            };
//            await client.Drives[driveId].Items[itemId].Invite.PostAsInvitePostResponseAsync(inviteBody);
//        }

//        // Example: UpdatePermission using GraphServiceClient
//        public async Task UpdatePermissionAsync(string accessToken, string driveId, string itemId, string permissionId, List<string> roles)
//        {
//            var client = GetGraphClient(accessToken);
//            var permission = new Permission { Roles = roles };
//            await client.Drives[driveId].Items[itemId].Permissions[permissionId].PatchAsync(permission);
//        }

//        // Example: DeletePermission using GraphServiceClient
//        public async Task DeletePermissionAsync(string accessToken, string driveId, string itemId, string permissionId)
//        {
//            var client = GetGraphClient(accessToken);
//            await client.Drives[driveId].Items[itemId].Permissions[permissionId].DeleteAsync();
//        }

//        public async Task AddFolder(string accessToken, string driveId, string parentId, string name)
//        {
//            var driveItem = new DriveItem
//            {
//                Name = name,
//                Folder = new Folder
//                {
//                },
//                AdditionalData = new Dictionary<string, object>()
//                    {
//                        {"@microsoft.graph.conflictBehavior", "rename"}
//                    }
//            };
//            var graphClient = GetGraphClient(accessToken);

//            try
//            {
//                await graphClient.Drives[driveId].Items[parentId].Children
//                        .PostAsync(driveItem);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine(ex);
//            }
//        }

//        public async Task<DriveItem> AddFile(string accessToken, string driveId, string parentId, string name, Stream stream)
//        {
//            //Upload small file> https://docs.microsoft.com/en-us/graph/api/driveitem-put-content
//            //Upload large file> https://docs.microsoft.com/en-us/graph/sdks/large-file-upload

//            return await UploadSmallFile(accessToken, driveId, parentId, name, stream);
//        }

//        /// <summary>
//        /// Uploads a small file (less than 4MB) directly using Graph API endpoint without GraphServiceClient
//        /// </summary>
//        /// <param name="accessToken">The access token for authentication</param>
//        /// <param name="driveId">The ID of the drive</param>
//        /// <param name="parentId">The ID of the parent folder</param>
//        /// <param name="name">The name of the file to upload</param>
//        /// <param name="stream">The stream containing the file data</param>
//        /// <returns>The created DriveItem</returns>
//        public async Task<DriveItem> UploadSmallFile(string accessToken, string driveId, string parentId, string name, Stream stream)
//        {
//            _logger.LogInformation("Starting small file upload with direct API: {Name} to drive {DriveId}, parent folder {ParentId}",
//                name, driveId, parentId);

//            try
//            {
//                // Create HttpClient and set authorization header
//                using var httpClient = new HttpClient();
//                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

//                // Prepare the URL with conflict behavior parameter
//                string apiUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{parentId}:/{name}:/content?@microsoft.graph.conflictBehavior=rename";

//                // Read the stream content into a byte array
//                using var memoryStream = new MemoryStream();
//                await stream.CopyToAsync(memoryStream);
//                byte[] fileData = memoryStream.ToArray();

//                // Create the HTTP content from the file data
//                using var content = new ByteArrayContent(fileData);

//                // Send the PUT request to upload the file
//                var response = await httpClient.PutAsync(apiUrl, content);

//                // Check if the request was successful
//                if (!response.IsSuccessStatusCode)
//                {
//                    string errorContent = await response.Content.ReadAsStringAsync();
//                    _logger.LogError("Error uploading file: Status code {StatusCode}, Content: {ErrorContent}",
//                        response.StatusCode, errorContent);
//                    throw new HttpRequestException($"Error uploading file: {response.StatusCode}");
//                }

//                // Deserialize the response to get the created DriveItem
//                string responseJson = await response.Content.ReadAsStringAsync();
//                var driveItem = JsonConvert.DeserializeObject<DriveItem>(responseJson);

//                _logger.LogInformation("Successfully uploaded file with direct API: {Name}, Item ID: {ItemId}",
//                    name, driveItem?.Id);

//                return driveItem;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error uploading file with direct API: {FileName}", name);
//                throw;
//            }
//        }

//        private async Task<DriveItem> UploadLargeFile(string accessToken, string driveId, string parentId, string name, Stream fileStream)
//        {
//            _logger.LogInformation("Starting large file upload with direct HTTP: {Name}", name);

//            try
//            {
//                // Get the access token from the graph client
//                HttpClient client = new HttpClient();
//                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

//                // Step 1: Create an upload session
//                string createSessionUrl = $"https://graph.microsoft.com/beta/drives/{driveId}/items/{parentId}:/{name}:/createUploadSession";
//                var requestBody = new
//                {
//                    item = new
//                    {
//                        name,
//                        AdditionalData = new Dictionary<string, object>
//                                {
//                                    { "@microsoft.graph.conflictBehavior", "rename" }
//                                }
//                    }
//                };

//                StringContent content = new StringContent(
//                    System.Text.Json.JsonSerializer.Serialize(requestBody),
//                    Encoding.UTF8,
//                    "application/json");

//                HttpResponseMessage sessionResponse = await client.PostAsync(createSessionUrl, content);

//                if (!sessionResponse.IsSuccessStatusCode)
//                {
//                    string errorContent = await sessionResponse.Content.ReadAsStringAsync();
//                    _logger.LogError("Error creating upload session: {StatusCode}, {ErrorContent}",
//                        sessionResponse.StatusCode, errorContent);
//                    throw new HttpRequestException($"Error creating upload session: {sessionResponse.StatusCode}");
//                }

//                // Parse the upload session from the response
//                string sessionResponseContent = await sessionResponse.Content.ReadAsStringAsync();
//                var sessionInfo = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(sessionResponseContent);
//                string uploadUrl = sessionInfo.GetProperty("uploadUrl").GetString();

//                if (string.IsNullOrEmpty(uploadUrl))
//                    throw new InvalidOperationException("Failed to get upload URL from session response");

//                _logger.LogInformation("Created upload session: {UploadUrl}", uploadUrl);

//                // The recommended fragment size is between 5-10 MiB
//                int maxSliceSize = 5 * 1024 * 1024; // 5 MB
//                long totalFileSize = fileStream.Length;
//                long totalBytesUploaded = 0;

//                // Reset the stream position
//                fileStream.Position = 0;

//                // Continue uploading until we've sent the entire file
//                while (totalBytesUploaded < totalFileSize)
//                {
//                    // Calculate the size of this slice
//                    int bytesToRead = (int)Math.Min(
//                        maxSliceSize,
//                        totalFileSize - totalBytesUploaded);

//                    // Read that many bytes from the stream
//                    byte[] buffer = new byte[bytesToRead];
//                    int bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead);

//                    // If we couldn't read as many bytes as expected, the stream might have ended early
//                    if (bytesRead == 0)
//                        break;

//                    // Create the content range header value
//                    long rangeStart = totalBytesUploaded;
//                    long rangeEnd = totalBytesUploaded + bytesRead - 1;
//                    string contentRange = $"bytes {rangeStart}-{rangeEnd}/{totalFileSize}";

//                    // Log the upload progress
//                    double progressPercentage = (double)totalBytesUploaded / totalFileSize * 100;
//                    _logger.LogInformation("Uploading slice: {RangeStart}-{RangeEnd}/{TotalSize} ({ProgressPercentage:F2}%)",
//                        rangeStart, rangeEnd, totalFileSize, progressPercentage);

//                    // Create the HTTP request for this slice
//                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
//                    request.Headers.Add("Content-Range", contentRange);
//                    request.Content = new ByteArrayContent(buffer, 0, bytesRead);

//                    // Send the request
//                    HttpResponseMessage response = await client.SendAsync(request);

//                    // Handle the response
//                    if (response.IsSuccessStatusCode)
//                    {
//                        string responseContent = await response.Content.ReadAsStringAsync();
//                        totalBytesUploaded += bytesRead;

//                        // If we've uploaded the entire file, the response should contain the item metadata
//                        if (totalBytesUploaded >= totalFileSize)
//                        {
//                            DriveItem driveItem = System.Text.Json.JsonSerializer.Deserialize<DriveItem>(responseContent);
//                            _logger.LogInformation("Successfully uploaded large file: {Name}, Item ID: {ItemId}",
//                                name, driveItem?.Id);
//                            return driveItem;
//                        }
//                    }
//                    else
//                    {
//                        string errorContent = await response.Content.ReadAsStringAsync();
//                        _logger.LogError("Error uploading file slice: {StatusCode}, {ErrorContent}",
//                            response.StatusCode, errorContent);
//                        throw new HttpRequestException($"Error uploading file slice: {response.StatusCode}");
//                    }
//                }

//                throw new InvalidOperationException("Upload completed without receiving final DriveItem response");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error uploading large file {FileName}", name);
//                throw;
//            }
//        }

//        /// <summary>
//        /// Searches for drive items using the Microsoft Graph search API with direct HTTP calls.
//        /// </summary>
//        /// <param name="accessToken">The access token for authentication</param>
//        /// <param name="searchString">The search query string</param>
//        /// <param name="containerTypeId">Optional container type ID to filter results (defaults to null)</param>
//        /// <param name="maxResults">Maximum number of results to return (defaults to 100)</param>
//        /// <returns>A list of DriveItems matching the search criteria</returns>
//        public async Task<List<DriveItem>> SearchDriveItems(string accessToken, string searchString, string containerTypeId = null, int maxResults = 100)
//        {
//            _logger.LogInformation("Searching drive items with direct API. Query: '{SearchString}'", searchString);

//            try
//            {
//                // Create the request URL
//                string endpoint = "https://graph.microsoft.com/beta/search/query";

//                // Build the query string
//                string queryString = !string.IsNullOrEmpty(containerTypeId)
//                    ? $"({searchString}) AND (containerTypeId:{containerTypeId})"
//                    : searchString;

//                // Create the request payload
//                var requestBody = new
//                {
//                    requests = new[]
//                    {
//                                new
//                                {
//                                    entityTypes = new[] { "driveItem" },
//                                    query = new
//                                    {
//                                        queryString
//                                    },
//                                    size = maxResults,
//                                    sharePointOneDriveOptions = new
//                                    {
//                                        includeHiddenContent = true
//                                    },
//                                    from = 0,
//                                    fields =  new [] { "id", "name", "createdDateTime", "lastModifiedDateTime", "size", "threadIdOWSTEXT", "parentReference" }
//                                }
//                            }
//                };

//                // Serialize the request body
//                string jsonRequest = JsonConvert.SerializeObject(requestBody);

//                // Create HTTP client and set headers
//                using var httpClient = new HttpClient();
//                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

//                // Send the POST request
//                using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
//                var response = await httpClient.PostAsync(endpoint, content);

//                // Check if request was successful
//                if (!response.IsSuccessStatusCode)
//                {
//                    string errorContent = await response.Content.ReadAsStringAsync();
//                    _logger.LogError("Search request failed. Status: {StatusCode}, Error: {Error}",
//                        response.StatusCode, errorContent);
//                    throw new HttpRequestException($"Search request failed with status code {response.StatusCode}");
//                }

//                // Parse the response
//                string responseContent = await response.Content.ReadAsStringAsync();
//                JObject responseObject = JObject.Parse(responseContent);

//                // Extract drive items from the search results
//                var driveItems = new List<DriveItem>();

//                // Navigate through the response structure
//                var valueArray = responseObject["value"] as JArray;
//                if (valueArray != null && valueArray.Count > 0)
//                {
//                    var hitsContainers = valueArray[0]["hitsContainers"] as JArray;
//                    if (hitsContainers != null && hitsContainers.Count > 0)
//                    {
//                        var hits = hitsContainers[0]["hits"] as JArray;
//                        if (hits != null)
//                        {
//                            foreach (var hit in hits)
//                            {
//                                // Extract the DriveItem from the resource property
//                                var resourceObject = hit["resource"];
//                                if (resourceObject != null && resourceObject["@odata.type"]?.ToString() == "#microsoft.graph.driveItem")
//                                {
//                                    try
//                                    {
//                                        // Convert to DriveItem
//                                        var driveItem = resourceObject.ToObject<DriveItem>();

//                                        // Ensure AdditionalData dictionary exists
//                                        if (driveItem.AdditionalData == null)
//                                        {
//                                            driveItem.AdditionalData = new Dictionary<string, object>();
//                                        }

//                                        // Extract listItem fields if they exist
//                                        if (resourceObject["listItem"] != null &&
//                                            resourceObject["listItem"]["fields"] != null)
//                                        {
//                                            var fields = resourceObject["listItem"]["fields"];

//                                            // Add threadId to AdditionalData if it exists
//                                            if (fields["threadIdOWSTEXT"] != null)
//                                            {
//                                                string threadId = fields["threadIdOWSTEXT"].ToString();
//                                                driveItem.AdditionalData["threadId"] = threadId;

//                                                // Log for debugging
//                                                _logger.LogDebug("Found threadId: {ThreadId} for file: {FileName}",
//                                                    threadId, driveItem.Name);
//                                            }

//                                            // Store the entire fields object for completeness
//                                            driveItem.AdditionalData["listItemFields"] = fields.ToObject<Dictionary<string, object>>();

//                                            // If there's a listItem ID, store it too
//                                            if (resourceObject["listItem"]["id"] != null)
//                                            {
//                                                driveItem.AdditionalData["listItemId"] = resourceObject["listItem"]["id"].ToString();
//                                            }
//                                        }

//                                        // Store the hit ID and rank for reference
//                                        if (hit["hitId"] != null)
//                                        {
//                                            driveItem.AdditionalData["hitId"] = hit["hitId"].ToString();
//                                        }

//                                        if (hit["rank"] != null && hit["rank"].Type == JTokenType.Integer)
//                                        {
//                                            driveItem.AdditionalData["searchRank"] = hit["rank"].Value<int>();
//                                        }

//                                        driveItems.Add(driveItem);
//                                    }
//                                    catch (Exception ex)
//                                    {
//                                        _logger.LogError(ex, "Error processing search hit: {Message}", ex.Message);
//                                        // Continue processing other hits even if one fails
//                                    }
//                                }
//                            }
//                        }
//                    }
//                }

//                _logger.LogInformation("Search completed successfully. Found {Count} items.", driveItems.Count);
//                return driveItems;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error searching drive items: {Message}", ex.Message);
//                throw;
//            }
//        }
//    }
//}
