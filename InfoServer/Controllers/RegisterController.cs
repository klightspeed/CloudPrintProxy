using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.InfoServer.Models;
using System.Xml.Linq;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class RegisterController : XHtmlController
    {
        protected CloudPrintProxy PrintProxy
        {
            get
            {
                return Request.GetPrintProxy();
            }
        }

        protected XElement GetAuthCodeRequestForm()
        {
            return new XElement("form",
                new XAttribute("Action", Url.Route("default", new { controller = "Register" })),
                new XAttribute("method", "post"),
                new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "GetAuthCode")),
                new XElement("label", new XAttribute("for", "Email"), "Registered under Google Account:"),
                new XElement("input", new XAttribute("type", "text"), new XAttribute("name", "Email")),
                new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Get the Authorization Code"))
            );
        }

        protected XElement GetAuthCodeRequestForm(string email)
        {
            return new XElement("form",
                new XAttribute("action", Url.Route("default", new { controller = "Register" })),
                new XAttribute("method", "post"),
                new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "GetAuthCode")),
                new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Email")),
                new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Get the Authorization Code"))
            );
        }

        protected XElement GetRegistrationRequestForm()
        {
                return new XElement("form",
                    new XAttribute("action", Url.Route("default", new { controller = "Register" })),
                    new XAttribute("method", "post"),
                    new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "RegisterProxy")),
                    new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Register the Proxy"))
                );
        }

        protected XElement GetRetryRegistrationRequestForm()
        {
            return new XElement("form",
                new XAttribute("action", Url.Route("default", new { controller = "Register" })),
                new XAttribute("method", "post"),
                new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "RegisterProxy")),
                new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Retry Registering the Proxy"))
            );
        }

        protected HttpResponseMessage Page(params object[] elements)
        {
            return Html(
                Head("Register Print Proxy"),
                Body(
                    H1("Register Print Proxy"),
                    elements
                )
            );
        }

        protected HttpResponseMessage Register()
        {
            PrintProxy.ClearAuthCode();
            string claim_url = PrintProxy.Register();
            return Page(
                new XElement("div", "Please go to ", new XElement("a", new XAttribute("href", claim_url), new XAttribute("target", "_blank"), claim_url), " to claim this print proxy."),
                new XElement("div", GetAuthCodeRequestForm())
            );
        }

        protected HttpResponseMessage GetAuthCode(string email)
        {
            if (email == null)
            {
                return Page(
                    new XElement("div", "Please enter the email address you logged in with when claiming the printer"),
                    new XElement("div", GetAuthCodeRequestForm()),
                    new XElement("div", GetRetryRegistrationRequestForm())
                );
            }
            else
            {
                string claimemail = PrintProxy.RequestAuthCode();
                if (claimemail == null)
                {
                    return Page(
                        new XElement("div", "Unable to get auth code.  Please try again."),
                        new XElement("div", GetAuthCodeRequestForm(email)),
                        new XElement("div", GetRetryRegistrationRequestForm())
                    );
                }
                else if (claimemail == email)
                {
                    PrintProxy.AcceptAuthCode(email);
                    PrintProxy.Start(true);
                    return Page(
                        new XElement("div", "Print proxy successfully registered."),
                        new XElement("div", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Home" })), "Return to Print Proxy information page"))
                    );
                }
                else
                {
                    PrintProxy.ClearAuthCode();
                    return Page(
                        new XElement("div", "Print proxy claim account did not match."),
                        new XElement("div", "A user with the email address " + claimemail + " tried to claim the print proxy."),
                        new XElement("div", GetRetryRegistrationRequestForm())
                    );
                }
            }
        }

        [HttpGet]
        public HttpResponseMessage Get()
        {
            if (!PrintProxy.IsRegistered)
            {
                return Page(
                    new XElement("div", GetRegistrationRequestForm())
                );
            }
            else
            {
                return Page(
                    new XElement("div", "This print proxy is already registered."),
                    new XElement("div", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Home" })), "Return to Print Proxy information page"))
                );
            }
        }

        [HttpPost]
        public HttpResponseMessage Post(FormDataCollection form)
        {
            if (!PrintProxy.IsRegistered)
            {
                switch (form.Get("Action"))
                {
                    case "RegisterProxy": return Register();
                    case "GetAuthCode": return GetAuthCode(form.Get("Email"));
                }
            }

            return Forbidden();
        }
    }
}