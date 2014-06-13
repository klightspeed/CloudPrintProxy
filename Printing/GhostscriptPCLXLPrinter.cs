using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Printing;
using System.Reflection;
using Microsoft.Win32;
using System.Security.AccessControl;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class GhostscriptPCLXLPrinter : JobPrinter
    {

        #region constructor / destructor

        public GhostscriptPCLXLPrinter()
        {
        }

        ~GhostscriptPCLXLPrinter()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        protected PaginatedPrintData ProcessPCL(byte[] rawdata, Dictionary<string, string> pjljobattribs, PrintTicket ticket)
        {
            PCLXLPrintJob pcljob = new PCLXLPrintJob(rawdata);
            return new PaginatedPrintData
            {
                Prologue = PJLHelper.GetPJL(pjljobattribs, ticket, "PCLXL").Concat(pcljob.Prologue).ToArray(),
                PageData = pcljob.PageData.ToArray(),
                Epilogue = pcljob.Epilogue.Concat(PJLHelper.GetEndJobPJL()).ToArray(),
            };
        }

        protected void PrintData(string username, PrintTicket ticket, string printername, string jobname, byte[] data, Dictionary<string, string> pjljobattribs)
        {
            using (Ghostscript gs = new Ghostscript())
            {
                string driver = ticket.OutputColor == OutputColor.Color ? "pxlcolor" : "pxlmono";

                byte[] pcldata = gs.ProcessData(ticket, data, driver, null, null);

                PaginatedPrintData pcljob = ProcessPCL(pcldata, pjljobattribs, ticket);
                WindowsRawPrintJob job = new WindowsRawPrintJob
                {
                    PagedData = pcljob,
                    JobName = jobname,
                    PrinterName = printername,
                    UserName = username,
                    RunAsUser = true
                };

                job.Print();
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
            PrintData(job.Username, printTicket, job.Printer.Name, job.JobTitle, printData, null);
        }

        #endregion
    }
}
