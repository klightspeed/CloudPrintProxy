using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Printing;

namespace TSVCEO.CloudPrint.Printing
{
    public class PopplerPostscriptPrinter : JobPrinter
    {
        protected string EscapePostscriptString(string str)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");

            foreach (char c in str)
            {
                if (c >= 0377)
                {
                    sb.Append("\\377");
                }
                else if (c < ' ' || c >= 0x7F)
                {
                    sb.AppendFormat("\\{0}", Convert.ToString((int)c, 8));
                }
                else if (c == '\\' || c == '(' || c == ')')
                {
                    sb.AppendFormat("\\{0}", c);
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append(")");
            return sb.ToString();
        }

        protected IEnumerable<string> SetPageDeviceCommand(PrintTicket ticket)
        {
            yield return "<<";

            double width = (ticket.PageMediaSize.Width ?? (210 * 96)) * 72.0 / 96.0;
            double height = (ticket.PageMediaSize.Height ?? (297 * 96)) * 72.0 / 96.0;

            /*
            yield return "/PageSize";
            yield return "[";
            yield return width.ToString();
            yield return height.ToString();
            yield return "]";
             */

            if (ticket.PageMediaType != null && ticket.PageMediaType != PageMediaType.Unknown)
            {
                yield return "/MediaType";
                yield return EscapePostscriptString(ticket.PageMediaType.ToString());
            }

            if (ticket.InputBin != null && ticket.InputBin == InputBin.Manual)
            {
                yield return "/ManualFeed";
                yield return "true";
            }

            if (ticket.Collation != null && ticket.Collation != Collation.Unknown)
            {
                yield return "/Collate";
                yield return ticket.Collation == Collation.Collated ? "true" : "false";
            }

            if (ticket.CopyCount != null)
            {
                yield return "/NumCopies";
                yield return ticket.CopyCount.ToString();
            }

            if (ticket.Duplexing != null && ticket.Duplexing != Duplexing.Unknown)
            {
                yield return "/Duplex";
                yield return ticket.Duplexing == Duplexing.OneSided ? "false" : "true";
                yield return "/Tumble";
                yield return ticket.Duplexing == Duplexing.TwoSidedShortEdge ? "true" : "false";
            }

            /*
            if (ticket.PageResolution.X != null && ticket.PageResolution.Y != null)
            {
                yield return "/HWResolution";
                yield return "[";
                yield return ticket.PageResolution.X.ToString();
                yield return ticket.PageResolution.Y.ToString();
                yield return "]";
            }
             */

            if (ticket.PageOrientation != null && ticket.PageOrientation != PageOrientation.Unknown)
            {
                int orientation = 0;
                bool pagesizelandscape = width > height;

                switch (ticket.PageOrientation)
                {
                    case PageOrientation.Portrait: orientation = pagesizelandscape ? 3 : 0; break;
                    case PageOrientation.Landscape: orientation = pagesizelandscape ? 0 : 1; break;
                    case PageOrientation.ReversePortrait: orientation = pagesizelandscape ? 2 : 1; break;
                    case PageOrientation.ReverseLandscape: orientation = pagesizelandscape ? 2 : 3; break;
                }

                yield return "/Orientation";
                yield return orientation.ToString();
            }

            if (ticket.OutputColor != null && ticket.OutputColor != OutputColor.Unknown && ticket.OutputColor != OutputColor.Color)
            {
                yield return "/ProcessColorModel";
                yield return "/DeviceGray";
                yield return "/BitsPerPixel";
                yield return (ticket.OutputColor == OutputColor.Grayscale) ? "8" : "1";
            }

            yield return ">>";
            yield return "setpagedevice";
        }

        public override void Print(CloudPrintJob job)
        {
            MemoryStream stdin = new MemoryStream();
            MemoryStream stdout = new MemoryStream();
            MemoryStream stderr = new MemoryStream();

            PrintTicket ticket = job.GetPrintTicket();
            List<byte> pagesetup = this.SetPageDeviceCommand(ticket).SelectMany(w =>
            {
                List<byte> wb = Encoding.ASCII.GetBytes(w).ToList();
                wb.Add((byte)' ');
                return wb;
            }).ToList();
            pagesetup.Add((byte)'\n');

            int retval = Util.ProcessHelper.RunProcess(
                stdin,
                stdout,
                stderr,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\\poppler\\pdftops.exe",
                new string[] { job.GetPrintDataFile(), "-" }
            );

            if (retval != 0)
            {
                throw new InvalidOperationException(String.Format("pstopdf returned status code {0}\n\n{1}", retval, Encoding.UTF8.GetString(stderr.ToArray())));
            }

            byte[] psdata = stdout.ToArray();

            bool inprologue = true;
            List<byte[]> pages = new List<byte[]>();
            List<byte> prologue = new List<byte>();
            byte[] epilogue = null;

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

            WindowsRawPrinter.PrintRawAsUser(new WindowsRawPrintJobInfo
            {
                Prologue = prologue.ToArray(),
                PageData = pages.ToArray(),
                Epilogue = epilogue,
                JobName = job.JobTitle,
                PrinterName = job.Printer.Name,
                UserName = job.Username,
                PrintTicket = ticket
            });
        }
    }
}
