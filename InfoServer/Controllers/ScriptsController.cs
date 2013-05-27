using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Reflection;
using System.Text;
using System.Windows;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class ScriptsController : ApiController
    {
        protected string GetScript(string name)
        {
            string basens = typeof(CloudPrintInfoServer).Namespace;
            return new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(basens + ".Scripts." + name)).ReadToEnd();
        }

        // GET Scripts
        public HttpResponseMessage Get()
        {
            string path = Request.RequestUri.AbsolutePath;
            
            if (path.StartsWith("/Scripts/"))
            {
                path = path.Replace("/Scripts/", "");
                if (path != "")
                {
                    return Get(path);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        // GET Scripts/<name>
        public HttpResponseMessage Get(string id)
        {
            id = id.Replace("/", ".");
            try
            {
                return new HttpResponseMessage()
                {
                    Content = new StringContent(GetScript(id), Encoding.UTF8, "text/javascript")
                };
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }
}