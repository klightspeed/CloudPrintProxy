using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.ServiceProcess;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Service
{
    [DesignerCategory("Code")]
    public abstract class ServiceBase : System.ServiceProcess.ServiceBase
    {

        public ServiceBase()
        {
            this.CanPauseAndContinue = IsOverriden((Action)OnPause) && IsOverriden((Action)OnContinue);
            this.CanStop = IsOverriden((Action)OnStop);
            this.CanShutdown = IsOverriden((Action)OnShutdown);
            this.CanHandlePowerEvent = IsOverriden((Func<PowerBroadcastStatus, bool>)OnPowerEvent);
            this.CanHandleSessionChangeEvent = IsOverriden((Action<SessionChangeDescription>)OnSessionChange);
        }

        public void Install()
        {
            using (var servicemanager = NativeServiceManager.Open())
            {
                using (var service = servicemanager.CreateService(this.ServiceName, this.ServiceName, "\"" + Assembly.GetExecutingAssembly().Location + "\" -service", ServiceRights.AllAccess))
                {
                    service.Start(new string[] { });
                }
            }
        }

        public void Uninstall()
        {
            using (var servicemanager = NativeServiceManager.Open())
            {
                using (var service = servicemanager.OpenService(this.ServiceName, ServiceRights.AllAccess))
                {
                    service.Stop();
                    service.Delete();
                }
            }
        }

        public void RunService()
        {
            Logger.SetLogger(new NtEventLogger(this.EventLog), LogLevel.Info);

            ServiceBase.Run(this);
        }

        public abstract void RunStandalone(params string[] args);

        public void Run(params string[] args)
        {
            if (args.Length == 1 || args[0].Length >= 2)
            {
                if ("-install".StartsWith(args[0]))
                {
                    Install();
                    return;
                }
                else if ("-uninstall".StartsWith(args[0]))
                {
                    Uninstall();
                    return;
                }
                else if ("-console".StartsWith(args[0]))
                {
                    RunStandalone();
                    return;
                }
                else if ("-service".StartsWith(args[0]))
                {
                    RunService();
                    return;
                }
            }

            Console.WriteLine("Usage: {0} <-i|-u|-c>", Assembly.GetExecutingAssembly().Location);
            Console.WriteLine();
            Console.WriteLine("-i{nstall}      Install service");
            Console.WriteLine("-u{ninstall}    Uninstall service");
            Console.WriteLine("-c{onsole}      Run standalone");
        }

        private bool IsOverriden(MethodInfo method)
        {
            var name = method.Name;
            var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
            var mi = this.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
            return mi.DeclaringType == this.GetType();
        }

        private bool IsOverriden(Delegate action)
        {
            return IsOverriden(action.Method);
        }
    }
}
