using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Util
{
    [Serializable]
    public class PaginatedPrintData
    {
        public byte[] Prologue { get; set; }
        public byte[][] PageData { get; set; }
        public byte[] Epilogue { get; set; }

        public byte[] GetData()
        {
            return Prologue.Concat(PageData.SelectMany(p => p)).Concat(Epilogue).ToArray();
        }
    }
}
