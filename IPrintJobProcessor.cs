using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint
{
    public interface IPrintJobProcessor : IDisposable
    {
        void Start();
        void Stop();
        void AddJob(CloudPrintJob job);
        void AddJobs(IEnumerable<CloudPrintJob> jobs);
        IEnumerable<CloudPrinter> GetPrintQueues();
    }
}
