using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TSVCEO.CloudPrint.Util
{
    public class NtEventLogger : Logger
    {
        protected EventLog EventLog { get; set; }

        public NtEventLogger(EventLog log)
        {
            this.EventLog = log;
        }

        public override void LogMessage(LogLevel level, string message)
        {
            EventLogEntryType entrytype;
            
            if (level <= LogLevel.Info)
            {
                entrytype = EventLogEntryType.Information;
            }
            else if (level == LogLevel.Warning)
            {
                entrytype = EventLogEntryType.Warning;
            }
            else
            {
                entrytype = EventLogEntryType.Error;
            }

            EventLog.WriteEntry(message, entrytype);
        }
    }
}
