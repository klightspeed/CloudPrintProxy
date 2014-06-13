using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Printing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using TSVCEO.CloudPrint.Util;
using System.Reflection;

namespace TSVCEO.CloudPrint.Printing
{
    [Serializable]
    public class PrintJob
    {
        public virtual string PrinterName { get; set; }
        public virtual string JobName { get; set; }
        public virtual string UserName { get; set; }
        public virtual bool RunAsUser { get; set; }
        public virtual byte[] PrintTicketXML { get; set; }
        public virtual byte[] PrintData { get; set; }

        public PrintTicket PrintTicket { get { return new PrintTicket(new MemoryStream(PrintTicketXML)); } set { PrintTicketXML = value.GetXmlStream().ToArray(); } }

        protected virtual void Run()
        {
            throw new NotImplementedException();
        }

        private static void Serialize(Stream stream, object graph)
        {
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.CrossProcess));
            formatter.Serialize(stream, graph);
        }

        private static object Deserialize(Stream stream)
        {
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.CrossProcess));
            return formatter.Deserialize(stream);
        }

        public void Print()
        {
            if (RunAsUser)
            {
                MemoryStream stdin = new MemoryStream();
                MemoryStream stdout = new MemoryStream();
                MemoryStream stderr = new MemoryStream();

                try
                {
                    Serialize(stdin, this);
                    stdin.Flush();
                    stdin.Position = 0;

                    int retcode = WindowsIdentityStore.RunProcessAsUser(UserName, stdin, stdout, stderr, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Assembly.GetExecutingAssembly().Location, new string[] { "-print" });

                    if (retcode != 0)
                    {
                        stderr.Position = 0;
                        Logger.Log(LogLevel.Info, "Error printing file:\n{0}", System.Text.Encoding.ASCII.GetString(stderr.ToArray()));
                        throw new AggregateException((Exception)Deserialize(stderr));
                    }
                }
                finally
                {
                    stdin.Dispose();
                    stdout.Dispose();
                    stderr.Dispose();
                }
            }
            else
            {
                Run();
            }
        }

        public static int Run(Stream stdin, Stream stdout, Stream stderr)
        {
            try
            {
                PrintJob ji = (PrintJob)Deserialize(stdin);
                ji.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Serialize(stderr, ex);
                return 1;
            }
        }    
    }
}
