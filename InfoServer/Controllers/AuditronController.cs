using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Xml.Linq;
using TSVCEO.CloudPrint.Util;
using TSVCEO.CloudPrint.Printing;

namespace TSVCEO.CloudPrint.InfoServer.Controllers
{
    public class AuditronController : XHtmlController
    {
        protected HttpResponseMessage GetUserId(string userid, params object[] errors)
        {
            string[] usernames = AuditronPostscriptPrinter.GetAllUserIds().Where(kvp => kvp.Value == userid).Select(kvp => kvp.Key).ToArray();

            return Html(
                Head("Xerox Usernames mapped to Printer Code " + userid),
                Body(
                    new XElement("p", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Auditron" })), "Back to Printer Code list")),
                    new XElement("p", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Home" })), "Back to Cloud Print Server")),
                    errors,
                    new XElement("form",
                        new XAttribute("action", Url.Route("default", new { controller = "Auditron" })),
                        new XAttribute("method", "post"),
                        new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "Delete")),
                        new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "UserId"), new XAttribute("value", userid)),
                        new XElement("ul",
                            usernames.Select(u => new XElement("li",
                                new XElement("input", new XAttribute("type", "checkbox"), new XAttribute("name", "user_" + u)),
                                new XElement("label", new XAttribute("for", "user_" + u), u)
                            ))
                        ),
                        new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Delete"))
                    ),
                    new XElement("form",
                        new XAttribute("action", Url.Route("default", new { controller = "Auditron" })),
                        new XAttribute("method", "post"),
                        new XElement("fieldset",
                            new XElement("legend", "Add Users to Printer Code"),
                            new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "AddUser")),
                            new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "UserId"), new XAttribute("value", userid)),
                            new XElement("label", new XAttribute("for", "Usernames"), "Username(s):"),
                            new XElement("input", new XAttribute("type", "text"), new XAttribute("name", "Usernames")),
                            new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Add Username(s)"))
                        )
                    )
                )
            );
        }

        protected HttpResponseMessage GetUserIds(params object[] errors)
        {
            Dictionary<string, string[]> userids = AuditronPostscriptPrinter.GetAllUserIds().GroupBy(kvp => kvp.Value).ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToArray());

            return Html(
                Head("Xerox Username to Printer Code mapping"),
                Body(
                    new XElement("p", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Home" })), "Back to Cloud Print Server")),
                    new XElement("dl",
                        userids.SelectMany(uid => new object[]
                        {
                            new XElement("dt", "UserID: ", new XElement("a", new XAttribute("href", Url.Route("default", new { controller = "Auditron", userid = uid.Key })), uid.Key)),
                            new XElement("dd", "Username(s): ", String.Join(", ", uid.Value))
                        })
                    ),
                    new XElement("form",
                        new XAttribute("action", Url.Route("default", new { controller = "Auditron" })),
                        new XAttribute("method", "post"),
                        new XElement("fieldset",
                            new XElement("legend", "Map UserId"),
                            new XElement("input", new XAttribute("type", "hidden"), new XAttribute("name", "Action"), new XAttribute("value", "AddUserId")),
                            new XElement("p",
                                new XElement("label", new XAttribute("for", "UserId"), "Printer Code:"),
                                new XElement("input", new XAttribute("type", "text"), new XAttribute("name", "UserId"))
                            ),
                            new XElement("p",
                                new XElement("label", new XAttribute("for", "Usernames"), "Username(s):"),
                                new XElement("input", new XAttribute("type", "text"), new XAttribute("name", "Usernames"))
                            ),
                            new XElement("input", new XAttribute("type", "submit"), new XAttribute("value", "Add Printer Code"))
                        )
                    )
                )
            );
        }

        protected HttpResponseMessage Delete(FormDataCollection form)
        {
            foreach (string username in form.Where(kvp => kvp.Key.StartsWith("user_")).Select(kvp => kvp.Key.Substring(5)))
            {
                AuditronPostscriptPrinter.DeleteUser(username);
            }

            return GetUserId(form.Get("UserId"));
        }

        protected string[] CSVToList(string csv)
        {
            return csv.Split(',', ';', ' ').Where(s => s != "").ToArray();
        }

        protected HttpResponseMessage AddUsers(string userid, string[] usernames)
        {
            foreach (string username in usernames)
            {
                AuditronPostscriptPrinter.CreateUser(username, userid);
            }

            return GetUserId(userid);
        }

        protected HttpResponseMessage AddUserId(string userid, string[] usernames)
        {
            if (usernames.Length != 0)
            {
                foreach (string username in usernames)
                {
                    AuditronPostscriptPrinter.CreateUser(username, userid);
                }

                return GetUserIds();
            }
            else
            {
                return GetUserId(userid);
            }
        }

        public HttpResponseMessage Get(string userid)
        {
            bool isadmin = WindowsIdentityStore.IsUserAdmin(Session["username"]);

            if (isadmin)
            {
                return GetUserId(userid);
            }
            else
            {
                return Forbidden();
            }
        }

        public HttpResponseMessage Get()
        {
            bool isadmin = WindowsIdentityStore.IsUserAdmin(Session["username"]);

            if (isadmin)
            {
                return GetUserIds();
            }
            else
            {
                return Forbidden();
            }
        }

        [HttpPost]
        public HttpResponseMessage Post(FormDataCollection form)
        {
            bool isadmin = WindowsIdentityStore.IsUserAdmin(Session["username"]);

            if (isadmin)
            {
                switch (form.Get("Action"))
                {
                    case "Delete": return Delete(form);
                    case "AddUsers": return AddUsers(form.Get("UserId"), CSVToList(form.Get("Usernames")));
                    case "AddUserId": return AddUserId(form.Get("UserId"), CSVToList(form.Get("Usernames")));
                }
            }

            return Forbidden();
        }
    }
}
