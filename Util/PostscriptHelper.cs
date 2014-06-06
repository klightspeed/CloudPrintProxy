using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;
using System.IO;
using System.Reflection;

namespace TSVCEO.CloudPrint.Util
{
    public static class PostscriptHelper
    {
        public static byte[] FromPDF(byte[] PDFData)
        {
            MemoryStream stdin = new MemoryStream(PDFData);
            MemoryStream stdout = new MemoryStream();
            MemoryStream stderr = new MemoryStream();

            int retval = Util.ProcessHelper.RunProcess(
                stdin,
                stdout,
                stderr,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\\poppler",
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\\poppler\\pdftops.exe",
                new string[] { "-", "-" }
            );

            if (retval != 0)
            {
                throw new InvalidOperationException(String.Format("pstopdf returned status code {0}\n\n{1}", retval, Encoding.UTF8.GetString(stderr.ToArray())));
            }

            return stdout.ToArray();
        }

        public static string EscapePostscriptString(string str)
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

        public static IEnumerable<string> SetPageDeviceCommand(PrintTicket ticket)
        {
            yield return "<<";

            double width = (ticket.PageMediaSize.Width ?? (210 * 96)) * 72.0 / 96.0;
            double height = (ticket.PageMediaSize.Height ?? (297 * 96)) * 72.0 / 96.0;

            yield return "/PageSize";
            yield return "[";
            yield return width.ToString();
            yield return height.ToString();
            yield return "]";

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

    }
}
