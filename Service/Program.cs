using System;

namespace TSVCEO.CloudPrint.Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            var service = new GoogleCloudPrintProxyService();
            service.Run(args);
        }
    }
}
