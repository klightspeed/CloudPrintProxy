using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Util
{
    public class ConsoleLogger : Logger
    {
        public override void LogMessage(LogLevel level, string message)
        {
            if (level >= LogLevel.Warning)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.Out.WriteLine(message);
            }
        }
    }
}
