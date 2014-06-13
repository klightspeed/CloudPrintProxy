using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using System.Printing;
using System.Printing.Interop;

namespace TSVCEO.CloudPrint.Util.Poppler
{
    public enum CairoStatus
    {
        SUCCESS = 0,

        NO_MEMORY,
        INVALID_RESTORE,
        INVALID_POP_GROUP,
        NO_CURRENT_POINT,
        INVALID_MATRIX,
        INVALID_STATUS,
        NULL_POINTER,
        INVALID_STRING,
        INVALID_PATH_DATA,
        READ_ERROR,
        WRITE_ERROR,
        SURFACE_FINISHED,
        SURFACE_TYPE_MISMATCH,
        PATTERN_TYPE_MISMATCH,
        INVALID_CONTENT,
        INVALID_FORMAT,
        INVALID_VISUAL,
        FILE_NOT_FOUND,
        INVALID_DASH,
        INVALID_DSC_COMMENT,
        INVALID_INDEX,
        CLIP_NOT_REPRESENTABLE,
        TEMP_FILE_ERROR,
        INVALID_STRIDE,
        FONT_TYPE_MISMATCH,
        USER_FONT_IMMUTABLE,
        USER_FONT_ERROR,
        NEGATIVE_COUNT,
        INVALID_CLUSTERS,
        INVALID_SLANT,
        INVALID_WEIGHT,
        INVALID_SIZE,
        USER_FONT_NOT_IMPLEMENTED,
        DEVICE_TYPE_MISMATCH,
        DEVICE_ERROR,
        INVALID_MESH_CONSTRUCTION,
        DEVICE_FINISHED,

        LAST_STATUS
    }

    public struct DOCINFO
    {
        public int Size;
        public string DocName;
        public string Output;
        public string Datatype;
        public int Type;
    }

    public static class NativeMethods
    {
        #region DLL Loading

