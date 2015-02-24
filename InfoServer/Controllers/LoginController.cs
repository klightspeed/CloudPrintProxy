using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.Util;
using TSVCEO.CloudPrint.InfoServer.Filters;
using System.Xml.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Globalization;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    [AllowAnonymous]
    public class LoginController : XHtmlController
    {
        protected XElement LoginForm(string username)
        {
            return new XElement("form",
                new XAttribute("Action", Url.Route("default", new { controller = "Login" })),
                new XAttribute("method", "post"),
                P(
                    new XElement("label", new XAttribute("for", "username"), "Username:"),
                    new XElement("input", new XAttribute("type", "text"), new XAttribute("name", "username"), new XAttribute("value", username))
                ),
                P(
                    new XElement("label", new XAttribute("for", "password"), "Password:"),
                    new XElement("input", new XAttribute("type", "password"), new XAttribute("name", "password"))
                ),
                P(new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Login")))
            );
        }

        protected HttpResponseMessage Page(params object[] elements)
        {
            return Html(
                Head("Authenticate with Print Proxy"),
                Body(
                    H1("Authenticate with Print Proxy"),
                    elements
                )
            );
        }

        protected Dictionary<string, byte[]> ProcessFormData(byte[] data)
        {
            Dictionary<string, byte[]> fields = new Dictionary<string, byte[]>();
            string fieldname = null;
            List<byte> field = new List<byte>();

            for (int pos = 0; pos <= data.Length; pos++)
            {
                byte b = pos < data.Length ? data[pos] : (byte)0;

                if (b == '&' || pos == data.Length)
                {
                    if (fieldname != null)
                    {
                        fields[fieldname] = field.ToArray();
                    }

                    fieldname = null;
                    field.Clear();
                }
                else if (b == '=' && fieldname == null)
                {
                    fieldname = Encoding.UTF8.GetString(field.ToArray());
                    field.Clear();
                }
                else if (b == '+')
                {
                    field.Add((byte)' ');
                }
                else if (b == '%')
                {
                    if (pos >= data.Length - 2)
                    {
                        break;
                    }

                    int v;

                    if (!Int32.TryParse("0x" + (char)data[pos + 1] + (char)data[pos + 2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture.NumberFormat, out v))
                    {
                        break;
                    }

                    field.Add((byte)v);
                }
                else
                {
                    field.Add(b);
                }
            }

            return fields;
        }

        public HttpResponseMessage Get()
        {
            return Page(LoginForm(""));
        }

        public HttpResponseMessage Get(string username)
        {
            return Page(LoginForm(username));
        }

        public HttpResponseMessage Post(HttpRequestMessage msg)
        {
            Task<byte[]> datatask = msg.Content.ReadAsByteArrayAsync();
            datatask.Wait();
            byte[] data = datatask.Result;
            Dictionary<string, byte[]> fields = ProcessFormData(data);
            string username = Encoding.UTF8.GetString(fields["username"]);
            byte[] passdata = fields["password"];

            if (WindowsAuthorizationFilter.Authenticate(msg.GetSession(), username, passdata))
            {
                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Found);
                string redirecturi = Url.Route("default", new { controller = "Home" });
                response.Headers.Location = new Uri(Request.RequestUri, redirecturi);
                return response;
            }
            else
            {
                return Get(username);
            }
        }
    }
}
