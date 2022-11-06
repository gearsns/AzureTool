using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebAuto.Handlers
{
    internal class VirtualHostSchemeHandlerFactory : ISchemeHandlerFactory
    {
        static Dictionary<string, string>? domainCache = null;
        public static Func<string, string> GetMimeTypeDelegate = (s) => { return ResourceHandler.GetMimeType(s); };
        public IResourceHandler? Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            var uri = new Uri(request.Url);

            if(domainCache == null)
            {
                domainCache = new();
                try
                {
                    foreach (var name in Directory.GetDirectories(Path.GetFullPath(@".\contents")))
                    {
                        domainCache[Path.GetFileName(name)] = name;
                    }
                }
                catch
                {

                }
            }
            if (!domainCache.ContainsKey(uri.Host))
            {
                return null;
            }
            var rootFolder = domainCache[uri.Host];
            //Get the absolute path and remove the leading slash
            var asbolutePath = uri.AbsolutePath.Substring(1);

            if (string.IsNullOrEmpty(asbolutePath))
            {
                asbolutePath = "index.html";
            }

            var filePath = WebUtility.UrlDecode(Path.GetFullPath(Path.Combine(rootFolder, asbolutePath)));

            //Check the file requested is within the specified path and that the file exists
            if (filePath != null && filePath.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase) && File.Exists(filePath))
            {
                var fileExtension = Path.GetExtension(filePath);
                var mimeType = GetMimeTypeDelegate(fileExtension);
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return ResourceHandler.FromStream(stream, mimeType, autoDisposeStream: true);
            }

            return ResourceHandler.ForErrorMessage("File Not Found - " + filePath, HttpStatusCode.NotFound);
        }
    }
}
