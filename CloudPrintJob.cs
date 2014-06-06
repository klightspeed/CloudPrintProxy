using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Printing;
using System.IO;

namespace TSVCEO.CloudPrint
{
    [DataContract]
    public class CloudPrintJob
    {
        [DataMember] public virtual CloudPrinter Printer { get; protected set; }
        [DataMember] public virtual string JobID { get; protected set; }
        [DataMember] public virtual string TicketUrl { get; protected set; }
        [DataMember] public virtual string FileUrl { get; protected set; }
        [DataMember] public virtual string ContentType { get; protected set; }
        [DataMember] public virtual string OwnerId { get; protected set; }
        [DataMember] public virtual string JobTitle { get; protected set; }
        [DataMember] public virtual string Username { get; protected set; }
        [DataMember] public virtual string Domain { get; protected set; }
        [DataMember] public virtual DateTime CreateTime { get; protected set; }
        [DataMember] public virtual DateTime UpdateTime { get; protected set; }
        [DataMember] public virtual CloudPrintJobStatus Status { get; protected set; }
        [DataMember] public virtual string ErrorCode { get; protected set; }
        [DataMember] public virtual string ErrorMessage { get; protected set; }
        [DataMember] public virtual bool DeliveryAttempted { get; protected set; }

        public virtual void SetStatus(CloudPrintJobStatus status) { throw new NotImplementedException(); }
        public virtual void SetError(string ErrorCode, string ErrorMessage) { throw new NotImplementedException(); }
        public virtual PrintTicket GetPrintTicket() { throw new NotImplementedException(); }
        public virtual byte[] GetPrintData() { throw new NotImplementedException(); }
        public virtual void SetDeliveryAttempted() { throw new NotImplementedException(); }
    }
}
