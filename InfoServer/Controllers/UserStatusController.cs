using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.Util;
using System.IO;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class UserStatusController : ApiController
    {
        protected CloudPrintProxy PrintProxy
        {
            get
            {
                return Request.GetPrintProxy();
            }
        }

        [AllowAnonymous]
        public HttpResponseMessage Get(string username)
        {
            bool isauthenticated = WindowsIdentityStore.HasWindowsIdentity(username);
            int jobswaiting = PrintProxy.GetCloudPrintJobsForUser(username).Where(j => j.Status == CloudPrintJobStatus.QUEUED).Count();
            StringWriter writer = new StringWriter();

            JsonHelper.WriteJson(writer, new
            {
                isauthenticated = isauthenticated,
                jobswaiting = jobswaiting
            });

            return new HttpResponseMessage()
            {
                Content = new StringContent(writer.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }
}
