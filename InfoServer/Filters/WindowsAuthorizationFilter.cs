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
    public class WindowsAuthorizationFilter : ActionFilterAttribute
    {
        public bool Authorize { get; set; }

        protected static Dictionary<string, string> SessionUsers = new Dictionary<string, string>();

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            base.OnActionExecuting(actionContext);
            var ctx = actionContext.ControllerContext;
            var ctl = ctx.Controller;

            try
            {
                if (IsAuthorized(actionContext))
                {
                    base.OnActionExecuting(actionContext);
                }
                else
                {
                    HandleUnauthorizedRequest(actionContext);
                }
            }
            catch (Exception ex)
            {
                actionContext.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Error: " + ex.ToString())
                };
            }
        }

        protected bool IsAuthorized(HttpActionContext actionContext)
        {
            Session session = actionContext.Request.GetSession();
            string username = session["username"];

            if (username != null)
            {
                if (WindowsIdentityStore.HasWindowsIdentity(username))
                {
                    return true;
                }
            }

            var auth = actionContext.Request.Headers.Authorization;

            if (auth != null)
            {
                byte[] authdata = Convert.FromBase64String(auth.Parameter);
                username = Encoding.UTF8.GetString(authdata.TakeWhile(c => c != (byte)':').ToArray());
                SecureString password = new SecureString();
                foreach (byte c in authdata.SkipWhile(c => c != (byte)':').Skip(1))
                {
                    password.AppendChar((char)c);
                }
                password.MakeReadOnly();
                var identity = WindowsIdentityStore.Login(username, password);

                if (identity != null && identity.IsAuthenticated)
                {
                    session["username"] = username;
                    return true;
                }
            }

            return false;
        }

        protected void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            actionContext.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            actionContext.Response.Headers.WwwAuthenticate.Clear();
            actionContext.Response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"Google Cloud Print Proxy\""));
        }
    }
}
