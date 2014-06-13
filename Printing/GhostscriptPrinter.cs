using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Printing;
using Microsoft.Win32;
using System.Security.AccessControl;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class GhostscriptPrinter : JobPrinter
    {

        #region constructor / destructor

        public GhostscriptPrinter()
        {
        }

        ~GhostscriptPrinter()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        protected void PrintData(string username, PrintTicket ticket, string printername, string jobname, byte[] data, string driver)
        {
            using (Ghostscript gs = new Ghostscript())
            {
                if (driver != null)
                {
                    byte[] outdata = gs.ProcessData(ticket, data, driver, null, null);

                    WindowsRawPrintJob pj = new WindowsRawPrintJob
                    {
                        JobName = jobname,
                        PrinterName = printername,
                        UserName = username,
                        PrintData = outdata,
                        RunAsUser = true
                    };

                    pj.Print();
                }
                else
                {
                    gs.PrintData(username, ticket, printername, jobname, data, new string[] { });
                }
            }
        }

        #endregion

        #region public methods

        public override bool NeedUserAuth { get { return true; } }

        public override bool UserCanPrint(string username)
        {
            return WindowsIdentityStore.HasWindowsIdentity(username);
        }

        public override void Print(CloudPrintJob job)
        {
            PrintTicket printTicket = job.GetPrintTicket();
            byte[] printData = job.GetPrintData();
            string printerDriver = Config.GhostscriptPrinterDrivers[job.Printer.Name];
            PrintData(job.Username, printTicket, job.Printer.Name, job.JobTitle, printData, printerDriver);
        }

        #endregion
    }
}
