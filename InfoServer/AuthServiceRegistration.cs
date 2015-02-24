using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.InfoServer
{
    public class AuthServiceRegistration
    {
        protected CloudPrintProxy PrintProxy;
        protected Timer AuthRegistrationTimer;
        protected Dictionary<string, DateTime> PrinterRegistrationTimes;

        public AuthServiceRegistration(CloudPrintProxy proxy)
        {
            this.PrintProxy = proxy;
            this.PrinterRegistrationTimes = new Dictionary<string, DateTime>();
            this.AuthRegistrationTimer = new Timer(RegisterAuthService);
        }

        public void Start()
        {
            AuthRegistrationTimer.Change(new TimeSpan(0, 0, 30), new TimeSpan(0, 1, 0));
        }

        protected void RegisterAuthService(object state)
        {
            foreach (CloudPrinter printer in PrintProxy.Queues)
            {
                if (!PrinterRegistrationTimes.ContainsKey(printer.PrinterID) ||
                    PrinterRegistrationTimes[printer.PrinterID] < DateTime.Now)
                {
                    HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(Config.AuthRegistrationURL);

                    req.Proxy = null;

                    if (Config.WebProxyHost != null)
                    {
                        req.Proxy = new WebProxy(Config.WebProxyHost, Config.WebProxyPort);
                    }

                    try
                    {
                        byte[] responsedata = HTTPHelper.SendUrlEncodedPostData(req, new
                        {
                            printerid = printer.PrinterID,
                            authserver = "http://" + Config.UserAuthHost + ":" + Config.UserAuthHttpPort
                        });

                        PrinterRegistrationTimes[printer.PrinterID] = DateTime.Now.AddHours(1);
                    }
                    catch (WebException)
                    {
                        PrinterRegistrationTimes[printer.PrinterID] = DateTime.Now.AddMinutes(1);
                    }
                }
            }
        }
    }
}
