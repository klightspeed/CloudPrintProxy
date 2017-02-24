using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;
using System.IO;
using System.Xml.Linq;
using System.Security.AccessControl;

namespace TSVCEO.CloudPrint.Proxy
{
    public class CloudPrintJobImpl : CloudPrintJob
    {
        protected static Dictionary<string, CloudPrintJob> _PrintJobs = new Dictionary<string,CloudPrintJob>();

        protected readonly CloudPrintProxy _Proxy;
        protected readonly dynamic _JobAttributes;
        protected readonly CloudPrinter _Printer;
        protected readonly string _PrintDataFileName;
        protected readonly string _PrintDataBasename;

        public override CloudPrinter Printer { get { return _Printer ?? _Proxy.GetCloudPrinterById(_JobAttributes.printerid); } }
        public override string JobID { get { return _JobAttributes.id; } }
        public override string ContentType { get { return _JobAttributes.contentType; } }
        public override string FileUrl { get { return _JobAttributes.fileUrl; } }
        public override string OwnerId { get { return _JobAttributes.ownerId; } }
        public override string TicketUrl { get { return _JobAttributes.ticketUrl; } }
        public override string JobTitle { get { return _JobAttributes.title; } }
        public override string Username { get { return OwnerId.Split(new char[] { '@' }, 2).ToArray()[0]; } }
        public override string Domain { get { return OwnerId.Split(new char[] { '@' }, 2).ToArray()[1]; } }
        public override DateTime CreateTime { get { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Double.Parse(_JobAttributes.createTime.ToString())); } }
        public override DateTime UpdateTime { get { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Double.Parse(_JobAttributes.updateTime.ToString())); } }
        public override CloudPrintJobStatus Status { get { return Enum.Parse(typeof(CloudPrintJobStatus), _JobAttributes.status); } protected set { _JobAttributes.status = value.ToString(); } }
        public override string ErrorCode { get { return _JobAttributes.errorCode; } protected set { _JobAttributes.errorCode = value; } }
        public override string ErrorMessage { get { return _JobAttributes.message; } protected set { _JobAttributes.message = value; } }

        private void WriteJobData()
        {
            if (!File.Exists(this._PrintDataFileName))
            {
                using (Stream datastream = File.Create(this._PrintDataFileName))
                {
                    using (Stream inputstream = _Proxy.GetPrintDataStream(this))
                    {
                        inputstream.CopyTo(datastream);
                    }
                }
            }

#if DEBUG
            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(this._PrintDataFileName, this.CreateTime);
                File.SetLastWriteTime(this._PrintDataFileName, this.CreateTime);
            }
            catch
            {
            }

            Util.WindowsIdentityStore.SetFileAccess(this._PrintDataFileName, this.Username);
#endif
        }

        private void WriteJobTicket()
        {
            string filename = _PrintDataBasename + ".ticket.xml";
            
            PrintTicket ticket = this.GetPrintTicket();
            
            if (!File.Exists(filename))
            {
                using (Stream ticketstream = File.Create(filename))
                {
                    ticket.SaveTo(ticketstream);
                }
            }

#if DEBUG
            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.CreateTime);
            }
            catch
            {
            }
#endif

            Util.WindowsIdentityStore.SetFileAccess(filename, this.Username);
        }

        private void WriteJobXml()
        {
            string filename = _PrintDataBasename + ".job.xml";

            XDocument xdoc = new XDocument(
                new XElement("CloudPrintJob",
                    new XElement("ContentType", this.ContentType),
                    new XElement("Domain", this.Domain),
                    new XElement("JobID", this.JobID),
                    new XElement("JobTitle", this.JobTitle),
                    new XElement("Printer",
                        new XElement("ID", this.Printer.PrinterID),
                        new XElement("Name", this.Printer.Name)
                    ),
                    new XElement("Username", this.Username)
                )
            );

            xdoc.Save(filename);

#if DEBUG
            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.UpdateTime);
            }
            catch
            {
            }
