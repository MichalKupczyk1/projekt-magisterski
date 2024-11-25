using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using static System.Reflection.Metadata.BlobBuilder;

namespace ProjektMgr
{
    //jpeg jfif
    public static class JFIF
    {
        //domyslnie 8x8, wedlug standardu jfif
        private static int blockSize = 8;

        public static async Task<byte[]> CreateJFIFFile(byte[] bytes, long width, long height, int padding)
        {
            var arrays = CreateTwoDimYCbCrArrays(bytes, width, height, padding);
            var downsampledData = ApplyDownsamplingAndExtendArrays(arrays.Y, arrays.Cb, arrays.Cr);

            var YBlocks = SeparateIntoBlocks(downsampledData.Y);
            var CbBlocks = SeparateIntoBlocks(downsampledData.Cb);
            var CrBlocks = SeparateIntoBlocks(downsampledData.Cr);

            var YDCT = ApplyDCTAndQuantization(YBlocks, true).Result;
            var CbDCT = ApplyDCTAndQuantization(CbBlocks).Result;
            var CrDCT = ApplyDCTAndQuantization(CrBlocks).Result;

            var encodedY = GenerateHuffmanEncodedDataString(YDCT, isY: true);
            var encodedCb = GenerateHuffmanEncodedDataString(CbDCT, isCb: true);
            var encodedCr = GenerateHuffmanEncodedDataString(CrDCT, isCr: true);

            Console.WriteLine("File size after encoding: " + ((encodedY.Length + encodedCb.Length + encodedCr.Length) / 1000000.0).ToString("F2") + "mb");

            var decodedY = DecodeHuffmanData(encodedY, HuffmanCoding.YDataDictionary);
            var decodedCb = DecodeHuffmanData(encodedCb, HuffmanCoding.CbDataDictionary);
            var decodedCr = DecodeHuffmanData(encodedCr, HuffmanCoding.CrDataDictionary);

            var decodedYBlocks = DecodeZigZagDataIntoBlocks(decodedY);
            var decodedCbBlocks = DecodeZigZagDataIntoBlocks(decodedCb);
            var decodedCrBlocks = DecodeZigZagDataIntoBlocks(decodedCr);

            var dequantizedYBlocks = ApplyIDCTAndDequantize(decodedYBlocks, true).Result;
            var dequantizedCbBlocks = ApplyIDCTAndDequantize(decodedCbBlocks).Result;
            var dequantizedCrBlocks = ApplyIDCTAndDequantize(decodedCrBlocks).Result;

            var MergedY = MergeBlocksInto2DArray(dequantizedYBlocks, width, height, true);
            var MergedCb = MergeBlocksInto2DArray(dequantizedCbBlocks, width, height);
            var MergedCr = MergeBlocksInto2DArray(dequantizedCrBlocks, width, height);

            var rgb = YCbCrToRGB(height, width, MergedY, MergedCb, MergedCr);
            var result = PixelToByteArray(rgb);

            return result;
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

        private static double[,] MergeBlocksInto2DArray(double[][,] blocks, long width, long height, bool isY = false)
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

        private static async Task<double[][,]> ApplyIDCTAndDequantize(double[][,] blocks, bool isY = false)
        {
            var threadsAvailable = Environment.ProcessorCount;
            var chunkSize = (int)Math.Ceiling((double)blocks.Length / threadsAvailable);

            var results = new double[blocks.Length][,];
            var tasks = new Task[threadsAvailable];

            for (var thread = 0; thread < threadsAvailable; thread++)
            {
                var start = thread * chunkSize;
                var end = Math.Min(start + chunkSize, blocks.Length);

                tasks[thread] = Task.Run(() =>
                {
                    for (int i = start; i < end; i++)
                    {
                        var block = blocks[i];
                        for (int x = 0; x < blockSize; x++)
                            for (int y = 0; y < blockSize; y++)
                                block[x, y] = isY ? block[x, y] * CalculationArrays.QuantizationYTable[x, y] : block[x, y] * CalculationArrays.QuantizationCbCrTable[x, y];

                        var res = new double[blockSize, blockSize];
                        var cos = new double[blockSize, blockSize];

                        for (int u = 0; u < blockSize; u++)
                            for (int x = 0; x < blockSize; x++)
                                cos[u, x] = Math.Cos((Math.PI * (2.0 * x + 1.0) * u) / (2.0 * blockSize));

                        for (int a = 0; a < blockSize; a++)
                        {
                            for (int b = 0; b < blockSize; b++)
                            {
                                var sum = 0.0;
                                for (int x = 0; x < blockSize; x++)
                                {
                                    for (int y = 0; y < blockSize; y++)
                                    {
                                        var cu = x == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                                        var cv = y == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                                        sum += block[x, y] * cu * cv * cos[x, a] * cos[y, b];
                                    }
                                }
                                res[a, b] = 0.25 * sum;
                            }
                        }
                        results[i] = res;
                    }
                });
            }
            await Task.WhenAll(tasks);
            return results;
        }


        private static double[,] ApplyIDCT(double[,] block)
        {
            var res = new double[blockSize, blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                {
                    var sum = 0.0;
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
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

        private static async Task<double[][,]> ApplyDCTAndQuantization(double[][,] blocks, bool isY = false)
        {
            var threadsAvailable = Environment.ProcessorCount;
            var chunkSize = (int)Math.Ceiling((double)blocks.Length / threadsAvailable);

            var results = new double[blocks.Length][,];
            var tasks = new Task[threadsAvailable];

            for (var thread = 0; thread < threadsAvailable; thread++)
            {
                var start = thread * chunkSize;
                var end = Math.Min(start + chunkSize, blocks.Length);

                tasks[thread] = Task.Run(() =>
                {
                    for (int i = start; i < end; i++)
                    {
                        var block = blocks[i];

                        var res = new double[blockSize, blockSize];
                        var cos = new double[blockSize, blockSize];

                        for (int u = 0; u < blockSize; u++)
                            for (int x = 0; x < blockSize; x++)
                                cos[u, x] = Math.Cos((Math.PI * (2.0 * x + 1.0) * u) / (2.0 * blockSize));

                        for (int a = 0; a < blockSize; a++)
                        {
                            for (int b = 0; b < blockSize; b++)
                            {
                                var cu = a == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                                var cv = b == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                                var sum = 0.0;

                                for (int x = 0; x < blockSize; x++)
                                    for (int y = 0; y < blockSize; y++)
                                        sum += block[x, y] * cos[a, x] * cos[b, y];

                                res[a, b] = 0.25 * cu * cv * sum;
                            }
                        }
                        for (int x = 0; x < blockSize; x++)
                            for (int y = 0; y < blockSize; y++)
                                block[x, y] = isY ? Math.Round(res[x, y] / CalculationArrays.QuantizationYTable[x, y]) : Math.Round(res[x, y] / CalculationArrays.QuantizationCbCrTable[x, y]);
                        results[i] = block;
                    }
                });
            }
            await Task.WhenAll(tasks);
            return results;
        }

        private static double[,] ApplyDCT(double[,] block)
        {
            var res = new double[blockSize, blockSize];
            var multipliedBlockSize = 2.0 * blockSize;
            var cu = 0.0;
            var cv = 0.0;
            var sum = 0.0;
            var pi = Math.PI;

            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                {
                    sum = 0.0;
                    cu = i == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                    cv = j == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                            sum += block[x, y] * Math.Cos(((pi * (2.0 * x + 1.0) * i) / (multipliedBlockSize))) * Math.Cos(((pi * (2.0 * y + 1.0) * j) / (multipliedBlockSize)));
                    }
                    res[i, j] = 0.25 * cu * cv * sum;
                }
            }
            return res;
        }

        private static double[][,] SeparateIntoBlocks(double[,] data)
        {
            var height = data.GetLength(0);
            var width = data.GetLength(1);
            var counter = 0;
            var resArray = new double[((height - blockSize) * (width - blockSize)) / (blockSize * blockSize)][,];

            for (int i = 0; i < height - blockSize; i += blockSize)
            {
                for (int j = 0; j < width - blockSize; j += blockSize)
                {
                    var block = new double[blockSize, blockSize];
                    for (int x = 0; x < blockSize; x++)
                        for (int y = 0; y < blockSize; y++)
                            block[x, y] = data[i + x, y + j];
                    resArray[counter++] = block;
                }
            }
            return resArray;
        }

        private static (double[,] Y, double[,] Cb, double[,] Cr) ApplyDownsamplingAndExtendArrays(double[,] yArray, double[,] cbArray, double[,] crArray)
        {
            var height = yArray.GetLength(0);
            var width = yArray.GetLength(1);

            var extendedHeight = (long)Math.Ceiling(height / 8.0) * 8;
            var extendedWidth = (long)Math.Ceiling(width / 8.0) * 8;

            var resY = new double[extendedHeight, extendedWidth];

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    resY[i, j] = yArray[i, j] - 128.0;

            resY = ExtendArray(resY, height, extendedHeight, width, extendedWidth);

            var downsampledHeight = height / 2;
            var downsampledWidth = width / 2;

            extendedHeight = (long)Math.Ceiling(downsampledHeight / 8.0) * 8;
            extendedWidth = (long)Math.Ceiling(downsampledWidth / 8.0) * 8;

            var downsampledCb = new double[extendedHeight, extendedWidth];
            var downsampledCr = new double[extendedHeight, extendedWidth];

            for (int i = 0; i < downsampledHeight; i++)
            {
                for (int j = 0; j < downsampledWidth; j++)
                {
                    downsampledCb[i, j] =
                        ((cbArray[i * 2, j * 2] +
                         cbArray[i * 2, j * 2 + 1] +
                         cbArray[i * 2 + 1, j * 2] +
                         cbArray[i * 2 + 1, j * 2 + 1]) / 4.0) - 128.0;

                    downsampledCr[i, j] =
                        ((crArray[i * 2, j * 2] +
                         crArray[i * 2, j * 2 + 1] +
                         crArray[i * 2 + 1, j * 2] +
                         crArray[i * 2 + 1, j * 2 + 1]) / 4.0) - 128.0;
                }
            }
            downsampledCb = ExtendArray(downsampledCb, downsampledHeight, extendedHeight, downsampledWidth, extendedWidth);
            downsampledCr = ExtendArray(downsampledCr, downsampledHeight, extendedHeight, downsampledWidth, extendedWidth);

            return (resY, downsampledCb, downsampledCr);
        }

        private static double[,] ExtendArray(double[,] resArr, long currentHeight, long newHeight, long currentWidth, long newWidth)
        {
            for (var i = 0; i < currentHeight; i++)
            {
                for (var j = currentWidth; j < newWidth; j++)
                    resArr[i, j] = resArr[i, currentWidth - 1];
            }

            for (var i = currentHeight; i < newHeight; i++)
            {
                for (var j = 0; j < newHeight; j++)
                    resArr[i, j] = resArr[currentHeight - 1, j];
            }
            return resArr;
        }

        private static RGBPixel[] CreateOneDimPixelArray(byte[] bytes, long width, long height, int padding)
        {
            var pixelsOneDim = new RGBPixel[width * height];
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
                pixelsOneDim[z++] = new RGBPixel(bytes[i], bytes[i + 1], bytes[i + 2]);
                i += 3;

                if (padding != 0)
                    counter += 3;
            }

            return pixelsOneDim;
        }


        private static (double[,] Y, double[,] Cb, double[,] Cr) CreateTwoDimYCbCrArrays(byte[] bytes, long width, long height, int padding)
        {
            var YArray = new double[height, width];
            var CbArray = new double[height, width];
            var CrArray = new double[height, width];
            var counter = 0;
            byte r = 0, g = 0, b = 0;
            var temp = 0;

            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    if (padding != 0 && counter != 0 && (counter / 3) % width == 0)
                    {
                        temp += padding;
                        counter = 0;
                        continue;
                    }

                    b = bytes[temp];
                    g = bytes[temp + 1];
                    r = bytes[temp + 2];
                    YArray[i, j] = Math.Clamp(0.299 * r + 0.587 * g + 0.114 * b, 0, 255);
                    CbArray[i, j] = Math.Clamp(-0.168736 * r - 0.331264 * g + 0.5 * b + 128, 0, 255);
                    CrArray[i, j] = Math.Clamp(0.5 * r - 0.418688 * g - 0.081312 * b + 128, 0, 255);
                    temp += 3;

                    if (padding != 0)
                        counter += 3;
                }
            }
            return (YArray, CbArray, CrArray);
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

