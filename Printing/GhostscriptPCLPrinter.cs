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
    public class GhostscriptPCLPrinter : JobPrinter
    {

        #region constructor / destructor

        public GhostscriptPCLPrinter()
        {
        }

        ~GhostscriptPCLPrinter()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        protected WindowsRawPrintJobInfo ProcessPCL(byte[] rawdata, Dictionary<string, string> pjljobattribs, Dictionary<string, string> pjlsettings)
        {
            PCLPrintJob pcljob = new PCLPrintJob(rawdata);
            return new WindowsRawPrintJobInfo
            {
                Prologue = PJLHelper.GetPJL(pjljobattribs, pjlsettings, "PCLXL").Concat(pcljob.Prologue).ToArray(),
                PageData = pcljob.PageData.ToArray(),
                Epilogue = pcljob.Epilogue.Concat(PJLHelper.GetEndJobPJL()).ToArray(),
            };
        }

        protected string GetStapling(Stapling? stapling)
        {
            switch (stapling ?? Stapling.None)
            {
                case Stapling.StapleTopLeft: return "TOPLEFT";
                case Stapling.StapleTopRight: return "TOPRIGHT";
                case Stapling.StapleBottomLeft: return "BOTTOMLEFT";
                case Stapling.StapleBottomRight: return "BOTTOMRIGHT";
                case Stapling.StapleDualLeft: return "LEFTDUAL";
                case Stapling.StapleDualRight: return "RIGHTDUAL";
                case Stapling.StapleDualTop: return "TOPDUAL";
                case Stapling.StapleDualBottom: return "BOTTOMDUAL";
                case Stapling.SaddleStitch: return "SADDLE";
                default: return "NONE";
            }
        }

        protected void PrintData(string username, PrintTicket ticket, string printername, string jobname, byte[] data, Dictionary<string, string> pjljobattribs)
        {
            using (Ghostscript gs = new Ghostscript())
            {
                Dictionary<string, string> pjlsettings = new Dictionary<string,string>
                {
                    { "DUPLEX", ticket.Duplexing == Duplexing.OneSided ? "OFF" : "ON" },
                    { "BINDING", ticket.Duplexing == Duplexing.TwoSidedShortEdge ? "SHORTEDGE" : "LONGEDGE" },
                    { "COPIES", (ticket.CopyCount ?? 1).ToString() },
                    { "RENDERMODE", ticket.OutputColor == OutputColor.Color ? "COLOR" : "GRAYSCALE" },
                    { "STAPLE", GetStapling(ticket.Stapling) },
                    { "PUNCH", "NONE" }
                };

                string driver = ticket.OutputColor == OutputColor.Color ? "pxlcolor" : "pxlmono";

                byte[] postscript = PostscriptHelper.FromPDF(data);
                byte[] pcldata = gs.ProcessData(ticket, postscript, driver, null, null);

                WindowsRawPrintJobInfo jobinfo = ProcessPCL(pcldata, pjljobattribs, pjlsettings);

                jobinfo.JobName = jobname;
                jobinfo.PrinterName = printername;
                jobinfo.UserName = username;
                jobinfo.RunAsUser = true;

                WindowsRawPrinter.PrintRaw(jobinfo);
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
