using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using ImageResizer;
using ImageResizer.Configuration;
using ImageResizer.Plugins.PdfRenderer;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using System;

namespace UserReporting.ReportGenerator
{
    public static class Function1
    {
        private static readonly string _MainStorageAccountName = Environment.GetEnvironmentVariable("MainStorageAccountName");
        private static readonly string _MainStorageAccountKey = Environment.GetEnvironmentVariable("MainStorageAccountKey");
        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();
            var tenantId = (long)data.TenantId;
            var tenancyName = data.TenancyName.ToString();
            var assetFilename = data.AssetFilename.ToString();

            await Convert(tenantId, tenancyName, assetFilename);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static async Task Convert(long tenantId, string tenancyName, string assetFilename)
        {
            var containerName = tenantId.ToString() + "-" + tenancyName;
            // Retrieve reference to the blob
            var blobPath = string.Format(@"assets/{0}", assetFilename);
            var inputBlob = await GetBlobAsync(containerName, blobPath);

            PdfDocument s_document = new PdfDocument();

            PdfPage page = s_document.AddPage();

            XGraphics gfx = XGraphics.FromPdfPage(page);

            using (var blobStream = await inputBlob.OpenReadAsync())
            {
                using (XImage image = XImage.FromStream(blobStream))
                {
                    page.Width = image.PointWidth;
                    page.Height = image.PointHeight;

                    gfx.DrawImage(image, 0, 0);

                    using (var stream = new MemoryStream())
                    {
                        s_document.Save(stream, false);

                        var thumbnailIns = new Instructions
                        {
                            Width = 640,
                            Height = 360,
                            Mode = FitMode.Max,
                            Page = 1,
                            Format = "jpg"
                        };

                        var config = BuildApplicationResizerConfig();

                        using (var thumbnailStream = new MemoryStream())
                        {
                            var thumbnailJob = new ImageJob(stream, thumbnailStream, thumbnailIns);
                            config.Build(thumbnailJob);

                            thumbnailStream.Seek(0, SeekOrigin.Begin);

                            using (var fileStream = File.Create(@"D:\home\site\wwwroot\thumb2.jpg"))
                            {
                                thumbnailStream.CopyTo(fileStream);
                            }
                        }
                    }
                }
            }
           
        }

        private static async Task<CloudBlobContainer> GetBlobContainerAsync(string containerName)
        {
            // Retrieve storage account from connection string.
            CloudStorageAccount storageAccount =
            new CloudStorageAccount(new StorageCredentials(_MainStorageAccountName,
                _MainStorageAccountKey), true);

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            await container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            return container;
        }

        private static async Task<CloudBlockBlob> GetBlobAsync(string containerName, string blobPath)
        {
            var path = Uri.UnescapeDataString(blobPath.Replace(containerName + "/", ""));
            if (path.StartsWith("/"))
                path = path.Remove(0, 1);

            var container = await GetBlobContainerAsync(containerName);
            var blob = container.GetBlockBlobReference(path);

            if (!(await blob.ExistsAsync()))
            {
                throw new Exception("Asset not found");
            }

            return blob;
        }

        public static Config BuildApplicationResizerConfig()
        {
            var resizerConfig = new Config();

            new PdfRendererPlugin().Install(resizerConfig);

            return resizerConfig;
        }
    }
}
