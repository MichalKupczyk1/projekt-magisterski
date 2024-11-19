using System.Diagnostics;

namespace ProjektMgr
{
    //jpeg jfif
    public static class JFIF
    {
        //domyslnie 8x8, wedlug standardu jfif
        private static int blockSize = 8;

        public static byte[] CreateJFIFFile(byte[] bytes, long width, long height, int padding)
        {
            Console.WriteLine("File size before encoding: " + (bytes.Length / 1000000.0).ToString("F2") + "mb");

            var YCbCrArray = CreateTwoDimYCbCrPixelArray(CreateOneDimPixelArray(bytes, width, height, padding), width, height);
            var downsampledData = SeparateArrayIntoDownsampledData(YCbCrArray);

            var YBlocks = SeparateIntoBlocks(downsampledData.Y);
            var CbBlocks = SeparateIntoBlocks(downsampledData.Cb);
            var CrBlocks = SeparateIntoBlocks(downsampledData.Cr);

            var YDCT = ApplyDCTAndQuantization(YBlocks, true);
            var CbDCT = ApplyDCTAndQuantization(CbBlocks);
            var CrDCT = ApplyDCTAndQuantization(CrBlocks);

            var encodedY = GenerateHuffmanEncodedDataString(YDCT, isY: true);
            var encodedCb = GenerateHuffmanEncodedDataString(CbDCT, isCb: true);
            var encodedCr = GenerateHuffmanEncodedDataString(CrDCT, isCr: true);

            Console.WriteLine("File size after encoding: " + ((encodedY.Length + encodedCb.Length + encodedCr.Length) / 1000000.0).ToString("F2") + "mb");

            var DecodedYBlocks = ApplyIDCTAndDequantize(YDCT, true);
            var DecodedCbBlocks = ApplyIDCTAndDequantize(CbDCT);
            var DecodedCrBlocks = ApplyIDCTAndDequantize(CrDCT);

            var MergedY = MergeBlocksInto2DArray(DecodedYBlocks, width, height, true);
            var MergedCb = MergeBlocksInto2DArray(DecodedCbBlocks, width, height);
            var MergedCr = MergeBlocksInto2DArray(DecodedCrBlocks, width, height);

            var rgb = YCbCrToRGB(height, width, MergedY, MergedCb, MergedCr);

            return PixelToByteArray(rgb);
        }

        private static RGBPixel[,] YCbCrToRGB(long height, long width, double[,] y, double[,] cb, double[,] cr)
        {
            var res = new RGBPixel[height, width];

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    res[i, j] = new RGBPixel(y: y[i, j], cb: cb[i, j], cr: cr[i, j]);

            return res;
        }

        public static byte[] PixelToByteArray(RGBPixel[,] pixels)
        {
            var height = pixels.GetLength(0);
            var width = pixels.GetLength(1);
            var length = height * width;

            var oneDimPixels = new RGBPixel[length];
            var z = 0;

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    oneDimPixels[z++] = pixels[i, j];

            var byteLength = length * 3;
            var result = new byte[byteLength];
            var a = 0;

            for (int i = 0; i < byteLength;)
            {
                result[i] = oneDimPixels[a].B;
                result[i + 1] = oneDimPixels[a].G;
                result[i + 2] = oneDimPixels[a].R;
                i += 3;
                a++;
            }
            return result;
        }

        private static double[,] MergeBlocksInto2DArray(List<double[,]> blocks, long width, long height, bool isY = false)
        {
            var res = new double[height, width];
            var counter = 0;

            var tempArrayHeight = isY ? height : height / 2;
            var tempArrayWidth = isY ? width : width / 2;

            if (isY)
            {
                for (int i = 0; i < tempArrayHeight - blockSize; i += blockSize)
                {
                    for (int j = 0; j < tempArrayWidth - blockSize; j += blockSize)
                    {
                        var block = blocks[counter++];
                        for (int x = 0; x < blockSize; x++)
                            for (int y = 0; y < blockSize; y++)
                                res[i + x, j + y] = block[x, y] + 128.0;
                    }
                }
            }
            else
            {
                var array = new double[tempArrayHeight, tempArrayWidth];
                for (int i = 0; i < tempArrayHeight - blockSize; i += blockSize)
                {
                    for (int j = 0; j < tempArrayWidth - blockSize; j += blockSize)
                    {
                        var block = blocks[counter++];
                        for (int x = 0; x < blockSize; x++)
                            for (int y = 0; y < blockSize; y++)
                                array[i + x, j + y] = block[x, y];
                    }
                }
                for (int i = 0; i < tempArrayHeight; i++)
                {
                    for (int j = 0; j < tempArrayWidth; j++)
                    {
                        var temp = array[i, j] + 128.0;
                        res[i * 2, j * 2] = temp;
                        res[i * 2, j * 2 + 1] = temp;
                        res[i * 2 + 1, j * 2] = temp;
                        res[i * 2 + 1, j * 2 + 1] = temp;
                    }
                }
            }
            return res;
        }

