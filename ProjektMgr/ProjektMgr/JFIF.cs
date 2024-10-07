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
        //domyslnie 8x8, wedlug standardu jfif
        private static int blockSize = 8;
        public static RGBPixel[,] Pixels { get; set; }
        public static void LoadAllPixels(byte[] bytes, long width, long height, int padding)
        {
            var oneDimArray = CreateOneDimRGBPixelArray(bytes, width, height, padding);
            var YCbCrArray = CreateTwoDimYCbCrPixelArray(oneDimArray, width, height);
            var downsampledArray = ApplyDownsampling(YCbCrArray, width, height);
            var Y = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Y));
            var Cr = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Cr));
            var Cb = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Cb));

            var YDCT = ApplyDCTToBlock(Y)
                .Select(x => QuantizeYBlock(x))
                .ToList();

            var CrDCT = ApplyDCTToBlock(Cr)
                .Select(x => QuantizeCbCrBlock(x))
                .ToList();

            var CbDCT = ApplyDCTToBlock(Cb)
                .Select(x => QuantizeCbCrBlock(x))
                .ToList();
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

        private static float[,] ExtendArrayIfNeeded(float[,] array)
        {
            int originalRows = array.GetLength(0);
            int originalCols = array.GetLength(1);

            int rowsToAdd = (originalRows % 8 != 0) ? 8 - (originalRows % 8) : 0;
            int colsToAdd = (originalCols % 8 != 0) ? 8 - (originalCols % 8) : 0;

            if (rowsToAdd == 0 && colsToAdd == 0)
                return array;

            float[,] newArray = new float[originalRows + rowsToAdd, originalCols + colsToAdd];

            for (int i = 0; i < newArray.GetLength(0); i++)
            {
                for (int j = 0; j < newArray.GetLength(1); j++)
                {
                    if (i >= originalRows)
                        newArray[i, j] = array[originalRows - 1, Math.Min(j, originalCols - 1)];

                    else if (j >= originalCols)
                        newArray[i, j] = array[i, originalCols - 1];

                    else
                        newArray[i, j] = array[i, j];
                }
            }

            return newArray;
        }

        private static float[,] CalculateDCT(float[,] block)
        {
            var width = block.GetLength(0);
            var height = block.GetLength(1);
            var result = new float[width, height];

            for (int u = 0; u < blockSize; u++)
            {
                for (int v = 0; v < blockSize; v++)
                {
                    float sum = 0.0f;

                    for (int x = 0; x < blockSize; x++)
                        for (int y = 0; y < blockSize; y++)
                            sum += block[x, y] * (float)Math.Cos(((2 * x + 1) * u * Math.PI) / (2 * blockSize)) * (float)Math.Cos(((2 * y + 1) * v * Math.PI) / (2 * blockSize));

                    float alphaU = (u == 0) ? (float)(1 / Math.Sqrt(2)) : 1.0f;
                    float alphaV = (v == 0) ? (float)(1 / Math.Sqrt(2)) : 1.0f;
                    result[u, v] = 0.25f * alphaU * alphaV * sum;
                }
            }
            return result;
        }

        public static float[,] ShiftValuesInArray(float[,] array)
        {
            var width = array.GetLength(0);
            var height = array.GetLength(1);

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    array[i, j] -= 128;
                }
            }
            return array;
        }

        private static List<float[,]> ApplyDCTToBlock(float[,] array)
        {
            int width = array.GetLength(0);
            int height = array.GetLength(1);
            var res = new List<float[,]>();

            for (int i = 0; i < height; i += blockSize)
            {
                for (int j = 0; j < width; j += blockSize)
                {
                    var block = new float[blockSize, blockSize];
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            block[x, y] = array[i + x, j + y];
                        }
                    }
                    var dct = CalculateDCT(block);
                    res.Add(dct);
                }
            }
            return res;
        }

        private static float[,] QuantizeYBlock(float[,] block)
        {
            var res = new float[blockSize, blockSize];

            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                    res[i, j] = (float)Math.Round(block[i, j] / CalculationArrays.QuantizationYTable[i, j]);
            }
            return res;
        }

        private static float[,] QuantizeCbCrBlock(float[,] block)
        {
            var res = new float[blockSize, blockSize];

            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                    res[i, j] = (float)Math.Round(block[i, j] / CalculationArrays.QuantizationCbCrTable[i, j]);
            }
            return res;
        }

        private static int[] ZigZagScan(float[,] block)
        {
            var res = new int[64];

            for (int i = 0; i < 64; i++)
            {
                int x = CalculationArrays.ZigZagScan[i] / 8;
                int y = CalculationArrays.ZigZagScan[i] % 8;
                res[i] = (int)block[x, y];
            }

            return res;
        }
    }

    public static class CalculationArrays
    {
        //all tables according to jfif/jpeg standard
        public static readonly int[,] QuantizationYTable = new int[8, 8]
            {
                { 16, 11, 10, 16, 24, 40, 51, 61 },
                { 12, 12, 14, 19, 26, 58, 60, 55 },
                { 14, 13, 16, 24, 40, 57, 69, 56 },
                { 14, 17, 22, 29, 51, 87, 80, 62 },
                { 18, 22, 37, 56, 68, 109, 103, 77 },
                { 24, 35, 55, 64, 81, 104, 113, 92 },
                { 49, 64, 78, 87, 103, 121, 120, 101 },
                { 72, 92, 95, 98, 112, 100, 103, 99 }
            };

        public static readonly int[,] QuantizationCbCrTable = new int[8, 8]
            {
                { 17, 18, 24, 47, 99, 99, 99, 99 },
                { 18, 21, 26, 66, 99, 99, 99, 99 },
                { 24, 26, 56, 99, 99, 99, 99, 99 },
                { 47, 66, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 }
            };

        public static readonly int[] ZigZagScan = new int[64]
             {
                0, 1, 5, 6, 14, 15, 27, 28,
                2, 4, 7, 13, 16, 26, 29, 42,
                3, 8, 12, 17, 25, 30, 41, 43,
                9, 11, 18, 24, 31, 40, 44, 53,
                10, 19, 23, 32, 39, 45, 52, 54,
                20, 22, 33, 38, 46, 51, 55, 60,
                21, 34, 37, 47, 50, 56, 59, 61,
                35, 36, 48, 49, 57, 58, 62, 63
             };
    }
}
