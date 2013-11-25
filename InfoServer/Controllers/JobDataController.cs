using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Xml.Linq;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.Util;
using TSVCEO.CloudPrint.InfoServer.Filters;
using System.IO;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    class JobDataController : ApiController
    {
        protected CloudPrintProxy PrintProxy
        {
            get
            {
                return Request.GetPrintProxy();
            }
        }

        public HttpResponseMessage Get(string JobID)
        {
            CloudPrintJob job = PrintProxy.GetCloudPrintJobById(JobID);
            
            if (job == null)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            string username = Request.GetSession()["username"];

            bool isadmin = WindowsIdentityStore.IsUserAdmin(username);

            if (isadmin || username == job.Username)
            {
                HttpResponseMessage response = new HttpResponseMessage
                {
                    Content = new StreamContent(File.Open(job.GetPrintDataFile(), FileMode.Open, FileAccess.Read, FileShare.Read))
                };
                response.Content.Headers.ContentType.MediaType = "application/pdf";

                return response;
            }
            else
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            }
        }
    }
}
