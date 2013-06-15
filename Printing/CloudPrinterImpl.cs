using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Printing;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace TSVCEO.CloudPrint.Printing
{
    public class CloudPrinterImpl : CloudPrinter
    {
        public override string Status { get { return new LocalPrintServer().GetPrintQueue(Name).QueueStatus.ToString(); } }

        protected static string GetMD5Hash(byte[] data)
        {
            MD5 md5 = MD5.Create();
            md5.Initialize();
            md5.TransformFinalBlock(data, 0, data.Length);
            return new String(md5.Hash.SelectMany((b) => b.ToString("X2").ToCharArray()).ToArray());
        }

        public override void SetPrinterID(string id)
        {
            if (this.PrinterID == null)
            {
                this.PrinterID = id;
            }
            else
            {
                throw new InvalidOperationException("Printer ID already set");
            }
        }

        public CloudPrinterImpl(PrintQueue queue)
        {
            PrintTicket defaults = queue.DefaultPrintTicket.Clone();
            defaults.OutputColor = OutputColor.Monochrome;
            defaults.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);

            this.Name = queue.FullName;
            this.Description = queue.Description;
            this.Capabilities = XDocument.Load(queue.GetPrintCapabilitiesAsXml()).ToString();
            this.Defaults = new StreamReader(defaults.GetXmlStream(), Encoding.UTF8, false).ReadToEnd();
            this.CapsHash = GetMD5Hash(Encoding.UTF8.GetBytes(Capabilities));
        }
    }
}
