using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using TSVCEO.CloudPrint.Util;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Printing;

namespace TSVCEO.CloudPrint.Printing
{
    [Serializable]
    public class WindowsRawPrinterException : Exception
    {
        public WindowsRawPrinterException(Exception inner)
            : base(inner.Message, inner)
        {
        }
    }

    [Serializable]
    public class WindowsRawPrintJobInfo
    {
        public string PrinterName { get; set; }
        public string JobName { get; set; }
        public string UserName { get; set; }
        public byte[] RawPrintData { get; set; }
        public PaginatedPrintJob PagedData { get; set; }
        public byte[] PrintTicketXML { get; set; }
        public bool RunAsUser { get; set; }

        public PrintTicket PrintTicket { get { return new PrintTicket(new MemoryStream(PrintTicketXML)); } set { PrintTicketXML = value.GetXmlStream().ToArray(); } }
    }

    public class WindowsRawPrinter
    {

        public enum PRINTER_ACCESS_MASK
        {
            PRINTER_ACCESS_ADMINISTER = 4,
            PRINTER_ACCESS_USE = 8,
            PRINTER_ALL_ACCESS = 0x000F000C
        }

        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            public ushort SpecVersion;
            public ushort DriverVersion;
            public ushort Size;
            public ushort DriverExtra;
            public uint Fields;
            public short Orientation;
            public short PaperSize;
            public short PaperLength;
            public short PaperWidth;
            public short Scale;
            public short Copies;
            public short DefaultSource;
            public short PrintQuality;
            public short Color;
            public short Duplex;
            public short YResolution;
            public short TTOption;
            public short Collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FormName;
            public ushort LogPixels;
            public uint BitsPerPel;
            public uint PelsWidth;
            public uint PelsHeight;
            public uint Nup;
            public uint DisplayFrequency;
            public uint ICMMethod;
            public uint ICMIntent;
            public uint MediaType;
            public uint DitherType;
            public uint Reserved1;
            public uint Reserved2;
            public uint PanningWidth;
            public uint PanningHeight;
        }

        public struct PRINTER_DEFAULTS
        {
            public string pDatatype;
            public IntPtr pDevMode;
            public PRINTER_ACCESS_MASK DesiredAccess;

            public DEVMODE? DevMode
            {
                get
                {
                    if (pDevMode != IntPtr.Zero)
                    {
                        return (DEVMODE)Marshal.PtrToStructure(pDevMode, typeof(DEVMODE));
                    }
                    else
                    {
                        return null;
                    }
                }
                set
                {
                    if (pDevMode != null)
                    {
                        Marshal.DestroyStructure(pDevMode, typeof(DEVMODE));
                        Marshal.FreeHGlobal(pDevMode);
                        pDevMode = IntPtr.Zero;
                    }

                    if (value != null)
                    {
                        pDevMode = Marshal.AllocHGlobal(Marshal.SizeOf(value));
                        Marshal.StructureToPtr(value, pDevMode, false);
                    }
                }
            }
        }

        public struct ADDJOB_INFO_1
        {
            public string Path;
            public uint JobId;
        }

        public struct DOC_INFO_1
        {
            public string DocName;
            public string OutputFile;
            public string Datatype;
        }

        public struct JOB_INFO_2
        {
            public uint JobId;
            public string PrinterName;
            public string MachineName;
            public string UserName;
            public string Document;
            public string NotifyName;
            public string Datatype;
            public string PrintProcessor;
            public string Parameters;
            public string DriverName;
            public IntPtr pDevMode;
            public uint Status;
            public uint Priority;
            public uint Position;
            public uint StartTime;
            public uint UntilTime;
            public uint TotalPages;
            public uint Size;
            public ulong Submitted;
            public uint Time;
            public uint PagesPrinted;

