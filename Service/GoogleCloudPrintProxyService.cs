using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using TSVCEO.CloudPrint.Printing;
using TSVCEO.CloudPrint.Proxy;
using TSVCEO.CloudPrint.InfoServer;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Service
{
    [DesignerCategory("Code")]
    public class GoogleCloudPrintProxyService : ServiceBase
    {
        protected ManualResetEvent Stopped;
        protected IPrintJobProcessor PrintProcessor;
        protected CloudPrintInfoServer InfoServer;
        protected CloudPrintProxy PrintProxy;

        public GoogleCloudPrintProxyService()
        {
            this.ServiceName = "TSVCEO_CloudPrint";
            this.Stopped = new ManualResetEvent(false);
        }

        protected override void OnStart(string[] args)
        {
            this.Stopped.Reset();
            Logger.Log(LogLevel.Info, "Starting service");
            PrintProcessor = new WindowsPrintJobProcessor();
            PrintProcessor.Start();

            PrintProxy = new CloudPrintProxy(PrintProcessor, (p) => Stop());

            InfoServer = new CloudPrintInfoServer(Config.UserAuthHttpPort, PrintProxy);
            InfoServer.Start();

            if (PrintProxy.IsRegistered)
            {
                PrintProxy.Start(true);
            }
            Logger.Log(LogLevel.Info, "Service started");
        }

        protected override void OnStop()
        {
            Logger.Log(LogLevel.Info, "Stopping service");
            if (InfoServer != null)
            {
                InfoServer.Dispose();
                InfoServer = null;
            }

            if (PrintProxy != null)
            {
                PrintProxy.Dispose();
                PrintProxy = null;
            }

            if (PrintProcessor != null)
            {
                PrintProcessor.Dispose();
                PrintProcessor = null;
            }
            Logger.Log(LogLevel.Info, "Service stopped");
            this.Stopped.Set();
        }

        public override void RunStandalone(params string[] args)
        {
            Logger.SetLogger(new ConsoleLogger(), LogLevel.Debug);
            OnStart(args);
            Stopped.WaitOne();
        }
    }
}
