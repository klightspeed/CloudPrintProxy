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
            List<byte> pagesetup = PostscriptHelper.SetPageDeviceCommand(ticket).SelectMany(w =>
            {
                List<byte> wb = Encoding.ASCII.GetBytes(w).ToList();
                wb.Add((byte)' ');
                return wb;
            }).ToList();
            pagesetup.Add((byte)'\n');

            byte[] psdata = PostscriptHelper.FromPDF(File.ReadAllBytes(job.GetPrintDataFile()));

            bool inprologue = true;
            List<byte[]> pages = new List<byte[]>();
            List<byte> prologue = new List<byte>();
            byte[] epilogue = null;

            if (usePJL)
            {
                prologue.AddRange(PJLHelper.GetPJL(pjljobattribs, pjlsettings, "POSTSCRIPT"));
            }

            byte[] pdfsetup = Encoding.ASCII.GetBytes("false pdfSetup");
            byte[] pagesep = Encoding.ASCII.GetBytes("%%Page:");
            byte[] trailer = Encoding.ASCII.GetBytes("%%Trailer");
            int start = 0;

            for (int i = 0; i < psdata.Length; i++)
            {
                if (i == 0 || psdata[i - 1] == '\n')
                {
                    if (inprologue && i < psdata.Length - pdfsetup.Length && pdfsetup.Select((v, j) => psdata[i + j] == v).All(v => v))
                    {
                        byte[] _prologue = new byte[i - start];
                        Array.Copy(psdata, start, _prologue, 0, i - start);
                        prologue.AddRange(_prologue);
                        prologue.AddRange(pagesetup);
                        i += pdfsetup.Length;
                        start = i;
                    }
                    else if (i < psdata.Length - pagesep.Length && pagesep.Select((v, j) => psdata[i + j] == v).All(v => v))
                    {
                        if (inprologue)
                        {
                            byte[] _prologue = new byte[i - start];
                            Array.Copy(psdata, start, _prologue, 0, i - start);
                            prologue.AddRange(_prologue);
                            inprologue = false;
                            start = i;
                        }
                        else
                        {
                            byte[] page = new byte[i - start];
                            Array.Copy(psdata, start, page, 0, i - start);
                            pages.Add(page);
                            start = i;
                        }
                    }
                    else if (i < psdata.Length - trailer.Length && trailer.Select((v, j) => psdata[i + j] == v).All(v => v))
                    {
                        byte[] page = new byte[i - start];
                        Array.Copy(psdata, start, page, 0, i - start);
                        pages.Add(page);
                        epilogue = new byte[psdata.Length - i];
                        Array.Copy(psdata, i, epilogue, 0, psdata.Length - i);
                        break;
                    }
                }
            }

            string prologuestr = Encoding.ASCII.GetString(prologue.ToArray());
            string[] pagestr = pages.Select(p => Encoding.ASCII.GetString(p)).ToArray();
            string epiloguestr = Encoding.ASCII.GetString(epilogue);

            WindowsRawPrinter.PrintRaw(new WindowsRawPrintJobInfo
            {
                Prologue = prologue.ToArray(),
                PageData = pages.ToArray(),
                Epilogue = epilogue,
                JobName = job.JobTitle,
                PrinterName = job.Printer.Name,
                UserName = job.Username,
                PrintTicket = ticket,
                RunAsUser = runAsUser
            });
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
