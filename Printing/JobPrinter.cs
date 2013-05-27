using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Printing
{
    public abstract class JobPrinter : IDisposable
    {
        public abstract void Print(CloudPrintJob job);

        public static JobPrinter Create<T>() where T: JobPrinter, new()
        {
            return new T();
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