        private static List<double[,]> ApplyIDCTAndDequantize(List<double[,]> blocks, bool isY = false)
        {
            var resList = new List<double[,]>();
            for (int i = 0; i < blocks.Count; i++)
            {
                var temp = new double[blockSize, blockSize];
                for (int x = 0; x < blockSize; x++)
                    for (int y = 0; y < blockSize; y++)
                        temp[x, y] = blocks[i][x, y];
                resList.Add(ApplyIDCT((DequantizeBlock(temp, isY))));
            }

            return resList;
        }

        private static double[,] ApplyIDCT(double[,] block)
        {
            var height = block.GetLength(0);
            var width = block.GetLength(1);

            var res = new double[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    var sum = 0.0;
                    for (int x = 0; x < height; x++)
                    {
                        for (int y = 0; y < width; y++)
                        {
                            var cu = x == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                            var cv = y == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                            sum += block[x, y] * cu * cv * Math.Cos(((Math.PI * (2.0 * i + 1.0) * x) / (2.0 * blockSize))) * Math.Cos(((Math.PI * (2.0 * j + 1.0) * y) / (2.0 * blockSize)));
                        }
                    }
                    res[i, j] = 0.25 * sum;
                }
            }
            return res;
        }

        private static double[,] DequantizeBlock(double[,] block, bool isY = false)
        {
            var height = block.GetLength(0); ;
            var width = block.GetLength(1);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    block[i, j] = isY ? block[i, j] * CalculationArrays.QuantizationYTable[i, j] : block[i, j] * CalculationArrays.QuantizationCbCrTable[i, j];

            return block;
        }

        private static List<double[,]> ApplyDCTAndQuantization(List<double[,]> blocks, bool isY = false)
        {
            var resList = new List<double[,]>();
            for (int i = 0; i < blocks.Count; i++)
            {
                var temp = new double[blockSize, blockSize];
                for (int x = 0; x < blockSize; x++)
                    for (int y = 0; y < blockSize; y++)
                        temp[x, y] = blocks[i][x, y];
                resList.Add(QuantizeBlock(ApplyDCT(temp), isY));
            }

            return resList;
        }

        private static double[,] QuantizeBlock(double[,] block, bool isY = false)
        {
            var height = block.GetLength(0); ;
            var width = block.GetLength(1);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    block[i, j] = isY ? Math.Round(block[i, j] / CalculationArrays.QuantizationYTable[i, j]) : Math.Round(block[i, j] / CalculationArrays.QuantizationCbCrTable[i, j]);

            return block;
        }

