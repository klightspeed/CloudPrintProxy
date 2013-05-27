using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TSVCEO.CloudPrint.Util
{
    public class Win32Dll : SafeHandleZeroOrMinusOneIsInvalid
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        public T GetDelegate<T>(string name)
        {
            if (!(typeof(T).IsSubclassOf(typeof(Delegate))))
            {
                throw new InvalidCastException("Type T must be Delegate");
            }

            return (T)((object)Marshal.GetDelegateForFunctionPointer(GetProcAddress(handle, name), typeof(T)));
        }

        public Win32Dll(string name)
            : base(true)
        {
            handle = LoadLibrary(name);
            if (IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        protected override bool ReleaseHandle()
        {
            return FreeLibrary(handle);
        }
    }
}
