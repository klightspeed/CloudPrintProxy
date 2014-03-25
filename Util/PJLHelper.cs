using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Util
{
    public static class PJLHelper
    {
        public static byte[] GetPJL(Dictionary<string, string> attribs, string language)
        {
            return Encoding.ASCII.GetBytes(
                "\x1E%-12345X".ToArray()
                .Concat("@PJL JOB MODE=PRINTER\r\n")
                .Concat(attribs == null ? new char[] { } : attribs.SelectMany(kvp => "@PJL SET JOBATTR=\"@".ToArray().Concat(kvp.Key).Concat("=").Concat(kvp.Value).Concat("\"\r\n")))
                .Concat("@PJL ENTER LANGUAGE=")
                .Concat(language)
                .Concat("\r\n")
                .ToArray()
            );
        }
    }
}
