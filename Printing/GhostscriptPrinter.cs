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

        protected void PrintData(string username, PrintTicket ticket, string printername, string tempfile, string jobname, string datafile, string driver)
        {
            using (Ghostscript gs = new Ghostscript())
            {
                if (tempfile != null && driver != null)
                {
                    gs.ProcessData(ticket, tempfile, datafile, driver, null, null);

                    WindowsRawPrintJobInfo jobinfo = new WindowsRawPrintJobInfo
                    {
                        JobName = jobname,
                        PrinterName = printername,
                        UserName = username,
                        RawPrintData = File.ReadAllBytes(tempfile),
                        RunAsUser = true
                    };

                    WindowsRawPrinter.PrintRaw(jobinfo);
                }
                else
                {
                    gs.PrintData(username, ticket, printername, jobname, datafile, new string[] { });
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
            string printDataFile = job.GetPrintDataFile();
            string printOutputFile = printDataFile + ".raw";
            string printerDriver = Config.GhostscriptPrinterDrivers[job.Printer.Name];
            PrintData(job.Username, printTicket, job.Printer.Name, printOutputFile, job.JobTitle, printDataFile, printerDriver);

            if (File.Exists(printOutputFile))
            {
#if !DEBUG
                File.Delete(printOutputFile);
#endif
            }
        }

        #endregion
    }
}
