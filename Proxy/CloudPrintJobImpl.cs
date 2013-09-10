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
        protected readonly CloudPrintProxy _Proxy;
        protected readonly dynamic _JobAttributes;
        protected readonly CloudPrinter _Printer;
        protected readonly string _PrintDataFileName;
        protected readonly string _PrintDataBasename;

        public override CloudPrinter Printer { get { return _Printer; } }
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
        public override CloudPrintJobStatus Status { get; protected set; }
        public override string ErrorCode { get; protected set; }
        public override string ErrorMessage { get; protected set; }

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

            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(this._PrintDataFileName, this.CreateTime);
                File.SetLastWriteTime(this._PrintDataFileName, this.CreateTime);
            }
            catch
            {
            }

            Util.WindowsIdentityStore.SetFileAccess(this._PrintDataFileName, this.Username);
        }

        private void WriteJobTicket()
        {
            string filename = _PrintDataBasename + ".ticket.xml";
            
            if (!File.Exists(filename))
            {
                using (Stream ticketstream = File.Create(filename))
                {
                    this.GetPrintTicket().SaveTo(ticketstream);
                }

            }

            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.CreateTime);
            }
            catch
            {
            }

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

            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.UpdateTime);
            }
            catch
            {
            }

            Util.WindowsIdentityStore.SetFileAccess(filename, this.Username);
        }

        private void WriteJobJson()
        {
            string filename = _PrintDataBasename + ".job.xml";

            using (Stream jobfile = File.Create(filename))
            {
                Util.JsonHelper.WriteJson(new StreamWriter(jobfile, Encoding.UTF8), this._JobAttributes);
            }

            try  /* #@$%^& thing gives an IO exception, but doesn't say what happened.  #@&^%$ */
            {
                File.SetCreationTime(filename, this.CreateTime);
                File.SetLastWriteTime(filename, this.CreateTime);
            }
            catch
            {
            }

            Util.WindowsIdentityStore.SetFileAccess(filename, this.Username);
        }

        public override void SetStatus(CloudPrintJobStatus status)
        {
            this.Status = status;
            this.ErrorCode = null;
            this.ErrorMessage = null;
            _Proxy.UpdatePrintJob(this);

            if (status == CloudPrintJobStatus.DONE)
            {
                File.Delete(_PrintDataFileName);
            }
        }

        public override void SetError(string errorCode, string errorMessage)
        {
            this.Status = CloudPrintJobStatus.ERROR;
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
            _Proxy.UpdatePrintJob(this);
        }

        public override PrintTicket GetPrintTicket()
        {
            return _Proxy.GetPrintTicket(this); 
        }

        public override string GetPrintDataFile()
        {
            return _PrintDataFileName;
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
        }
    }
}
