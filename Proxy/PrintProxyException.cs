using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Proxy
{
    public class PrintProxyException : InvalidOperationException
    {
        public PrintProxyException(string message)
            : base(message)
        {
        }

        public PrintProxyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
