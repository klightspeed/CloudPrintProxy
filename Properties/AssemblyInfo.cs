using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("TSVCEO.CloudPrint")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Townsville Catholic Education Office")]
[assembly: AssemblyProduct("TSVCEO.CloudPrint")]
[assembly: AssemblyCopyright("Copyright © Townsville Catholic Education Office 2013")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("0446bf28-f0cf-40ba-9294-05737748e8d7")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TSVCEO.CloudPrint
{
    public static class AssemblyInfo
    {
        public static readonly string CompanyName = GetCompanyName();
        public static readonly string ProductName = GetProductName();
        public static readonly string Version = GetVersion();

        private static string GetCompanyName()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), true).OfType<AssemblyCompanyAttribute>().Single().Company;
        }

        private static string GetProductName()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), true).OfType<AssemblyProductAttribute>().Single().Product;
        }

        private static string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true).OfType<AssemblyFileVersionAttribute>().Single().Version;
        }
    }
}
