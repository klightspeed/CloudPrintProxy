using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Printing;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class PopplerPostscriptPrinter : JobPrinter
    {
        #region Protected Methods

        protected void Print(CloudPrintJob job, bool runAsUser, bool usePJL, Dictionary<string, string> pjljobattribs, Dictionary<string, string> pjlsettings)
        {
            PrintTicket ticket = job.GetPrintTicket();
            PaginatedPrintData pagedjob = PostscriptHelper.FromPDF(job.GetPrintData(), ticket);

            if (usePJL)
            {
                pagedjob.Prologue = PJLHelper.GetPJL(pjljobattribs, pjlsettings, "POSTSCRIPT").Concat(pagedjob.Prologue).ToArray();
            }

            WindowsRawPrintJob pj = new WindowsRawPrintJob
            {
                PagedData = pagedjob,
                JobName = job.JobTitle,
                PrinterName = job.Printer.Name,
                UserName = job.Username,
                PrintTicket = ticket,
                RunAsUser = runAsUser
            };

            pj.Print();
        }

        #endregion

        #region Public Methods / Properties

        public override bool NeedUserAuth { get { return true; } }

        public override bool UserCanPrint(string username)
        {
            return WindowsIdentityStore.HasWindowsIdentity(username);
        }

        public override void Print(CloudPrintJob job)
        {
            Print(job, true, false, null, null);
        }

        #endregion
    }
}
