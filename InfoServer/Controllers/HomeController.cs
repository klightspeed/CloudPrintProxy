using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Xml.Linq;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.InfoServer.Filters;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class HomeController : XHtmlController
    {
        protected CloudPrintProxy PrintProxy
        {
            get
            {
                return Request.GetPrintProxy();
            }
        }

        protected HttpResponseMessage Page(params object[] elements)
        {
            return Html(
                Head("Cloud Print Server"),
                Body(
                    H1("Cloud Print Server"),
                    new XElement("div", "Welcome to the Cloud Print Server, " + Session["username"]),
                    elements
                )
            );

        }

        public HttpResponseMessage Get()
        {
            if (!PrintProxy.IsRegistered)
            {
                return Page(
                    new XElement("div", "This cloud print proxy is not yet registered"),
                    new XElement("div", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Register" })), "Register the print proxy"))
                );
            }
            else
            {
                return Page();
            }
        }
    }
}
