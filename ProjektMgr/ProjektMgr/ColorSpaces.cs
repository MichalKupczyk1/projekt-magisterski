using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjektMgr
{
    public class RGBPixel
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public RGBPixel(byte b, byte g, byte r)
        {
            R = r;
            G = g;
            B = b;
        }
        public RGBPixel(float y, float cb, float cr)
        {
            R = (byte)((y - 16) * 1.164f + (cr - 128) * 1.596f);
            G = (byte)((y - 16) * 1.164f - (cb - 128) * 0.392f - (cr - 128) * 0.813f);
            B = (byte)((y - 16) * 1.164f + (cb - 128) * 2.017f);
        }
    }

    public class YCbCr
    {
        public float Y { get; set; }
        public float Cb { get; set; }
        public float Cr { get; set; }

        public YCbCr(byte r, byte g, byte b)
        {
            Y = 16 + (65.738f * r / 256) + (129.057f * g / 256) + (25.064f * b / 256);
            Cb = 128 - (37.945f * r / 256) - (74.494f * g / 256) + (112.439f * b / 256);
            Cr = 128 + (112.439f * r / 256) - (94.154f * g / 256) - (18.285f * b / 256);
        }
    }
}
