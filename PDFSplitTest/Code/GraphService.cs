using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Authentication.Azure;
using static Microsoft.Graph.Drives.DrivesRequestBuilder;

namespace PDFSplitTest.Code
{
    public class GraphService
    {
        private GraphServiceClient graphClient;
        private string _hostname;

        public GraphService(string tenantId, string hostName, string clientId, string clientSecret)
        {
            #region Connection
            _hostname = hostName;
            string[] scopes = { "https://graph.microsoft.com/.default" };
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            AzureIdentityAuthenticationProvider authProvider = new AzureIdentityAuthenticationProvider(clientSecretCredential, scopes: scopes);
            var handlers = GraphClientFactory.CreateDefaultHandlers();

            var httpClient = GraphClientFactory.Create(handlers);
            graphClient = new GraphServiceClient(httpClient, authProvider);
            #endregion
        }

        public async Task<Site?> GetSiteAync(string relativeSiteUrl)
        {
            var siteUrl = $"{_hostname}:{relativeSiteUrl}";
            var siteCorsoSpfx = await graphClient.Sites[siteUrl].GetAsync(requestConfiguration =>
            {
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "name", "webUrl" };
            });

            return siteCorsoSpfx;
        }

        public async Task<List<Drive>?> GetLibrariesAsync(string siteId)
        {
            try
            {
                DriveCollectionResponse? listsResponse = null!;

                listsResponse = await graphClient.Sites[siteId].Drives.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["Id", "Name", "WebUrl", "System"];
                });

                return listsResponse?.Value;
            }
            catch (ODataError odataError)
            {
                if (odataError.Error != null)
                {
                    Console.WriteLine($"Code: '{odataError.Error.Code}' - Msg: {odataError.Error.Message}");
                    if (odataError.Error.Code?.ToLower() == "invalidrequest")
                    {
                        throw new Exception(odataError.Error.Message, odataError);
                    }
                }
                throw;
            }
        }

        public async Task<DriveItem?> GetDriveItemByNameAsync(string driveId, string objName)
        {
            try
            {
                objName = objName.StartsWith("/") ? objName : $"/{objName}";
                return await graphClient.Drives[driveId].Root.ItemWithPath($"{objName}").GetAsync();
            }
            catch (ODataError odataError)
            {
                if (odataError.Error != null)
                {
                    Console.WriteLine($"Code: '{odataError.Error.Code}' - Msg: {odataError.Error.Message}");
                    if (odataError.Error.Code?.ToLower() == "itemnotfound")
                    {
                        throw new Exception($"Get object '{objName}': {odataError.Error.Message}", odataError);
                    }
                }
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Msg: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetFileDownloadUrl(string driveId, string fileName)
        {
            ////var fileContent = await graphClient.Drives[driveId].Items[fileId].Content.GetAsync();
            ////return fileContent!;
            string? result = string.Empty;

            var driveItem = await graphClient.Drives[driveId]
            .Root
            .ItemWithPath(fileName)
            .GetAsync();

            if (driveItem != null)
            {
                result = driveItem.AdditionalData != null ? driveItem.AdditionalData["@microsoft.graph.downloadUrl"].ToString() : string.Empty;
            }

            return result!;
        }

        public async Task<Stream> DownloadFileAsStream(string downloadUrl)
        {
            const int chunkSize = 10 * 1024 * 1024; // 10 MB per blocco
            long offset = 0;
            bool moreChunks = true;

            var memoryStream = new MemoryStream();

            using (var httpClient = new HttpClient())
            {
                while (moreChunks)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, offset + chunkSize - 1);

                    var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using (var chunkStream = await response.Content.ReadAsStreamAsync())
                    {
                        await chunkStream.CopyToAsync(memoryStream);
                    }

                    offset += chunkSize;

                    // Controlla se ci sono altri blocchi da scaricare
                    if (response!.Content!.Headers!.ContentRange!.Length.HasValue && offset >= response.Content.Headers.ContentRange.Length)
                    {
                        moreChunks = false;
                    }
                }
            }

            // Riposiziona lo Stream all'inizio per permettere la lettura
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task<DriveItem> UploadFileToSharePoint(string destinationLibraryId, Stream fileStream, string fileName, string conflictBehavior = "replace")
        {

            // Use properties to specify the conflict behavior
            // in this case, replace
            var uploadSessionRequestBody = new CreateUploadSessionPostRequestBody
            {
                Item = new DriveItemUploadableProperties
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "@microsoft.graph.conflictBehavior", conflictBehavior }
                    }
                }
            };

            // Create the upload session
            // itemPath does not need to be a path to an existing item
            var uploadSession = await graphClient.Drives[destinationLibraryId].Root
                .ItemWithPath(fileName)
                .CreateUploadSession
                .PostAsync(uploadSessionRequestBody);

            // Max slice size must be a multiple of 320 KiB
            //int maxSliceSize = 320 * 1024;
            //Uso -1 per fare chunck da 5MB (valore di default)
            var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, -1, graphClient.RequestAdapter);

            var totalLength = fileStream.Length;
            // Create a callback that is invoked after each slice is uploaded
            IProgress<long> progress = new Progress<long>(prog =>
            {
                Console.WriteLine($"Uploaded {prog} bytes of {totalLength} bytes");
            });

            try
            {
                // Upload the file
                var uploadResult = await fileUploadTask.UploadAsync(progress);

                if (uploadResult.UploadSucceeded == false)
                {
                    throw new Exception("Unable to upload file");
                }
                return uploadResult.ItemResponse;
            }
            catch (Microsoft.Graph.ServiceException ex)
            {
                Console.WriteLine($"Error uploading: {ex}");
                throw;
            }
        }
    }
}
