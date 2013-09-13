using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.InfoServer.Models;
using TSVCEO.CloudPrint.Util;
using System.Xml.Linq;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class StatusController : XHtmlController
    {
        protected CloudPrintProxy PrintProxy
        {
            get
            {
                return Request.GetPrintProxy();
            }
        }

        public HttpResponseMessage Get()
        {
            return Html(
                Head("Print Proxy Status"),
                Body(
                    H1("Print Proxy Status"),
                    new XElement("h2", "Users with print jobs waiting"),
                    new XElement("table",
                        new XAttribute("border", "1"),
                        new XElement("thead",
                            new XElement("tr",
                                new XElement("th", "Username"),
                                new XElement("th", "Logged In?")
                            )
                        ),
                        new XElement("tbody",
                            PrintProxy.GetQueuedJobs().GroupBy(j => j.Username).OrderBy(j => j.Key).Select(j => 
                                new XElement("tr",
                                    new XElement("td", j.Key),
                                    new XElement("td", WindowsIdentityStore.HasWindowsIdentity(j.Key) ? "Yes" : "No")
                                )
                            )
                        )
                    )
                )
            );
        }
    }
}
