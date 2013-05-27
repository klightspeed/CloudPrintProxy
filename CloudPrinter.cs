using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace TSVCEO.CloudPrint
{
    [DataContract]
    public class CloudPrinter
    {
        [DataMember] public virtual string Name { get; protected set; }
        [DataMember] public virtual string Description { get; protected set; }
        [DataMember] public virtual string Status { get; protected set; }
        [DataMember] public virtual string Capabilities { get; protected set; }
        [DataMember] public virtual string Defaults { get; protected set; }
        [DataMember] public virtual string CapsHash { get; protected set; }
        [DataMember] public virtual string PrinterID { get; protected set; }

        public virtual void SetPrinterID(string id) { throw new NotImplementedException(); }
    }
}
