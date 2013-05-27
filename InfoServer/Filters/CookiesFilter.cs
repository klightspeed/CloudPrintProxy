using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web.Http.Filters;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace TSVCEO.CloudPrint.InfoServer.Filters
{
    public class CookiesFilter : ActionFilterAttribute
    {
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Request.Properties.ContainsKey("SetCookies"))
            {
                var setcookies = actionExecutedContext.Request.Properties["SetCookies"] as Dictionary<string, string>;
                if (setcookies != null)
                {
                    foreach (var kvp in setcookies)
                    {
                        actionExecutedContext.Response.SetCookie(kvp.Key, kvp.Value);
                    }
                }
            }
        }
    }
}
