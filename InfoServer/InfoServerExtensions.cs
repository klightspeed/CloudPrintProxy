using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net.Http;
using TSVCEO.CloudPrint.Proxy;

namespace TSVCEO.CloudPrint.InfoServer
{
    public static class InfoServerExtensions
    {
        public static NameValueCollection GetCookies(this HttpRequestMessage request)
        {
            var cookiestr = request.Headers.SingleOrDefault(h => h.Key == "Cookie").Value;
            var cookies = new NameValueCollection();

            if (cookiestr != null)
            {
                foreach (var cookienv in cookiestr.SelectMany(cs => cs.Split(';').Select(c => c.Trim().Split(new char[] { '=' }, 2))))
                {
                    cookies.Add(cookienv[0], cookienv[1]);
                }
            }

            return cookies;
        }

        public static string GetCookie(this HttpRequestMessage request, string name)
        {
            return GetCookies(request)[name];
        }

        public static void SetCookie(this HttpRequestMessage request, string name, string value)
        {
            if (!request.Properties.ContainsKey("SetCookies"))
            {
                request.Properties["SetCookies"] = new Dictionary<string, string>();
            }
            ((Dictionary<string, string>)request.Properties["SetCookies"])[name] = value;
        }

        public static void SetCookie(this HttpResponseMessage response, string name, string value)
        {
            response.Headers.Add("Set-Cookie", name + "=" + value);
        }

        public static Session GetSession(this HttpRequestMessage request)
        {
            string sessionid = request.GetCookie("SessionID");

            if (sessionid == null)
            {
                sessionid = Guid.NewGuid().ToString();
                request.SetCookie("SessionID", sessionid);
            }

            return new Session(sessionid);
        }

        public static CloudPrintProxy GetPrintProxy(this HttpRequestMessage request)
        {
            return request.Properties["CloudPrintProxy"] as CloudPrintProxy;
        }
    }
}
