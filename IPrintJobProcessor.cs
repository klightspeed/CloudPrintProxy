using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint
{
    public interface IPrintJobProcessor : IDisposable
    {
        void AddJob(CloudPrintJob job);
        void AddJobs(IEnumerable<CloudPrintJob> jobs);
        IEnumerable<CloudPrinter> GetPrintQueues();
        IEnumerable<CloudPrintJob> GetQueuedJobs(string username);
        IEnumerable<CloudPrintJob> GetQueuedJobs();
        void RestartDeferredPrintJobs(string username);
    }
}
