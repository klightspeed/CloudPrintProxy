using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class AuditronPostscriptPrinter : PopplerPostscriptPrinter
    {
        public override bool NeedUserAuth { get { return false; } }
        
        public override bool UserCanPrint(string username)
        {
            return UserIDMapper.GetUserId(username) != null;
        }

        public override void Print(CloudPrintJob job)
        {
            Dictionary<string, string> pjlattribs = new Dictionary<string,string>
            {
                { "LUNA", job.Username },
                { "ACNA", job.JobTitle },
                { "JOAU", UserIDMapper.GetUserId(job.Username) }
            };

            base.Print(job, false, true, pjlattribs, null);
        }
    }
}