        private static double[,] ApplyDCT(double[,] block)
        {
            var height = block.GetLength(0);
            var width = block.GetLength(1);

            var res = new double[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    var sum = 0.0;
                    for (int x = 0; x < height; x++)
                    {
                        for (int y = 0; y < width; y++)
                        {
                            sum += block[x, y] * Math.Cos(((Math.PI * (2.0 * x + 1.0) * i) / (2.0 * blockSize))) * Math.Cos(((Math.PI * (2.0 * y + 1.0) * j) / (2.0 * blockSize)));
                        }
                    }
                    var cu = i == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                    var cv = j == 0 ? 1.0 / Math.Sqrt(2) : 1.0;

                    res[i, j] = 0.25 * cu * cv * sum;
                }
            }
            return res;
        }

        private static List<double[,]> SeparateIntoBlocks(double[,] data)
        {
            var res = new List<double[,]>();
            var height = data.GetLength(0);
            var width = data.GetLength(1);

            for (int i = 0; i < height - blockSize; i += blockSize)
            {
                for (int j = 0; j < width - blockSize; j += blockSize)
                {
                    var block = new double[blockSize, blockSize];
                    for (int x = 0; x < blockSize; x++)
                        for (int y = 0; y < blockSize; y++)
                            block[x, y] = data[i + x, y + j];
                    res.Add(block);
                }
            }
            return res;
        }

        private static (double[,] Y, double[,] Cb, double[,] Cr) SeparateArrayIntoDownsampledData(YCbCr[,] YCbCrData)
        {
            var height = YCbCrData.GetLength(0);
            var width = YCbCrData.GetLength(1);

            var y = new double[height, width];
            var downsampledCb = new double[height / 2, width / 2];
            var downsampledCr = new double[height / 2, width / 2];

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    y[i, j] = YCbCrData[i, j].Y - 128.0;

            for (int i = 0; i < height / 2; i++)
            {
                for (int j = 0; j < width / 2; j++)
                {
                    downsampledCb[i, j] =
                        ((YCbCrData[i * 2, j * 2].Cb +
                         YCbCrData[i * 2, j * 2 + 1].Cb +
                         YCbCrData[i * 2 + 1, j * 2].Cb +
                         YCbCrData[i * 2 + 1, j * 2 + 1].Cb) / 4.0) - 128.0;

                    downsampledCr[i, j] =
                        ((YCbCrData[i * 2, j * 2].Cr +
                         YCbCrData[i * 2, j * 2 + 1].Cr +
                         YCbCrData[i * 2 + 1, j * 2].Cr +
                         YCbCrData[i * 2 + 1, j * 2 + 1].Cr) / 4.0) - 128.0;
                }
            }

            return (y, downsampledCb, downsampledCr);
        }

        private static RGBPixel[] CreateOneDimPixelArray(byte[] bytes, long width, long height, int padding)
        {
            var pixelsonedim = new RGBPixel[width * height];
            var z = 0;
            var i = 0;
            var counter = 0;

            for (i = 0; i < bytes.Length - 1;)
            {
                if (padding != 0 && counter != 0 && (counter / 3) % width == 0)
                {
                    i += padding;
                    counter = 0;
                    continue;
                }
                pixelsonedim[z++] = new RGBPixel(bytes[i], bytes[i + 1], bytes[i + 2]);
                i += 3;

                if (padding != 0)
                    counter += 3;
            }
            return pixelsonedim;
        }

        private static YCbCr[,] CreateTwoDimYCbCrPixelArray(RGBPixel[] pixels, long width, long height)
        {
            var twoDimArray = new YCbCr[height, width];
            var count = 0;

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    var pixel = pixels[count];
                    twoDimArray[i, j] = new YCbCr(pixel.R, pixel.G, pixel.B);
                    count++;
                }
            }
            return twoDimArray;
        }

        private static int[] ZigZagScan(double[,] block)
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

        private static Dictionary<int, int> GenerateFrequencyDictionary(int[] scannedData)
        {
            var res = new Dictionary<int, int>();
            var groups = scannedData.GroupBy(x => x)
                .OrderBy(x => x.Count())
                .ToList();

            foreach (var group in groups)
                res.Add(group.First(), group.Count());

            return res;
        }

        private static List<int> DecodeHuffmanData(string encodedData, Dictionary<int, string> dictionary)
        {
            var res = new List<int>();

            var reverseDictionary = dictionary.ToDictionary(x => x.Value, x => x.Key);

            var temp = "";
            foreach (var character in encodedData)
            {
                temp += character;
                if (reverseDictionary.ContainsKey(temp))
                {
                    res.Add(reverseDictionary[temp]);
                    temp = "";
                }
            }

            return res;
        }

        private static double[,] CreateBlockFromZigZagData(int[] zigZagData)
        {
            var res = new double[blockSize, blockSize];

            for (int i = 0; i < blockSize * blockSize; i++)
            {
                int x = CalculationArrays.ZigZagScan[i] / 8;
                int y = CalculationArrays.ZigZagScan[i] % 8;
                res[x, y] = zigZagData[i];
            }

            return res;
        }
        private static string GenerateHuffmanEncodedDataString(List<double[,]> blocks, bool isY = false, bool isCr = false, bool isCb = false)
        {
            var zigZagData = EncodeAllBlocks(blocks);
            var frequency = GenerateFrequencyDictionary(zigZagData);
            var priorityQueue = HuffmanCoding.BuildPriorityQueue(frequency);
            var tree = HuffmanCoding.BuildHuffmanTree(priorityQueue);
            var huffmanDictionary = HuffmanCoding.GenerateHuffmanCodes(tree);

            if (isY)
                HuffmanCoding.YDataDictionary = huffmanDictionary;
            if (isCr)
                HuffmanCoding.CrDataDictionary = huffmanDictionary;
            if (isCb)
                HuffmanCoding.CbDataDictionary = huffmanDictionary;

            return ReturnEncodedString(huffmanDictionary, zigZagData);
        }

        private static string ReturnEncodedString(Dictionary<int, string> huffmanCodes, int[] dataToEncode)
        {
            var res = new List<string>();

            foreach (var value in dataToEncode)
                res.Add(huffmanCodes[value]);

            return String.Join("", res);
        }

        private static int[] EncodeAllBlocks(List<double[,]> blocks)
        {
            var res = new int[8 * 8 * blocks.Count()];
            var stopPoint = 0;
            foreach (var block in blocks)
            {
                var blockToAdd = ZigZagScan(block);
                foreach (var value in blockToAdd)
                    res[stopPoint++] = value;
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
