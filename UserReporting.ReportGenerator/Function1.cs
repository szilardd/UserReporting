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

namespace UserReporting.ReportGenerator
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            Convert();

            // parse query parameter
            string name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            if (name == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                name = data?.name;
            }

            return name == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
                : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
        }

        private static void Convert()
        {
            PdfDocument s_document = new PdfDocument();

            PdfPage page = s_document.AddPage();

            XGraphics gfx = XGraphics.FromPdfPage(page);

            XImage image = XImage.FromFile(@"D:\home\site\wwwroot\bd223fb1-c0ad-4034-8ceb-657592188f2d.tif");

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

                    using (var fileStream = File.Create(@"D:\home\site\wwwroot\thumb.jpg"))
                    {
                        thumbnailStream.CopyTo(fileStream);
                    }
                }
            }
        }

        public static Config BuildApplicationResizerConfig()
        {
            var resizerConfig = new Config();

            new PdfRendererPlugin().Install(resizerConfig);

            return resizerConfig;
        }
    }
}
