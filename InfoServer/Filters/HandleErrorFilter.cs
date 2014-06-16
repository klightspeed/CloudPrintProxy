using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http.Filters;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Runtime.InteropServices;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.InfoServer.Filters
{
    public class HandleErrorFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            actionExecutedContext.Response = new HttpResponseMessage
            {
                Content = new StringContent("Error: " + actionExecutedContext.Exception.ToString()),
                StatusCode = HttpStatusCode.InternalServerError
            };
        }
    }
}
