using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;
using System.IO;

namespace TSVCEO.CloudPrint.Proxy
{
    public class CloudPrintJobImpl : CloudPrintJob
    {
        protected readonly CloudPrintProxy _Proxy;
        protected readonly dynamic _JobAttributes;
        protected readonly CloudPrinter _Printer;

        public override CloudPrinter Printer { get { return _Printer; } }
        public override string JobID { get { return _JobAttributes.id; } }
        public override string ContentType { get { return _JobAttributes.contentType; } }
        public override string FileUrl { get { return _JobAttributes.fileUrl; } }
        public override string OwnerId { get { return _JobAttributes.ownerId; } }
        public override string TicketUrl { get { return _JobAttributes.ticketUrl; } }
        public override string JobTitle { get { return _JobAttributes.title; } }
        public override string Username { get { return OwnerId.Split(new char[] { '@' }, 2).ToArray()[0]; } }
        public override string Domain { get { return OwnerId.Split(new char[] { '@' }, 2).ToArray()[1]; } }
        public override CloudPrintJobStatus Status { get; protected set; }
        public override string ErrorCode { get; protected set; }
        public override string ErrorMessage { get; protected set; }

        public override void SetStatus(CloudPrintJobStatus status)
        {
            this.Status = status;
            this.ErrorCode = null;
            this.ErrorMessage = null;
            _Proxy.UpdatePrintJob(this);
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

        public override Stream GetPrintDataStream()
        {
            return _Proxy.GetPrintDataStream(this);
        }

        public CloudPrintJobImpl(CloudPrintProxy proxy, CloudPrinter printer, dynamic job)
        {
            this._Proxy = proxy;
            this._Printer = printer;
            this._JobAttributes = job;
        }
    }
}
