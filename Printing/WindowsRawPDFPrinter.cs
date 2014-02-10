using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class WindowsRawPDFPrinter : JobPrinter
    {
        public override bool NeedUserAuth { get { return true; } }

        public override bool UserCanPrint(string username)
        {
            return WindowsIdentityStore.HasWindowsIdentity(username);
        }

        public override void Print(CloudPrintJob job)
        {
            WindowsRawPrintJobInfo ji = new WindowsRawPrintJobInfo
            {
                JobName = job.JobTitle,
                UserName = job.Username,
                PrinterName = job.Printer.Name,
                RawPrintData = File.ReadAllBytes(job.GetPrintDataFile()),
                RunAsUser = true
            };

            WindowsRawPrinter.PrintRaw(ji);
        }
    }
}
