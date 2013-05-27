using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TSVCEO.CloudPrint.Util
{
    public abstract class Logger
    {
        private static Logger LoggerInstance { get; set; }
        private static LogLevel MinLogLevel { get; set; }

        public static void SetLogger(Logger logger, LogLevel minLoglevel)
        {
            LoggerInstance = logger;
            MinLogLevel = minLoglevel;
        }

        public static void Log(LogLevel level, string message, params object[] args)
        {
            if (LoggerInstance == null)
            {
                LoggerInstance = new ConsoleLogger();
                MinLogLevel = LogLevel.Info;
            }

            if (level >= MinLogLevel)
            {
                LoggerInstance.LogMessage(level, String.Format(message, args));
            }
        }

        public abstract void LogMessage(LogLevel level, string message);
    }
}
