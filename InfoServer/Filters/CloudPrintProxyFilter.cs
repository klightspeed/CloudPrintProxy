using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using TSVCEO.CloudPrint.Proxy;

namespace TSVCEO.CloudPrint.InfoServer.Filters
{
    public class CloudPrintProxyFilter : ActionFilterAttribute
    {
        public CloudPrintProxy PrintProxy { get; set; }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            actionContext.Request.Properties["CloudPrintProxy"] = PrintProxy;
        }
    }
}
