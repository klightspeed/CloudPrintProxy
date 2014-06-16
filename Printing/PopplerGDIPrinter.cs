using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.IO;
using TSVCEO.CloudPrint.Util;
using TSVCEO.CloudPrint.Util.Poppler;

namespace TSVCEO.CloudPrint.Printing
{
    [Serializable]
    public class PopplerGDIPrintJob : PrintJob
    {
        protected override void Run()
        {
            using (PopplerDocument doc = new PopplerDocument(PrintData, null))
            {
                using (GDIPrinterDeviceContext dc = new GDIPrinterDeviceContext(PrinterName, PrintTicket))
                {
                    doc.Print(dc, JobName);
                }
            }
        }
    }

    public class PopplerGDIPrinter : JobPrinter
    {
        #region constructor / destructor

        public PopplerGDIPrinter()
        {
        }

        ~PopplerGDIPrinter()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        public void Print(string username, byte[] data, string printername, string jobname, PrintTicket ticket)
        {
            PopplerGDIPrintJob pj = new PopplerGDIPrintJob
            {
                PrintData = data,
                PrinterName = printername,
                PrintTicket = ticket,
                JobName = jobname,
                RunAsUser = username != null,
                UserName = username
            };

            pj.Print();
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
            Print(job.Username, job.GetPrintData(), job.Printer.Name, job.JobTitle, job.GetPrintTicket());
        }

        #endregion
    }
}
