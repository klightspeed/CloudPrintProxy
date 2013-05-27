using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Xml.Linq;
using TSVCEO.CloudPrint.InfoServer.Filters;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public abstract class XHtmlController : ApiController
    {
        public Session Session
        {
            get
            {
                return Request.GetSession();
            }
        }

        public NameValueCollection Cookies
        {
            get
            {
                return Request.GetCookies();
            }
        }

        protected XElement Head(string title, params object[] content)
        {
            return new XElement("head", new XElement("title", title), content);
        }

        protected XElement Script(string name)
        {
            return new XElement("script",
                new XAttribute("type", "text/javascript"),
                new XAttribute("src", Url.Route("default", new { controller = "Scripts", id = name }))
            );
        }

        protected XElement Body(params object[] content)
        {
            return new XElement("body", content);
        }

        protected XElement H1(string content)
        {
            return new XElement("h1", content);
        }

        protected XElement P(params object[] content)
        {
            return new XElement("p", content);
        }
        
        protected HttpResponseMessage Html(XElement head, XElement body, HttpStatusCode status)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    new XDocument(
                        new XDocumentType("html", null, null, null),
                        new XElement("html", head, body)
                    ).ToString(),
                    Encoding.UTF8,
                    "text/html"
                )
            };
        }

        protected HttpResponseMessage Html(XElement head, XElement body)
        {
            return Html(head, body, HttpStatusCode.OK);
        }

        protected HttpResponseMessage NotFound()
        {
            return Html(
                Head("Not Found"),
                Body(
                    H1("Not Found")
                ),
                HttpStatusCode.NotFound
            );
        }

        protected HttpResponseMessage Forbidden()
        {
            return Html(
                Head("Forbidden"),
                Body(
                    H1("Forbidden")
                ),
                HttpStatusCode.Forbidden
            );
        }
    }
}