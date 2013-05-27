using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Printing;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using TSVCEO.CloudPrint.Util;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TSVCEO.CloudPrint.Printing
{
    public class GhostscriptAPI : Ghostscript
    {
        public const int e_NeedInput = -106;

        protected IntPtr Handle;

        #region interop

        [StructLayout(LayoutKind.Sequential)]
        public struct Version
        {
            public string product;
            public string copyright;
            public int revision;
            public int revisionDate;
        }

        public delegate int StdioReader(
            IntPtr caller_handle,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), In, Out]
            byte[] buf,
            int len
        );

        public delegate int StdioWriter(
            IntPtr caller_handler,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), In]
            byte[] buf,
            int len
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_revision(out Version version, int len);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_new_instance(out IntPtr instance, IntPtr caller_handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_set_stdio(IntPtr instance, StdioReader stdin_fn, StdioWriter stdout_fn, StdioWriter stderr_fn);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_init_with_args(IntPtr instance, int argc, [In, Out] string[] argv);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_run_string_begin(IntPtr instance, int user_errors, out int exit_code);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_run_string_continue(IntPtr instance, byte[] str, int length, int user_errors, out int exit_code);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_run_string_end(IntPtr instance, int user_errors, out int exit_code);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate int pgsapi_exit(IntPtr instance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate void pgsapi_delete_instance(IntPtr instance);

        protected Win32Dll gsdll;
        protected pgsapi_new_instance gsapi_new_instance;
        protected pgsapi_set_stdio gsapi_set_stdio;
        protected pgsapi_init_with_args gsapi_init_with_args;
        protected pgsapi_run_string_begin gsapi_run_string_begin;
        protected pgsapi_run_string_continue gsapi_run_string_continue;
        protected pgsapi_run_string_end gsapi_run_string_end;
        protected pgsapi_exit gsapi_exit;
        protected pgsapi_delete_instance gsapi_delete_instance;

        #endregion

        #region constructor / destructor

        public GhostscriptAPI(CloudPrintJob job)
        {
            Init();

            int errorcode = gsapi_new_instance(out Handle, IntPtr.Zero);
            if (errorcode < 0)
            {
                throw new InvalidOperationException(String.Format("Ghostscript error {0}", errorcode));
            }
        }

        ~GhostscriptAPI()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        protected void Init()
        {
            string gspath = GetGhostscriptPath(Environment.Is64BitProcess ? "gsdll64.dll" : "gsdll32.dll");

            if (gspath == null)
            {
                throw new FileNotFoundException("Unable to locate GhostScript");
            }

            gsdll = new Win32Dll(gspath);
            gsapi_new_instance = gsdll.GetDelegate<pgsapi_new_instance>("gsapi_new_instance");
            gsapi_set_stdio = gsdll.GetDelegate<pgsapi_set_stdio>("gsapi_set_stdio");
            gsapi_init_with_args = gsdll.GetDelegate<pgsapi_init_with_args>("gsapi_init_with_args");
            gsapi_run_string_begin = gsdll.GetDelegate<pgsapi_run_string_begin>("gsapi_run_string_begin");
            gsapi_run_string_continue = gsdll.GetDelegate<pgsapi_run_string_continue>("gsapi_run_string_continue");
            gsapi_run_string_end = gsdll.GetDelegate<pgsapi_run_string_end>("gsapi_run_string_end");
            gsapi_exit = gsdll.GetDelegate<pgsapi_exit>("gsapi_exit");
            gsapi_delete_instance = gsdll.GetDelegate<pgsapi_delete_instance>("gsapi_delete_instance");
        }

        protected StdioReader GetApiReader(Stream stream)
        {
            return new StdioReader((ptr, buf, len) => stream.Read(buf, 0, len));
        }

        protected StdioWriter GetApiWriter(Stream stream)
        {
            return new StdioWriter((ptr, buf, len) => { stream.Write(buf, 0, len); return len; });
        }

        protected override int RunCommandAsUser(string username, string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            using (WindowsIdentityStore.Impersonate(username))
            {
                byte[] buf = new byte[1024];
                gsapi_set_stdio(Handle, GetApiReader(stdin), GetApiWriter(stdout), GetApiWriter(stderr));
                gsapi_init_with_args(Handle, args.Length + 1, new string[] { "gs" }.Concat(args).ToArray());
                return gsapi_exit(Handle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            gsapi_delete_instance(Handle);
            Handle = IntPtr.Zero;
            gsdll.Dispose();

            base.Dispose(disposing);
        }

        #endregion

        #region public methods

        public static Version GetVersion()
        {
            using (Win32Dll gsdll = new Win32Dll(GetGhostscriptPath(Environment.Is64BitProcess ? "gsdll64.dll" : "gsdll32.dll")))
            {
                Version version;
                pgsapi_revision gsapi_revision = gsdll.GetDelegate<pgsapi_revision>("gsapi_revision");

                if (gsapi_revision(out version, Marshal.SizeOf(typeof(Version))) != 0)
                {
                    throw new InvalidOperationException("Invalid Ghostscript Version structure size");
                }

                return version;
            }
        }

        #endregion
    }
}
