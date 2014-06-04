using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSVCEO.CloudPrint.Util
{
    public enum PCLBindingFormat
    {
        ASCII = 0x27,
        BigEndianBinary = 0x28,
        LittleEndianBinary = 0x29
    }

    class PCLPrintJob
    {
        public string[] PrePJL;
        public string[] PostPJL;
        public byte[] Prologue;
        public byte[] Epilogue;
        public List<byte[]> PageData = new List<byte[]>();
        public PCLBindingFormat BindingFormat;
        public bool ForeignEndian { get { return BitConverter.IsLittleEndian ^ (BindingFormat == PCLBindingFormat.LittleEndianBinary); } }

        protected ushort ToUInt16(byte[] rawdata, int start)
        {
            byte[] data;

            if (ForeignEndian)
            {
                data = new byte[] { rawdata[start + 1], rawdata[start] };
            }
            else
            {
                data = new byte[] { rawdata[start], rawdata[start + 1] };
            }

            return BitConverter.ToUInt16(data, 0);
        }

        protected uint ToUInt32(byte[] rawdata, int start)
        {
            byte[] data;

            if (ForeignEndian)
            {
                data = new byte[] { rawdata[start + 3], rawdata[start + 2], rawdata[start + 1], rawdata[start] };
            }
            else
            {
                data = new byte[] { rawdata[start], rawdata[start + 1], rawdata[start + 2], rawdata[start + 3] };
            }

            return BitConverter.ToUInt32(data, 0);
        }

        protected short ToInt16(byte[] rawdata, int start)
        {
            byte[] data;

            if (ForeignEndian)
            {
                data = new byte[] { rawdata[start + 1], rawdata[start] };
            }
            else
            {
                data = new byte[] { rawdata[start], rawdata[start + 1] };
            }

            return BitConverter.ToInt16(data, 0);
        }

        protected int ToInt32(byte[] rawdata, int start)
        {
            byte[] data;

            if (ForeignEndian)
            {
                data = new byte[] { rawdata[start + 3], rawdata[start + 2], rawdata[start + 1], rawdata[start] };
            }
            else
            {
                data = new byte[] { rawdata[start], rawdata[start + 1], rawdata[start + 2], rawdata[start + 3] };
            }

            return BitConverter.ToInt32(data, 0);
        }

        protected float ToSingle(byte[] rawdata, int start)
        {
            byte[] data;

            if (ForeignEndian)
            {
                data = new byte[] { rawdata[start + 3], rawdata[start + 2], rawdata[start + 1], rawdata[start] };
            }
            else
            {
                data = new byte[] { rawdata[start], rawdata[start + 1], rawdata[start + 2], rawdata[start + 3] };
            }

            return BitConverter.ToSingle(data, 0);
        }

        protected string[] ParsePJL(byte[] rawdata, int start, out int end)
        {
            List<string> pjllines = new List<string>();
            int pos = start;
            end = rawdata.Length;
            
            if ("\x1B%-12345X".Select((c, i) => rawdata[pos + i] == (byte)c).All(v => v))
            {
                pos += 9;
            }

            while (pos < rawdata.Length)
            {
                int lineend = Array.FindIndex(rawdata, pos, b => b == '\n');

                if (lineend == -1)
                {
                    end = rawdata.Length;
                    break;
                }

                string line = Encoding.ASCII.GetString(rawdata, pos, lineend - pos);
                pjllines.Add(line);
                if (line.StartsWith("@PJL ENTER"))
                {
                    end = lineend + 1;
                    break;
                }

                pos = lineend + 1;
            }

            return pjllines.ToArray();
        }

        protected string ParsePCLHeader(byte[] rawdata, int start, out int end)
        {
            BindingFormat = (PCLBindingFormat)rawdata[start];
            end = Array.FindIndex(rawdata, start + 1, b => b == '\n');

            if (end != -1)
            {
                end++;
                return Encoding.ASCII.GetString(rawdata, start + 2, end - start - 3);
            }

            return null;
        }

        protected long ParseIntegerType(byte[] rawdata, int start, out int end, byte tag)
        {
            switch (tag)
            {
                case 0xC0:
                    end = start + 1;
                    return rawdata[start];
                case 0xC1:
                    end = start + 2;
                    return ToUInt16(rawdata, start);
                case 0xC2:
                    end = start + 4;
                    return ToUInt32(rawdata, start);
                case 0xC3:
                    end = start + 2;
                    return ToInt16(rawdata, start);
                case 0xC4:
                    end = start + 4;
                    return ToInt32(rawdata, start);
                default:
                    throw new InvalidOperationException(String.Format("Unknown integer type {0}", tag));
            }
        }

        protected void SkipIntegerType(byte[] rawdata, int start, out int end, byte tag)
        {
            ParseIntegerType(rawdata, start, out end, tag);
        }

        protected void SkipScalarType(byte[] rawdata, int start, out int end, byte tag)
        {
            if (tag == 0xC5)
            {
                end = start + 4;
            }
            else
            {
                SkipIntegerType(rawdata, start, out end, tag);
            }
        }

        protected void SkipArrayType(byte[] rawdata, int start, out int end, byte tag)
        {
            byte sizetag = rawdata[start];
            int size = (int)ParseIntegerType(rawdata, start + 1, out start, sizetag);

            switch (tag)
            {
                case 0xC8: end = start + 1 * size; break;
                case 0xC9: end = start + 2 * size; break;
                case 0xCA: end = start + 4 * size; break;
                case 0xCB: end = start + 2 * size; break;
                case 0xCC: end = start + 4 * size; break;
                case 0xCD: end = start + 4 * size; break;
                default: throw new InvalidOperationException(String.Format("Unknown array type {0}", tag));
            }
        }

        protected void SkipPairType(byte[] rawdata, int start, out int end, byte tag)
        {
            switch (tag)
            {
                case 0xD0: end = start + 2; break;
                case 0xD1: end = start + 4; break;
                case 0xD2: end = start + 8; break;
                case 0xD3: end = start + 4; break;
                case 0xD4: end = start + 8; break;
                case 0xD5: end = start + 8; break;
                default: throw new InvalidOperationException(String.Format("Unknown integer type {0}", tag));
            }
        }

        protected void SkipQuadType(byte[] rawdata, int start, out int end, byte tag)
        {
            switch (tag)
            {
                case 0xE0: end = start + 4; break;
                case 0xE1: end = start + 8; break;
                case 0xE2: end = start + 16; break;
                case 0xE3: end = start + 8; break;
                case 0xE4: end = start + 16; break;
                case 0xE5: end = start + 16; break;
                default: throw new InvalidOperationException(String.Format("Unknown integer type {0}", tag));
            }
        }

        protected void SkipDataType(byte[] rawdata, int start, out int end, byte tag)
        {
            if (tag >= 0xC0 && tag <= 0xC7)
            {
                SkipScalarType(rawdata, start, out end, tag);
            }
            else if (tag >= 0xC8 && tag <= 0xCF)
            {
                SkipArrayType(rawdata, start, out end, tag);
            }
            else if (tag >= 0xD0 && tag <= 0xD7)
            {
                SkipPairType(rawdata, start, out end, tag);
            }
            else if (tag >= 0xE0 && tag <= 0xE7)
            {
                SkipQuadType(rawdata, start, out end, tag);
            }
            else
            {
                throw new InvalidOperationException(String.Format("Unknown data type {0}", tag));
            }
        }

        protected void SkipDataArray(byte[] rawdata, int start, out int end, byte tag)
        {
            int length;

            if (tag == 0xfa)
            {
                length = ToInt32(rawdata, start);
                start += 4;
            }
            else if (tag == 0xfb)
            {
                length = rawdata[start];
                start += 1;
            }
            else
            {
                throw new InvalidOperationException(String.Format("Unknown data type {0}", tag));
            }

            end = start + length;
        }

        public PCLPrintJob(byte[] rawdata)
        {
            int pos = 0;
            this.PrePJL = ParsePJL(rawdata, pos, out pos);
            int pclstart = pos;
            int pclpagestart = -1;
            int pclpageend = -1;
            ParsePCLHeader(rawdata, pos, out pos);

            if (BindingFormat == PCLBindingFormat.ASCII)
            {
                throw new InvalidOperationException("Cannot handle ASCII PCL XL");
            }
            else
            {
                while (pos < rawdata.Length)
                {
                    byte tag = rawdata[pos];
                    pos++;

                    if (tag == 27)
                    {
                        break;
                    }
                    else if (tag >= 0x41 && tag <= 0xBF)
                    {
                        if (tag == 0x43)
                        {
                            if (pclpagestart == -1)
                            {
                                this.Prologue = new byte[pos - pclstart];
                                Array.Copy(rawdata, pclstart, this.Prologue, 0, pos - pclstart);
                            }
                            else
                            {
                                byte[] pagedata = new byte[pos - pclpagestart];
                                Array.Copy(rawdata, pclpagestart, pagedata, 0, pos - pclpagestart);
                                this.PageData.Add(pagedata);
                            }

                            pclpagestart = pos;
                        }
                        else if (tag == 0x44)
                        {
                            pclpageend = pos;
                        }
                    }
                    else if (tag >= 0xC0 && tag <= 0xEF)
                    {
                        SkipDataType(rawdata, pos, out pos, tag);
                    }
                    else if (tag == 0xF8)
                    {
                        pos++;
                    }
                    else if (tag == 0xF9)
                    {
                        pos += 2;
                    }
                    else if (tag == 0xFA || tag == 0xFB)
                    {
                        SkipDataArray(rawdata, pos, out pos, tag);
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("Unknown tag {0}", tag));
                    }
                }

                if (pclpageend != -1)
                {
                    byte[] pagedata = new byte[pclpageend - pclpagestart];
                    Array.Copy(rawdata, pclpagestart, pagedata, 0, pclpageend - pclpagestart);
                    this.PageData.Add(pagedata);
                }
                else
                {
                    pclpageend = pclstart;
                }

                this.Epilogue = new byte[pos - pclpageend];
                Array.Copy(rawdata, pclpageend, this.Epilogue, 0, pos - pclpageend);
            }

            this.PostPJL = ParsePJL(rawdata, pos, out pos);
        }
    }
}
