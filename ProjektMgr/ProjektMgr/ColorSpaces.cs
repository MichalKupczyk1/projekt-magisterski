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
        public RGBPixel(double y, double cb, double cr)
        {
            cb -= 128.0;
            cr -= 128.0;

            var r = y + 1.402 * cr;
            var g = y - 0.344136 * cb - 0.714136 * cr;
            var b = y + 1.772 * cb;

            R = (byte)Math.Clamp(r, 0, 255);
            G = (byte)Math.Clamp(g, 0, 255);
            B = (byte)Math.Clamp(b, 0, 255);
        }
    }

    public class YCbCr
    {
        public double Y { get; set; }
        public double Cb { get; set; }
        public double Cr { get; set; }

        public YCbCr(byte r, byte g, byte b)
        {
            var y = 0.299 * r + 0.587 * g + 0.114 * b;
            var cb = -0.168736 * r - 0.331264 * g + 0.5 * b + 128;
            var cr = 0.5 * r - 0.418688 * g - 0.081312 * b + 128;

            Y = Math.Clamp(y, 0, 255);
            Cb = Math.Clamp(cb, 0, 255);
            Cr = Math.Clamp(cr, 0, 255);
        }
    }
}
