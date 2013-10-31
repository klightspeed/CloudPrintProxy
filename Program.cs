using System;
using TSVCEO.CloudPrint.Printing;
using TSVCEO.CloudPrint.Service;

namespace TSVCEO.CloudPrint
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "-print")
            {
                return WindowsRawPrinter.PrintRaw_Child(Console.In, Console.Out, Console.Error);
            }
            else
            {
                var service = new GoogleCloudPrintProxyService();
                return service.Run(args);
            }
        }
    }
}
