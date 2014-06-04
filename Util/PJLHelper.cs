using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Util
{
    public static class PJLHelper
    {
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