        private static double[][,] DecodeZigZagDataIntoBlocks(List<int> decodedData)
        {
            var resList = new List<double[,]>();
            var currentPos = 0;
            var blockShift = 64;

            while (currentPos < decodedData.Count())
            {
                var first64 = decodedData.GetRange(currentPos, blockShift).ToArray();
                var resBlock = new double[blockSize, blockSize];

                for (int i = 0; i < blockSize * blockSize; i++)
                {
                    int x = CalculationArrays.ZigZagScan[i] / 8;
                    int y = CalculationArrays.ZigZagScan[i] % 8;
                    resBlock[x, y] = first64[i];
                }
                resList.Add(resBlock);
                currentPos += blockShift;
            }

            var res = new double[resList.Count()][,];
            var counter = 0;

            foreach (var resBlock in resList)
                res[counter++] = resBlock;

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

        private static string GenerateHuffmanEncodedDataString(double[][,] blocks, bool isY = false, bool isCr = false, bool isCb = false)
        {
            //- 0.1 0.2s, po wyciagnieciu funkcji
            var zigZagData = new int[8 * 8 * blocks.Count()];
            var stopPoint = 0;

            foreach (var block in blocks)
            {
                var blockToAdd = new int[64];
                for (int i = 0; i < 64; i++)
                {
                    int x = CalculationArrays.ZigZagScan[i] / 8;
                    int y = CalculationArrays.ZigZagScan[i] % 8;
                    blockToAdd[i] = (int)block[x, y];
                }
                foreach (var value in blockToAdd)
                    zigZagData[stopPoint++] = value;
            }

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

        private static int[] EncodeAllBlocks(double[][,] blocks)
        {
            var res = new int[8 * 8 * blocks.Count()];
            var stopPoint = 0;

            foreach (var block in blocks)
            {
                var blockToAdd = new int[64];
                for (int i = 0; i < 64; i++)
                {
                    int x = CalculationArrays.ZigZagScan[i] / 8;
                    int y = CalculationArrays.ZigZagScan[i] % 8;
                    blockToAdd[i] = (int)block[x, y];
                }
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