            public DEVMODE? DevMode
            {
                get
                {
                    if (pDevMode != IntPtr.Zero)
                    {
                        return (DEVMODE)Marshal.PtrToStructure(pDevMode, typeof(DEVMODE));
                    }
                    else
                    {
                        return null;
                    }
                }
                set
                {
                    if (pDevMode != null)
                    {
                        Marshal.DestroyStructure(pDevMode, typeof(DEVMODE));
                        Marshal.FreeHGlobal(pDevMode);
                        pDevMode = IntPtr.Zero;
                    }

                    if (value != null)
                    {
                        pDevMode = Marshal.AllocHGlobal(Marshal.SizeOf(value));
                        Marshal.StructureToPtr(value, pDevMode, false);
                    }
                }
            }
        }

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pPrinterDefaults);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool AddJob(IntPtr hPrinter, uint Level, IntPtr pData, uint cbBuf, out uint pcbNeeded);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool GetJob(IntPtr hPrinter, uint JobId, uint Level, IntPtr pJob, uint cbBuf, out uint pcbNeeded);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool SetJob(IntPtr hPrinter, uint JobId, uint Level, IntPtr pJob, uint Command);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ScheduleJob(IntPtr hPrinter, uint JobId);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern uint StartDocPrinter(IntPtr hPrinter, uint Level, ref DOC_INFO_1 pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, [MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBuf, out uint pcWritten);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

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

        private static void WritePrinter(IntPtr hPrinter, byte[] data)
        {
            int i = 0;

            while (i < data.Length)
            {
                byte[] _data = new byte[32768];
                uint len = (uint)Math.Min(data.Length - i, _data.Length);
                Array.Copy(data, i, _data, 0, len);

                if (!WritePrinter(hPrinter, _data, len, out len))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                i += (int)len;
            }
        }
        
        public static void PrintRaw(WindowsRawPrintJobInfo jobinfo)
        {
            if (jobinfo.RunAsUser)
            {
                jobinfo.RunAsUser = false;
                MemoryStream stdin = new MemoryStream();
                MemoryStream stdout = new MemoryStream();
                MemoryStream stderr = new MemoryStream();

                try
                {
                    Serialize(stdin, jobinfo);
                    stdin.Flush();
                    stdin.Position = 0;

                    int retcode = WindowsIdentityStore.RunProcessAsUser(jobinfo.UserName, stdin, stdout, stderr, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Assembly.GetExecutingAssembly().Location, new string[] { "-print" });

                    if (retcode != 0)
                    {
                        stderr.Position = 0;
                        Logger.Log(LogLevel.Info, "Error printing file:\n{0}", System.Text.Encoding.ASCII.GetString(stderr.ToArray()));
                        throw new WindowsRawPrinterException((Exception)Deserialize(stderr));
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
                IntPtr hPrinter;
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    DesiredAccess = PRINTER_ACCESS_MASK.PRINTER_ACCESS_USE,
                    pDatatype = "RAW"
                };

                if (OpenPrinter(jobinfo.PrinterName, out hPrinter, ref defaults))
                {
                    DOC_INFO_1 docInfo = new DOC_INFO_1 { Datatype = "RAW", DocName = jobinfo.JobName, OutputFile = null };
                    uint jobid = StartDocPrinter(hPrinter, 1, ref docInfo);

                    if (jobid < 0)
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }


                    if (jobinfo.RawPrintData != null)
                    {
                        WritePrinter(hPrinter, jobinfo.RawPrintData);
                    }

                    if (jobinfo.PagedData != null)
                    {
                        if (jobinfo.PagedData.Prologue != null)
                        {
                            WritePrinter(hPrinter, jobinfo.PagedData.Prologue);
                        }

                        if (jobinfo.PagedData.PageData != null)
                        {
                            foreach (byte[] pagedata in jobinfo.PagedData.PageData)
                            {
                                StartPagePrinter(hPrinter);
                                WritePrinter(hPrinter, pagedata);
                                EndPagePrinter(hPrinter);
                            }
                        }

                        if (jobinfo.PagedData.Epilogue != null)
                        {
                            WritePrinter(hPrinter, jobinfo.PagedData.Epilogue);
                        }
                    }

                    if (!EndDocPrinter(hPrinter))
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }

                    /*
                    IntPtr addjobinfobuf = Marshal.AllocHGlobal(32768);
                    uint cbneeded;

                    if (AddJob(hPrinter, 1, addjobinfobuf, 32768, out cbneeded))
                    {
                        ADDJOB_INFO_1 addjobinfo = (ADDJOB_INFO_1)Marshal.PtrToStructure(addjobinfobuf, typeof(ADDJOB_INFO_1));

                        IntPtr spooljobinfobuf = Marshal.AllocHGlobal(1048576);
                        GetJob(hPrinter, addjobinfo.JobId, 2, spooljobinfobuf, 1048576, out cbneeded);
                        JOB_INFO_2 oldspooljobinfo = (JOB_INFO_2)Marshal.PtrToStructure(spooljobinfobuf, typeof(JOB_INFO_2));
                    
                        JOB_INFO_2 spooljobinfo = new JOB_INFO_2
                        {
                            Datatype = "RAW",
                            Document = jobinfo.JobName,
                            JobId = addjobinfo.JobId,
                            UserName = jobinfo.UserName
                        };

                        Marshal.StructureToPtr(spooljobinfo, spooljobinfobuf, false);
                        SetJob(hPrinter, addjobinfo.JobId, 2, spooljobinfobuf, 0);
                        Marshal.DestroyStructure(spooljobinfobuf, typeof(JOB_INFO_2));
                        Marshal.FreeHGlobal(spooljobinfobuf);

                        File.WriteAllBytes(addjobinfo.Path, jobinfo.RawPrintData);

                        ScheduleJob(hPrinter, addjobinfo.JobId);
                    }
                     */

                    ClosePrinter(hPrinter);
                }
            }
        }

        public static int PrintRaw_Child(Stream stdin, Stream stdout, Stream stderr)
        {
            try
            {
                PrintRaw((WindowsRawPrintJobInfo)Deserialize(stdin));
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
