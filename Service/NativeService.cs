using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Service
{
    [Flags]
    public enum StandardRights : int
    {
        Delete = 0x10000,
        ReadControl = 0x20000,
        WriteDac = 0x40000,
        WriteOwner = 0x80000,
        AllAccess = 0xF0000
    }

    [Flags]
    public enum ServiceManagerRights : int
    {
        Connect = 0x0001,
        CreateService = 0x0002,
        EnumerateService = 0x0004,
        Lock = 0x0008,
        QueryLockStatus = 0x0010,
        ModifyBootConfig = 0x0020,
        AllAccess = StandardRights.AllAccess | Connect | CreateService | EnumerateService | Lock | QueryLockStatus | ModifyBootConfig
    }

    [Flags]
    public enum ServiceRights : int
    {
        QueryConfig = 0x0001,
        ChangeConfig = 0x0002,
        QueryStatus = 0x0004,
        EnumerateDependents = 0x0008,
        Start = 0x0010,
        Stop = 0x0020,
        PauseContinue = 0x0040,
        Interrogate = 0x0080,
        UserDefinedControl = 0x0100,
        Delete = StandardRights.Delete,
        AllAccess = StandardRights.AllAccess | QueryConfig | ChangeConfig | QueryStatus | EnumerateDependents | Start | Stop | PauseContinue | Interrogate | UserDefinedControl
    }

    public enum ServiceControl : int
    {
        Stop = 1,
        Pause = 2,
        Continue = 3,
        Interrogate = 4,
        Shutdown = 5,
        ParamChange = 6,
        NetBindAdd = 7,
        NetBindRemove = 8,
        NetBindEnable = 9,
        NetBindDisable = 10,
        DeviceEvent = 11,
        HardwareProfileChange = 12,
        PowerEvent = 13,
        SessionChange = 14,
        PreShutdown = 15,
        TimeChange = 16
    }

    public enum ServiceState : int
    {
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7
    }

    [Flags]
    public enum ServiceType : int
    {
        KernelDriver = 0x00000001,
        FilesystemDriver = 0x00000002,
        OwnProcess = 0x00000010,
        ShareProcess = 0x00000020,
        InteractiveProcess = 0x00000100
    }

    [Flags]
    public enum ServiceControlsAccepted : int
    {
        Stop = 0x00000001,
        PauseContinue = 0x00000002,
        Shutdown = 0x00000004,
        ParamChange = 0x00000008,
        NetBindChange = 0x00000010,
        HardwareProfileChange = 0x00000020,
        PowerEvent = 0x00000040,
        SessionChange = 0x00000080,
        PreShutdown = 0x00000100,
        TimeChange = 0x00000200
    }

    public enum ServiceStartType : int
    {
        BootStart = 0,
        SystemStart = 1,
        AutoStart = 2,
        DemandStart = 3,
        Disabled = 4
    }

    public enum ServiceErrorControl : int
    {
        Ignore = 0,
        Normal = 1,
        Severe = 2,
        Critical = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public ServiceType dwServiceType;
        public ServiceState dwCurrentState;
        public ServiceControlsAccepted dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
        public int dwProcessId;
        public int dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceTableEntry
    {
        public string lpServiceName;
        public ServiceMain lpServiceProc;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    public delegate void ServiceMain(int argc, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0),In] string[] argv);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    public delegate int ServiceHandlerEx(ServiceControl dwControl, int dwEventType, IntPtr lpEventData, IntPtr lpContext);

    public class NativeServiceManager : SafeHandleZeroOrMinusOneIsInvalid
    {
        protected NativeServiceManager() : base(true) { }

        #region interop

        [DllImport("advapi32.dll", SetLastError = true)]
        protected static extern NativeServiceManager OpenSCManager(
            string lpMachineName,
            string lpDatabaseName,
            ServiceManagerRights dwDesiredAccess
        );

        [DllImport("advapi32.dll")]
        protected static extern bool CloseServiceHandle(
            IntPtr hSCObject
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        protected static extern NativeService CreateService(
            NativeServiceManager hSCManager,
            string lpServiceName,
            string lpDisplayName,
            ServiceRights dwDesiredAccess,
            ServiceType dwServiceType,
            ServiceStartType dwStartType,
            ServiceErrorControl dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        protected static extern NativeService OpenService(
            NativeServiceManager hSCManager,
            string lpServiceName,
            ServiceRights dwDesiredAccess
        );

        #endregion

        protected override bool ReleaseHandle()
        {
            return CloseServiceHandle(this.handle);
        }

        public static NativeServiceManager Open()
        {
            NativeServiceManager handle = OpenSCManager(null, null, ServiceManagerRights.Connect | ServiceManagerRights.CreateService);
            if (handle.IsClosed || handle.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
            return handle;
        }

        public NativeService OpenService(string name, ServiceRights rights)
        {
            if (!(this.IsInvalid || this.IsClosed))
            {
                NativeService service = OpenService(this, name, rights);
                if (service.IsClosed || service.IsInvalid)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                return service;
            }
            else
            {
                throw new ObjectDisposedException("NativeServiceManager");
            }
        }

        public NativeService CreateService(string name, string displayname, string cmdline, ServiceRights rights)
        {
            if (!(this.IsInvalid || this.IsClosed))
            {
                Logger.Log(LogLevel.Debug, "CreateService({0},{1},{2},{3},{4})", this.handle, name, displayname, cmdline, rights);
                NativeService service = CreateService(this, name, displayname, rights, ServiceType.OwnProcess, ServiceStartType.AutoStart, ServiceErrorControl.Normal, cmdline, null, IntPtr.Zero, null, null, null);

                if (service.IsClosed || service.IsInvalid)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                return service;
            }
            else
            {
                throw new ObjectDisposedException("NativeServiceManager");
            }
        }
    }

    public class NativeService : SafeHandleZeroOrMinusOneIsInvalid
    {
        public NativeService() : base(true) { }

        protected ServiceStatus status;

        public ServiceType dwServiceType { get { return status.dwServiceType; } }
        public ServiceState dwCurrentState { get { return status.dwCurrentState; } }
        public ServiceControlsAccepted dwControlsAccepted { get { return status.dwControlsAccepted; } }
        public int Win32ExitCode { get { return status.dwWin32ExitCode; } }
        public int ServiceSpecificExitCode { get { return status.dwServiceSpecificExitCode; } }
        public int CheckPoint { get { return status.dwCheckPoint; } }
        public int WaitHint { get { return status.dwWaitHint; } }
        public int ProcessId { get { return status.dwProcessId; } }

        #region interop

        [DllImport("advapi32.dll", SetLastError = true)]
        protected static extern bool DeleteService(
            NativeService hService
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        protected static extern bool ControlService(
            NativeService hService,
            ServiceControl dwControl,
            out ServiceStatus lpServiceStatus
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        protected static extern bool StartService(
            NativeService hService,
            uint dwNumServiceArgs,
            string[] lpServiceArgVectors
        );

        [DllImport("advapi32.dll")]
        protected static extern bool CloseServiceHandle(
            IntPtr hSCObject
        );

        #endregion

        protected override bool ReleaseHandle()
        {
            return CloseServiceHandle(this.handle);
        }

        public void Delete()
        {
            if (!(this.IsInvalid || this.IsClosed))
            {
                bool status = DeleteService(this);

                if (!status)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
            else
            {
                throw new ObjectDisposedException("NativeService");
            }
        }

        public bool Start(string[] args)
        {
            return StartService(this, (uint)args.Length, args);
        }

        public bool Stop()
        {
            return ControlService(this, ServiceControl.Stop, out status);
        }

        public bool Pause()
        {
            return ControlService(this, ServiceControl.Pause, out status);
        }

        public bool Continue()
        {
            return ControlService(this, ServiceControl.Continue, out status);
        }

        public bool Interrogate()
        {
            return ControlService(this, ServiceControl.Interrogate, out status);
        }

    }


#if false
    public abstract class OldNativeServiceDispatcher
    {
        private const int ERROR_CALL_NOT_IMPLEMENTED = 120;

        private GCHandle GCHandle;
        private IntPtr DispatcherHandle;
        private ManualResetEvent Stopped;

        public string ServiceName { get; protected set; }
        public ServiceControlsAccepted ControlsAccepted { get; protected set; }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool StartServiceCtrlDispatcher(ServiceTableEntry[] lpServiceTable);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RegisterServiceCtrlHandlerEx(string lpServiceName, ServiceHandlerEx lpHandlerProc, IntPtr lpContext);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr hServiceStatus, ref ServiceStatus lpServiceStatus);

        public OldNativeServiceDispatcher()
        {
            ControlsAccepted = ServiceControlsAccepted.Stop |
                               ((IsOverriden((Action)OnPause) && IsOverriden((Action)OnContinue)) ? ServiceControlsAccepted.PauseContinue : 0) |
                               (IsOverriden((Action<int, IntPtr>)OnPowerEvent) ? ServiceControlsAccepted.PowerEvent : 0) |
                               (IsOverriden((Action)OnShutdown) ? ServiceControlsAccepted.Shutdown : 0);
            Stopped = new ManualResetEvent(true);
        }

        public void Install()
        {
            using (var servicemanager = NativeServiceManager.Open())
            {
                using (var service = servicemanager.CreateService(this.ServiceName, this.ServiceName, "\"" + Assembly.GetExecutingAssembly().Location + "\"", ServiceRights.AllAccess))
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

        public bool RunService()
        {
            ServiceTableEntry[] serviceTable = new ServiceTableEntry[]
            {
                new ServiceTableEntry { lpServiceName = this.ServiceName, lpServiceProc = this.ServiceMain },
                new ServiceTableEntry { lpServiceName = null, lpServiceProc = null }
            };

            return StartServiceCtrlDispatcher(serviceTable);
        }

        public void RunStandalone(params string[] args)
        {
            Logger.SetLogger(new ConsoleLogger(), LogLevel.Debug);
            ServiceMain(args.Length, args);

            Stopped.WaitOne();
        }

        protected void Stop()
        {
            OnControl(() => OnStop(), ServiceState.StopPending, ServiceState.Stopped);
        }

        protected void SetServiceStatus(ServiceState currentState, int win32ExitCode = 0, int serviceSpecificExitCode = 0, int checkPoint = 0, int waitHint = 30000)
        {
            if (DispatcherHandle != IntPtr.Zero)
            {
                ServiceStatus status = new ServiceStatus
                {
                    dwServiceType = ServiceType.OwnProcess,
                    dwCurrentState = currentState,
                    dwControlsAccepted = this.ControlsAccepted,
                    dwWin32ExitCode = win32ExitCode,
                    dwServiceSpecificExitCode = serviceSpecificExitCode,
                    dwCheckPoint = checkPoint,
                    dwWaitHint = waitHint
                };

                SetServiceStatus(DispatcherHandle, ref status);
            }

            if (currentState == ServiceState.Stopped)
            {
                Stopped.Set();
            }
            else
            {
                Stopped.Reset();
            }
        }

        private void ServiceMain(int argc, string[] argv)
        {
            GCHandle = GCHandle.Alloc(this);
            DispatcherHandle = RegisterServiceCtrlHandlerEx(ServiceName, new ServiceHandlerEx(ServiceHandler), GCHandle.ToIntPtr(GCHandle));
            if (DispatcherHandle == IntPtr.Zero || DispatcherHandle == new IntPtr(-1))
            {
                Logger.Log(LogLevel.Error, "Invalid handle received from RegisterServiceCtrlHandlerEx");
                return;
            }
            else
            {
                Logger.Log(LogLevel.Debug, "RegisterServiceCtrlHandlerEx returned handle {0}", DispatcherHandle.ToString());
            }

            Logger.SetLogger(new NtEventLogger(), LogLevel.Info);

            int status = OnControl(() => this.OnStart(argv), ServiceState.StartPending, ServiceState.Running);

            if (status != 0)
            {
                SetServiceStatus(ServiceState.Stopped, win32ExitCode: status);
            }
        }

        private bool IsOverriden(MethodInfo method)
        {
            var name = method.Name;
            var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
            var mi = this.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
            return mi.DeclaringType != typeof(NativeServiceDispatcher);
        }

        private bool IsOverriden(Delegate action)
        {
            return IsOverriden(action.Method);
        }

        private bool IsOverriden(Expression<Action> expression)
        {
            var methodcall = expression.Body as MethodCallExpression;
            return methodcall != null && IsOverriden(methodcall.Method);
        }

        private int OnControl(Expression<Action> expression, ServiceState starting = 0, ServiceState completed = 0, ServiceState failed = 0)
        {
            if (IsOverriden(expression))
            {
                if (starting != 0)
                {
                    SetServiceStatus(starting);
                }

                try
                {
                    var action = expression.Compile();
                    action();
                }
                catch (Win32Exception ex)
                {
                    if (failed != 0)
                    {
                        SetServiceStatus(failed);
                    }

                    return ex.NativeErrorCode;
                }

                if (completed != 0)
                {
                    SetServiceStatus(completed);
                }

                return 0;
            }
            else
            {
                return ERROR_CALL_NOT_IMPLEMENTED;
            }
        }

        private static int ServiceHandler(ServiceControl control, int eventType, IntPtr eventData, IntPtr context)
        {
            Logger.Log(LogLevel.Debug, "Received control {0} eventType {1} eventData {2} context {3}", control.ToString(), eventType, eventData.ToString(), context.ToString());

            OldNativeServiceDispatcher service = (OldNativeServiceDispatcher)GCHandle.FromIntPtr(context).Target;

            try
            {
                switch (control)
                {
                    case ServiceControl.Interrogate:
                        return 0;
                    case ServiceControl.Stop:
                        return service.OnControl(() => service.OnStop(), ServiceState.StopPending, ServiceState.Stopped);
                    case ServiceControl.Pause:
                        return service.OnControl(() => service.OnPause(), ServiceState.PausePending, ServiceState.Paused);
                    case ServiceControl.Continue:
                        return service.OnControl(() => service.OnContinue(), ServiceState.ContinuePending, ServiceState.Running);
                    case ServiceControl.PowerEvent:
                        return service.OnControl(() => service.OnPowerEvent(eventType, eventData));
                    case ServiceControl.Shutdown:
                        return service.OnControl(() => service.OnShutdown());
                    default:
                        return service.OnControl(() => service.OnCustomEvent(control, eventType, eventData));
                }
            }
            catch (Win32Exception ex)
            {
                Logger.Log(LogLevel.Warning, "Win32 error {0} handling control {1}", ex.NativeErrorCode, control.ToString());
                return ex.NativeErrorCode;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Uncaught exception {0}\n{1}", ex.Message, ex.ToString());
                return 31;
            }
        }

        protected abstract void OnStart(string[] args);

        protected abstract void OnStop();

        protected virtual void OnPause()
        {
            throw new NotSupportedException();
        }

        protected virtual void OnContinue()
        {
            throw new NotSupportedException();
        }

        protected virtual void OnPowerEvent(int eventType, IntPtr eventData)
        {
            throw new NotSupportedException();
        }

        protected virtual void OnShutdown()
        {
            throw new NotSupportedException();
        }

        protected virtual void OnCustomEvent(ServiceControl control, int eventType, IntPtr eventData)
        {
            throw new NotSupportedException();
        }
    }
#endif
}