#endif

            Util.WindowsIdentityStore.SetFileAccess(filename, this.Username);
        }

        private void WriteJobJson()
        {
            string filename = _PrintDataBasename + ".job.json";

            using (Stream jobfile = File.Create(filename))
            {
                Util.JsonHelper.WriteJson(new StreamWriter(jobfile, Encoding.UTF8), this._JobAttributes);
            }

#if DEBUG
            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.CreateTime);
            }
            catch
            {
            }
#endif

            Util.WindowsIdentityStore.SetFileAccess(filename, this.Username);
        }

        public override void SetStatus(CloudPrintJobStatus status)
        {
            this.Status = status;
            this.ErrorCode = null;
            this.ErrorMessage = null;
            _Proxy.UpdatePrintJob(this);
            WriteJobJson();
            WriteJobXml();

            if (status == CloudPrintJobStatus.DONE)
            {
                if (!Config.KeepPrintFile)
                {
                    File.Delete(_PrintDataFileName);
                }
            }
        }

        public override void SetError(string errorCode, string errorMessage)
        {
            this.Status = CloudPrintJobStatus.ERROR;
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
            _Proxy.UpdatePrintJob(this);
            WriteJobJson();
            WriteJobXml();
        }

        public override void SetDeliveryAttempted()
        {
            this.DeliveryAttempted = true;
        }

        public override PrintTicket GetPrintTicket()
        {
            if (File.Exists(_PrintDataBasename + ".ticket.xml"))
            {
                using (Stream stream = File.OpenRead(_PrintDataBasename + ".ticket.xml"))
                {
                    return new PrintTicket(stream);
                }
            }
            else
            {
                return _Proxy.GetPrintTicket(this);
            }
        }

        public override byte[] GetPrintData()
        {
            return File.ReadAllBytes(_PrintDataFileName);
        }

        public CloudPrintJobImpl(CloudPrintProxy proxy, CloudPrinter printer, dynamic job)
        {
            this._Proxy = proxy;
            this._Printer = printer;
            this._JobAttributes = job;
            string jobdirname = Path.Combine(Config.DataDirName, "PrintJobs", this.Username);
            this._PrintDataBasename = Path.Combine(jobdirname, job.id);
            this._PrintDataFileName = _PrintDataBasename + ".pdf";

            Directory.CreateDirectory(jobdirname);

            WriteJobData();
            WriteJobTicket();
            WriteJobXml();
            WriteJobJson();

            _PrintJobs[this.JobID] = this;
        }

        protected CloudPrintJobImpl(CloudPrintProxy proxy, string basename)
        {
            this._Proxy = proxy;

            using (TextReader rdr = File.OpenText(basename + ".job.json"))
            {
                _JobAttributes = Util.JsonHelper.ReadJson(rdr);
            }

            using (TextReader rdr = File.OpenText(basename + ".job.xml"))
            {
            }

            this._PrintDataBasename = basename;
            this._PrintDataFileName = basename + ".pdf";

            _PrintJobs[this.JobID] = this;
        }

        public static IEnumerable<CloudPrintJob> GetIncompletePrintJobs(CloudPrintProxy proxy)
        {
            return new List<CloudPrintJob>();

            /*
            string jobrootdirname = Path.Combine(Config.DataDirName, "PrintJobs");
            foreach (string jobpdfpath in Directory.EnumerateFiles(jobrootdirname, "*.pdf", SearchOption.AllDirectories))
            {
                string jobid = Path.GetFileNameWithoutExtension(jobpdfpath);

                if (_PrintJobs.ContainsKey(jobid))
                {
                    yield return _PrintJobs[jobid];
                }
                else
                {
                    string jobpath = Path.GetDirectoryName(jobpdfpath);
                    string basename = Path.Combine(jobpath, jobid);
                    string jobjsonpath = basename + ".job.json";
                    string jobticketpath = basename + ".ticket.xml";

                    if (File.Exists(jobjsonpath) && File.Exists(jobticketpath))
                    {
                        CloudPrintJob job = null;

                        try
                        {
                            job = new CloudPrintJobImpl(proxy, basename);
                        }
                        catch
                        {
                        }

                        if (job != null)
                        {
                            yield return job;
                        }
                    }
                }
            }
             */
        }
    }
}
