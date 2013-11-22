using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TSVCEO.CloudPrint.Printing
{
    public class WindowsRawPDFPrinter : JobPrinter
    {
        public override void Print(CloudPrintJob job)
        {
            WindowsRawPrintJobInfo ji = new WindowsRawPrintJobInfo
            {
                JobName = job.JobTitle,
                UserName = job.Username,
                PrinterName = job.Printer.Name,
                RawPrintData = File.ReadAllBytes(job.GetPrintDataFile())
            };

            WindowsRawPrinter.PrintRawAsUser(ji);
        }
    }
}