        static NativeMethods()
        {
            SetDllDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "poppler"));
            LoadLibrary("libcairo-2.dll");
            LoadLibrary("libgobject-2.0-0.dll");
            LoadLibrary("libpoppler-glib-8.dll");
            SetDllDirectory(null);
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetDllDirectory(string path);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string filename);

        #endregion

        #region GDI Functions

        [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateDC(string driver, string device, string output, IntPtr devmode);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool ReleaseDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern int StartDoc(IntPtr hdc, ref DOCINFO di);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern int StartPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern int EndPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern int EndDoc(IntPtr hdc);

        #endregion

        #region Cairo Functions

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cairo_create(IntPtr target);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_destroy(IntPtr cr);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern CairoStatus cairo_status(IntPtr cr);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern string cairo_status_to_string(CairoStatus status);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_save(IntPtr cr);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_restore(IntPtr cr);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_scale(IntPtr cr, double xscale, double yscale);

        #endregion

        #region Cairo Surface Functions

        public delegate CairoStatus cairo_write_func_t(object userdata, IntPtr data, int length);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cairo_win32_printing_surface_create(IntPtr hdc);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cairo_ps_surface_create_for_stream(cairo_write_func_t writefunc, object userdata, double width_in_points, double height_in_points);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_ps_surface_set_size(IntPtr surface, double width_in_points, double height_in_points);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_surface_show_page(IntPtr surface);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_surface_finish(IntPtr surface);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cairo_surface_destroy(IntPtr surface);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern CairoStatus cairo_surface_status(IntPtr surface);

        #endregion

        #region GObject Functions

        [DllImport("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void g_object_unref(IntPtr gobject);

        #endregion

        #region Poppler Functions

        [DllImport("libpoppler-glib-8.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr poppler_document_new_from_data(IntPtr data, int len, string password, IntPtr error);

        [DllImport("libpoppler-glib-8.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int poppler_document_get_n_pages(IntPtr doc);

        [DllImport("libpoppler-glib-8.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr poppler_document_get_page(IntPtr doc, int index);

        [DllImport("libpoppler-glib-8.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void poppler_page_render_for_printing(IntPtr page, IntPtr cairo);

        [DllImport("libpoppler-glib-8.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void poppler_page_get_size(IntPtr page, ref double width_in_points, ref double height_in_points);

        #endregion

        public static string ToString(this CairoStatus status)
        {
            return cairo_status_to_string(status);
        }
    }

    public class PopplerDocument : IDisposable, IEnumerable<PopplerPage>
    {
        private IntPtr Data;
        public IntPtr DocPtr { get; protected set; }

        public PopplerDocument(byte[] data, string password)
        {
            Data = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, Data, data.Length);
            DocPtr = NativeMethods.poppler_document_new_from_data(Data, data.Length, password, IntPtr.Zero);
        }

        #region Destructor / Disposal

        ~PopplerDocument()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (DocPtr != IntPtr.Zero)
            {
                NativeMethods.g_object_unref(DocPtr);
                DocPtr = IntPtr.Zero;
            }

            if (Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Data);
                Data = IntPtr.Zero;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        public PopplerPage this[int index]
        {
            get
            {
                return new PopplerPage(this, index);
            }
        }

        public int Count { get { return NativeMethods.poppler_document_get_n_pages(DocPtr); } }

        public IEnumerator<PopplerPage> GetEnumerator()
        {
            int count = Count;

            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Print(GDIPrinterDeviceContext dc, string jobname)
        {
            dc.StartDoc(jobname);

            using (CairoWin32PrintingSurface surface = new CairoWin32PrintingSurface(dc))
            {
                using (Cairo cairo = new Cairo(surface))
                {
                    int xdpi = dc.PrintTicket.PageResolution.X ?? 75;
                    int ydpi = dc.PrintTicket.PageResolution.Y ?? 75;

                    cairo.Scale(xdpi / 72.0, ydpi / 72.0);

                    foreach (PopplerPage page in this)
                    {
                        dc.StartPage();

                        cairo.Save();
                        page.RenderForPrinting(cairo);
                        cairo.Restore();
                        surface.ShowPage();


                        dc.EndPage();
                    }
                }

                surface.Finish();
            }

            dc.EndDoc();
        }

        public void WritePostscript(Stream output, bool closestream = true)
        {
            using (CairoPostscriptSurface surface = new CairoPostscriptSurface(output, 620, 877, closestream))
            {
                using (Cairo cairo = new Cairo(surface))
                {
                    foreach (PopplerPage page in this)
                    {
                        surface.SetSize(page.WidthInPoints, page.HeightinPoints);
                        cairo.Save();
                        page.RenderForPrinting(cairo);
                        cairo.Restore();
                        surface.ShowPage();
                    }
                }
            }
        }
    }

    public class PopplerPage : IDisposable
    {
        public IntPtr PagePtr { get; private set; }
        public double WidthInPoints { get; private set; }
        public double HeightinPoints { get; private set; }

        public PopplerPage(PopplerDocument doc, int index)
        {
            PagePtr = NativeMethods.poppler_document_get_page(doc.DocPtr, index);
            double width = 0;
            double height = 0;
            NativeMethods.poppler_page_get_size(PagePtr, ref width, ref height);
            WidthInPoints = width;
            HeightinPoints = height;
        }

        #region Destructor / Disposal

        ~PopplerPage()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (PagePtr != IntPtr.Zero)
            {
                NativeMethods.g_object_unref(PagePtr);
                PagePtr = IntPtr.Zero;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        public void RenderForPrinting(Cairo cairo)
        {
            NativeMethods.poppler_page_render_for_printing(PagePtr, cairo.CairoPtr);
        }
    }

    public class Cairo : IDisposable
    {
        public IntPtr CairoPtr { get; private set; }

        public Cairo(CairoSurface surface)
        {
            CairoPtr = NativeMethods.cairo_create(surface.CairoSurfacePtr);
        }

        #region Destructor / Disposal

        ~Cairo()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (CairoPtr != IntPtr.Zero)
            {
                NativeMethods.cairo_destroy(CairoPtr);
                CairoPtr = IntPtr.Zero;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        public void Scale(double xscale, double yscale)
        {
            NativeMethods.cairo_scale(CairoPtr, xscale, yscale);
        }

        public void Save()
        {
            NativeMethods.cairo_save(CairoPtr);
        }

        public void Restore()
        {
            NativeMethods.cairo_restore(CairoPtr);
        }

        public CairoStatus GetStatus()
        {
            return NativeMethods.cairo_status(CairoPtr);
        }
    }

    public class CairoSurface : IDisposable
    {
        public IntPtr CairoSurfacePtr { get; protected set; }

        protected CairoSurface() { }

        #region Destructor / Disposal

        ~CairoSurface()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (CairoSurfacePtr != IntPtr.Zero)
            {
                NativeMethods.cairo_destroy(CairoSurfacePtr);
                CairoSurfacePtr = IntPtr.Zero;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        public void ShowPage()
        {
            NativeMethods.cairo_surface_show_page(CairoSurfacePtr);
        }

        public void Finish()
        {
            NativeMethods.cairo_surface_finish(CairoSurfacePtr);
        }
    }

    public class CairoWin32PrintingSurface : CairoSurface
    {
        public CairoWin32PrintingSurface(GDIPrinterDeviceContext dc)
        {
            this.CairoSurfacePtr = NativeMethods.cairo_win32_printing_surface_create(dc.HDC);
        }
    }

    public class CairoPostscriptSurface : CairoSurface
    {
        protected Stream OutputStream;
        protected bool CloseStream;

        public CairoPostscriptSurface(Stream output, double width_in_points, double height_in_points, bool closestream = true)
        {
            OutputStream = output;
            CloseStream = closestream;
            NativeMethods.cairo_ps_surface_create_for_stream(CairoPostscriptSurface.Write, this, width_in_points, height_in_points);
        }

        protected static CairoStatus Write(object userdata, IntPtr data, int length)
        {
            return ((CairoPostscriptSurface)userdata).Write(data, length);
        }

        protected CairoStatus Write(IntPtr data, int length)
        {
            try
            {
                byte[] _data = new byte[length];
                Marshal.Copy(data, _data, 0, length);
                OutputStream.Write(_data, 0, length);
                return CairoStatus.SUCCESS;
            }
            catch
            {
                return CairoStatus.WRITE_ERROR;
            }
        }

        public void SetSize(double width_in_points, double height_in_points)
        {
            NativeMethods.cairo_ps_surface_set_size(CairoSurfacePtr, width_in_points, height_in_points);
        }
    }

    public class GDIDeviceContext : IDisposable
    {
        public IntPtr HDC { get; protected set; }
        public bool OwnsContext { get; protected set; }

        protected GDIDeviceContext(bool ownscontext)
        {
            OwnsContext = ownscontext;
        }

        #region Destructor / Disposal

        ~GDIDeviceContext()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (HDC != IntPtr.Zero)
            {
                if (OwnsContext)
                {
                    NativeMethods.DeleteDC(HDC);
                }
                else
                {
                    NativeMethods.ReleaseDC(HDC);
                }

                HDC = IntPtr.Zero;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }

    public class GDIPrinterDeviceContext : GDIDeviceContext
    {
        public PrintTicket PrintTicket { get; protected set; }

        public GDIPrinterDeviceContext(string printername, PrintTicket ticket)
            : base(true)
        {
            PrintTicketConverter ptc = new PrintTicketConverter(printername, PrintTicketConverter.MaxPrintSchemaVersion);
            byte[] dmdata = ptc.ConvertPrintTicketToDevMode(ticket, BaseDevModeType.PrinterDefault);
            this.PrintTicket = ptc.ConvertDevModeToPrintTicket(dmdata);
            IntPtr dmptr = Marshal.AllocHGlobal(dmdata.Length);
            Marshal.Copy(dmdata, 0, dmptr, dmdata.Length);
            this.HDC = NativeMethods.CreateDC("WINSPOOL", printername, null, dmptr);
            Marshal.FreeHGlobal(dmptr);
        }

        public void StartDoc(DOCINFO di)
        {
            NativeMethods.StartDoc(HDC, ref di);
        }

        public void StartDoc(string docname, string outputfile = null, string datatype = null, int type = 0)
        {
            StartDoc(new DOCINFO { Size = Marshal.SizeOf(typeof(DOCINFO)), DocName = docname, Output = outputfile, Datatype = datatype, Type = 0 });
        }

        public void EndDoc()
        {
            NativeMethods.EndDoc(HDC);
        }

        public void StartPage()
        {
            NativeMethods.StartPage(HDC);
        }

        public void EndPage()
        {
            NativeMethods.EndPage(HDC);
        }
    }
}
