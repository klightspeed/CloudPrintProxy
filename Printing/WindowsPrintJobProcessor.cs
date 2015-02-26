using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TSVCEO.CloudPrint.Util;
using System.Security.Principal;
using System.Printing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace TSVCEO.CloudPrint.Printing
{
    public class WindowsPrintJobProcessor : IPrintJobProcessor
    {
        private const int RequeueTime = 15000;

        #region interop methods

        [Flags]
        private enum PRINTER_ENUM
        {
            DEFAULT     = 0x00000001,
            LOCAL       = 0x00000002,
            CONNECTIONS = 0x00000004,
            NAME        = 0x00000008,
            REMOTE      = 0x00000010,
            SHARED      = 0x00000020,
            NETWORK     = 0x00000040
        }

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct PRINTER_INFO_4
        {
            public string PrinterName;
            public string ServerName;
            public uint Attributes;
        }

        [DllImport("winspool.drv", CharSet=CharSet.Unicode)]
        private static extern bool EnumPrinters(PRINTER_ENUM Flags, string Name, int Level, IntPtr pPrinterEnum, int cbBuf, out int pcbNeeded, out int pcReturned);

        #endregion

        #region private properties

        private int QueuedPrintJobs;

        private bool Disposed { get; set; }
        private bool Running { get; set; }
        private ConcurrentQueue<CloudPrintJob> PrintJobQueue { get; set; }
        private ConcurrentDictionary<string, ConcurrentQueue<CloudPrintJob>> UserDeferredJobs { get; set; }
        private Thread PrintQueueProcessorThread { get; set; }
        private Thread PrintServerDispatcherThread { get; set; }
        private Dispatcher PrintServerDispatcher { get; set; }
        private CancellationTokenSource CancelTokenSource { get; set; }
        private Dictionary<string, PrintQueue> PrintQueues { get; set; }

        #endregion

        #region constructor / destructor

        public WindowsPrintJobProcessor()
        {
            PrintJobQueue = new ConcurrentQueue<CloudPrintJob>();
            UserDeferredJobs = new ConcurrentDictionary<string, ConcurrentQueue<CloudPrintJob>>();
            CancelTokenSource = new CancellationTokenSource();
            PrintQueues = new Dictionary<string, PrintQueue>();
            PrintServerDispatcherThread = new Thread(new ThreadStart(PrintServerThreadProc));
            PrintServerDispatcherThread.Start();
        }

        ~WindowsPrintJobProcessor()
        {
            Dispose(false);
        }

        #endregion

        #region private methods

        private void Dispose(bool disposing)
        {
            if (CancelTokenSource != null)
            {
                CancelTokenSource.Cancel();

                if (PrintQueueProcessorThread != null)
                {
                    PrintQueueProcessorThread.Join();
                }

                CancelTokenSource.Dispose();
            }

            PrintQueueProcessorThread = null;
            CancelTokenSource = null;

            if (PrintServerDispatcherThread != null)
            {
                if (PrintServerDispatcher != null)
                {
                    PrintServerDispatcher.InvokeShutdown();
                }

                PrintServerDispatcherThread.Join();
            }

            PrintServerDispatcher = null;
            PrintServerDispatcherThread = null;

            if (disposing)
            {
                Disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private IEnumerable<CloudPrintJob> DequeueDeferredPrintQueueJobs(string username)
        {
            CloudPrintJob job;
            ConcurrentQueue<CloudPrintJob> queue;
            if (UserDeferredJobs.TryGetValue(username, out queue))
            {
                while (queue.TryDequeue(out job))
                {
                    yield return job;
                }
            }
        }

        private void SendEmail(string email, string subject, string body)
        {
            SmtpClient client = new SmtpClient();

            if (client.Host != null)
            {
                MailMessage message = new MailMessage();

                if (message.From == null)
                {
                    message.From = new MailAddress(Config.OAuthEmail);
                }

                message.To.Add(email);
                message.Subject = subject;
                message.Body = body;

                Logger.Log(LogLevel.Info, "Sending email\n\nFrom: {0}\nTo: {1}\nSubject: {2}\n\n{3}",
                    message.From,
                    email,
                    subject,
                    message.Body
                );

                client.Send(message);
            }
        }

        private void NotifyUserToLogin(CloudPrintJob job)
        {
            try
            {
                string message = String.Format(
                    "You have sent a job to cloud printer \"{0}\" on {1} at {2} on {3}\n\nPlease log into http://{4}:{5}/ to allow this job (and any others) to be printed.",
                    job.Printer.Name,
                    Environment.MachineName,
                    job.CreateTime.ToLocalTime().ToString("hh:mm tt"),
                    job.CreateTime.ToLocalTime().ToString("dd MMM yyyy"),
                    Config.UserAuthHost,
                    Config.UserAuthHttpPort
                );
                string subject = String.Format("Please log in to enable cloud printing on {0}", Environment.MachineName);
                SendEmail(job.OwnerId, subject, message);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "Error notifying user to log in\n\n{0}", ex.ToString());
            }
        }

        private void ProcessPrintJob(CloudPrintJob job)
        {
            if (!WindowsIdentityStore.IsAcceptedDomain(job.Domain))
            {
                Logger.Log(LogLevel.Debug, "Job {0} deferred because {1}@{2} is not in an accepted domain", job.JobID, job.Username, job.Domain);
            }
            else
            {
                using (JobPrinter printer = Activator.CreateInstance(job.Printer.GetJobPrinterType()) as JobPrinter)
                {
                    if (!printer.UserCanPrint(job.Username))
                    {
                        if (printer.NeedUserAuth)
                        {
                            Logger.Log(LogLevel.Debug, "Job {0} deferred because {1} has not logged in", job.JobID, job.Username);

                            if (job.CreateTime > DateTime.Now.AddDays(-3) && !job.DeliveryAttempted)
                            {
                                NotifyUserToLogin(job);
                            }
                        }

                        job.SetDeliveryAttempted();
                        UserDeferredJobs.GetOrAdd(job.Username, new ConcurrentQueue<CloudPrintJob>()).Enqueue(job);
                    }
                    else
                    {
                        job.SetStatus(CloudPrintJobStatus.IN_PROGRESS);
                        try
                        {
                            Logger.Log(LogLevel.Info, "Starting job {0}", job.JobID);
                            printer.Print(job);
                            Logger.Log(LogLevel.Info, "Job {0} Finished", job.JobID);
                            job.SetStatus(CloudPrintJobStatus.DONE);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Info, "Job {0} in error:\nException:\n{1}\n{2}", job.JobID, ex.Message, ex.ToString());
                            job.SetStatus(CloudPrintJobStatus.QUEUED);
                            //job.SetError(ex.GetType().Name, ex.Message);
                        }
                    }
                }
            }
        }

        private void PrintServerThreadProc()
        {
            Logger.Log(LogLevel.Debug, "Starting print queue processor thread");
            PrintServerDispatcher = Dispatcher.CurrentDispatcher;
            Dispatcher.Run();
        }

        private void DoProcessQueuedPrintJobs(CancellationToken cancelToken)
        {

            Logger.Log(LogLevel.Debug, "Processing queued jobs");
            do
            {
                try
                {
                    CloudPrintJob job;
                    while (!cancelToken.IsCancellationRequested && PrintJobQueue.TryDequeue(out job))
                    {
                        if (job.Status == CloudPrintJobStatus.QUEUED)
                        {
                            ProcessPrintJob(job);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, "ProcessQueuedPrintJobs task caught exception {0}\n{1}", ex.Message, ex.ToString());
                }
            }
            while (!cancelToken.IsCancellationRequested && Interlocked.Decrement(ref QueuedPrintJobs) != 0);
            Logger.Log(LogLevel.Debug, "Done");
        }

        private void ProcessQueuedPrintJobs()
        {
            if (Interlocked.Increment(ref QueuedPrintJobs) == 1)
            {
                PrintQueueProcessorThread = new Thread(new ThreadStart(() => DoProcessQueuedPrintJobs(CancelTokenSource.Token)));
                PrintQueueProcessorThread.Start();
            }
        }

        private void ThrowIfDisposed()
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        private IEnumerable<string> EnumerateLocalPrinterNames()
        {
            int cbneeded = 0;
            int nents = 0;
            int bufsize = 0;
            IntPtr buf = IntPtr.Zero;
            PRINTER_INFO_4[] printerinfoarray;
            try
            {
                while (!EnumPrinters(PRINTER_ENUM.LOCAL | PRINTER_ENUM.SHARED, null, 4, buf, bufsize, out cbneeded, out nents))
                {
                    if (cbneeded == bufsize)
                    {
                        throw new InvalidOperationException("Unable to enumerate printers");
                    }

                    if (buf != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(buf);
                    }

                    buf = Marshal.AllocHGlobal(cbneeded);
                    bufsize = cbneeded;
                }


                printerinfoarray = new PRINTER_INFO_4[nents];

                for (int i = 0; i < nents; i++)
                {
                    printerinfoarray[i] = (PRINTER_INFO_4)Marshal.PtrToStructure(buf + i * Marshal.SizeOf(typeof(PRINTER_INFO_4)), typeof(PRINTER_INFO_4));
                }

                return printerinfoarray.Select(pi => pi.PrinterName).ToArray();
            }
            finally
            {
                if (buf != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
        }

        private IEnumerable<CloudPrinter> DoGetPrintQueues()
        {
            LocalPrintServer PrintServer = new LocalPrintServer();
            Dictionary<string, PrintQueue> queuesToDispose = new Dictionary<string, PrintQueue>(PrintQueues);

            foreach (string printername in EnumerateLocalPrinterNames())
            {
                if (PrintQueues.ContainsKey(printername))
                {
                    queuesToDispose.Remove(printername);
                }
                else
                {
                    PrintQueues.Add(printername, PrintServer.GetPrintQueue(printername));
                }
            }

            foreach (KeyValuePair<string, PrintQueue> pq_kvp in queuesToDispose)
            {
                PrintQueues.Remove(pq_kvp.Key);
                pq_kvp.Value.Dispose();
            }

            return PrintQueues.Values.Where(q => q.IsShared).Select(q => new CloudPrinterImpl(q)).ToArray();
        }


        #endregion

        #region public methods

        public void Dispose()
        {
            Dispose(true);
        }

        public void AddJob(CloudPrintJob job)
        {
            ThrowIfDisposed();

            lock (PrintJobQueue)
            {
                PrintJobQueue.Enqueue(job);
            }

            ProcessQueuedPrintJobs();
        }

        public void AddJobs(IEnumerable<CloudPrintJob> jobs)
        {
            ThrowIfDisposed();
            
            lock (PrintJobQueue)
            {
                foreach (var job in jobs)
                {
                    PrintJobQueue.Enqueue(job);
                }
            }

            ProcessQueuedPrintJobs();
        }

        public IEnumerable<CloudPrinter> GetPrintQueues()
        {
            return (IEnumerable<CloudPrinter>)PrintServerDispatcher.Invoke((Func<IEnumerable<CloudPrinter>>)DoGetPrintQueues);
        }

        public IEnumerable<CloudPrintJob> GetQueuedJobs(string username)
        {
            ConcurrentQueue<CloudPrintJob> queue;
            
            if (UserDeferredJobs.TryGetValue(username, out queue))
            {
                foreach (CloudPrintJob job in queue.AsEnumerable())
                {
                    yield return job;
                }
            }

            foreach (CloudPrintJob job in PrintJobQueue.AsEnumerable().Where(j => j.Username == username))
            {
                yield return job;
            }
        }

        public IEnumerable<CloudPrintJob> GetQueuedJobs()
        {
            foreach (KeyValuePair<string, ConcurrentQueue<CloudPrintJob>> kvp in UserDeferredJobs.AsEnumerable())
            {
                foreach (CloudPrintJob job in kvp.Value.AsEnumerable())
                {
                    yield return job;
                }
            }

            foreach (CloudPrintJob job in PrintJobQueue.AsEnumerable())
            {
                yield return job;
            }
        }

        public void RestartDeferredPrintJobs(string username)
        {
            Logger.Log(LogLevel.Debug, "Re-queueing deferred jobs for user {0}", username);
            AddJobs(DequeueDeferredPrintQueueJobs(username));
            Logger.Log(LogLevel.Debug, "Done");
        }

        #endregion
    }
}
