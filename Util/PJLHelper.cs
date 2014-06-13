using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;

namespace TSVCEO.CloudPrint.Util
{
    public static class PJLHelper
    {
        private static string GetStapling(Stapling? stapling)
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

        public static byte[] GetPJL(Dictionary<string, string> jobattribs, PrintTicket ticket, string language)
        {
            Dictionary<string, string> pjlsettings = new Dictionary<string, string>
            {
                { "DUPLEX", ticket.Duplexing == Duplexing.OneSided ? "OFF" : "ON" },
                { "BINDING", ticket.Duplexing == Duplexing.TwoSidedShortEdge ? "SHORTEDGE" : "LONGEDGE" },
                { "COPIES", (ticket.CopyCount ?? 1).ToString() },
                { "RENDERMODE", ticket.OutputColor == OutputColor.Color ? "COLOR" : "GRAYSCALE" },
                { "STAPLE", GetStapling(ticket.Stapling) }
            };

            return GetPJL(jobattribs, pjlsettings, language);
        }

        public static byte[] GetPJL(Dictionary<string, string> jobattribs, Dictionary<string, string> pjlsettings, string language)
        {
            return Encoding.ASCII.GetBytes(
                "\x1B%-12345X".ToArray()
                .Concat("@PJL JOB MODE=PRINTER\r\n")
                .Concat(jobattribs == null ? new char[] { } : jobattribs.SelectMany(kvp => "@PJL SET JOBATTR=\"@".ToArray().Concat(kvp.Key).Concat("=").Concat(kvp.Value).Concat("\"\r\n")))
                .Concat(pjlsettings == null ? new char[] { } : pjlsettings.SelectMany(kvp => "@PJL SET ".ToArray().Concat(kvp.Key).Concat("=").Concat(kvp.Value).Concat("\r\n")))
                .Concat("@PJL ENTER LANGUAGE=")
                .Concat(language)
                .Concat("\r\n")
                .ToArray()
            );
        }

        public static byte[] GetEndJobPJL()
        {
            return Encoding.ASCII.GetBytes("\x1B%-12345X@PJL EOJ");
        }
    }
}
