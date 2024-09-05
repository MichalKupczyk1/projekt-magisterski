using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjektMgr
{
    //jpeg jfif
    public static class JFIF
    {
        public static RGBPixel[,] Pixels { get; set; }
        public static void LoadAllPixels(byte[] bytes, long width, long height, int padding)
        {
            var oneDimArray = CreateOneDimRGBPixelArray(bytes, width, height, padding);
            var YCbCrArray = CreateTwoDimYCbCrPixelArray(oneDimArray, width, height);
            var downsampledArray = ApplyDownsampling(YCbCrArray, width, height);
        }

        private static RGBPixel[] CreateOneDimRGBPixelArray(byte[] bytes, long width, long height, int padding)
        {
            var pixelsOneDim = new RGBPixel[width * height];
            var z = 0;
            var i = 0;
            var counter = 0;

            for (i = 0; i < bytes.Length - 54;)
            {
                if (padding != 0 && counter != 0 && (counter / 3) % width == 0)
                {
                    i += padding;
                    counter = 0;
                    continue;
                }
                pixelsOneDim[z++] = new RGBPixel(bytes[i + 54], bytes[i + 55], bytes[i + 56]);
                i += 3;

                if (padding != 0)
                    counter += 3;
            }
            return pixelsOneDim;
        }

        private static YCbCr[,] CreateTwoDimYCbCrPixelArray(RGBPixel[] pixels, long width, long height)
        {
            var twoDimArray = new YCbCr[width, height];
            var count = 0;

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    var pixel = pixels[count++];
                    twoDimArray[i, j] = new YCbCr(pixel.R, pixel.G, pixel.B);
                }
            }
            return twoDimArray;
        }

        private static (float[,] Y, float[,] Cb, float[,] Cr) ApplyDownsampling(YCbCr[,] YCbCrArray, long width, long height)
        {
            float[,] Y = new float[height, width];
            float[,] Cb = new float[height / 2, width / 2];
            float[,] Cr = new float[height / 2, width / 2];

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Y[i, j] = YCbCrArray[i, j].Y;
                    if (i % 2 == 0 && j % 2 == 0)
                    {
                        Cb[i / 2, j / 2] = YCbCrArray[i, j].Cb;
                        Cr[i / 2, j / 2] = YCbCrArray[i, j].Cr;
                    }
                }
            }

            return (Y, Cb, Cr);
        }
    }
}
