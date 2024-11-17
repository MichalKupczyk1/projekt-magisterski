using System.Diagnostics;

namespace ProjektMgr
{
    //jpeg jfif
    public static class JFIF
    {
        //domyslnie 8x8, wedlug standardu jfif
        private static int blockSize = 8;
        private static int padding = 0;

        private static ArrayInfo YArrayInfo { get; set; } = new ArrayInfo();
        private static ArrayInfo CbCrArrayInfo { get; set; } = new ArrayInfo();

        public static byte[] CreateJFIFFile(byte[] bytes, long width, long height, int padding)
        {
            SetStartingWidthAndHeight(width, height, padding);
            Console.WriteLine("File size before encoding: " + (bytes.Length / 1048576.0).ToString("F2") + "mb");
            //encoding
            var encodedData = EncodeData(bytes, width, height, padding);
            //decoding
            var decodedData = DecodeData(encodedData.Y, encodedData.Cr, encodedData.Cb, width, height);

            return decodedData;
        }

        private static void SetStartingWidthAndHeight(long width, long height, int pad)
        {
            YArrayInfo.StartingHeight = height;
            YArrayInfo.StartingWidth = width;

            CbCrArrayInfo.StartingHeight = height;
            CbCrArrayInfo.StartingWidth = width;

            padding = pad;
        }

        private static (string Y, string Cr, string Cb) EncodeData(byte[] bytes, long width, long height, int padding)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var downsampledArray = GenerateDownsampledArray(bytes, width, height, padding);
            var encodedData = ReturnHuffmanEncodedData(downsampledArray);
            Console.WriteLine("Total encoding time: " + (sw.ElapsedMilliseconds / 1000.0).ToString() + "s");

            var totalLenght = encodedData.Y.Length + encodedData.Cb.Length + encodedData.Cr.Length;
            Console.WriteLine("File size after encoding: " + (totalLenght / 1048576.0).ToString("F2") + "mb");

            return encodedData;
        }

        #region encoding
        private static (string Y, string Cr, string Cb) ReturnHuffmanEncodedData((float[,] Y, float[,] Cb, float[,] Cr) downsampledArray)
        {
            var Y = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Y, true));
            var Cr = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Cr));
            var Cb = ShiftValuesInArray(ExtendArrayIfNeeded(downsampledArray.Cb));

            var YDCT = ReturnQuantizedBlock(Y, true);
            var CrDCT = ReturnQuantizedBlock(Cr);
            var CbDCT = ReturnQuantizedBlock(Cb);

            var res = (GenerateHuffmanEncodedDataString(YDCT, isY: true), GenerateHuffmanEncodedDataString(CrDCT, isCr: true), GenerateHuffmanEncodedDataString(CbDCT, isCb: true));

            return res;
        }

        private static List<float[,]> ReturnQuantizedBlock(float[,] block, bool isY = false)
        {
            var res = new List<float[,]>();
            var dcts = ApplyDCTToBlock(block);
            foreach (var dct in dcts)
                res.Add(isY ? QuantizeYBlock(dct) : QuantizeCbCrBlock(dct));
            return res;
        }

        private static (float[,] Y, float[,] Cb, float[,] Cr) GenerateDownsampledArray(byte[] bytes, long width, long height, int padding)
        {
            var oneDimArray = CreateOneDimRGBPixelArray(bytes, width, height, padding);
            var YCbCrArray = CreateTwoDimYCbCrPixelArray(oneDimArray, width, height);
            return ApplyDownsampling(YCbCrArray, width, height);
        }

        private static string GenerateHuffmanEncodedDataString(List<float[,]> blocks, bool isY = false, bool isCr = false, bool isCb = false)
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

        private static RGBPixel[] CreateOneDimRGBPixelArray(byte[] bytes, long width, long height, int padding)
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

        private static float[,] ExtendArrayIfNeeded(float[,] array, bool isY = false)
        {
            var originalRows = array.GetLength(0);
            var originalCols = array.GetLength(1);

            var rowsToAdd = (originalRows % 8 != 0) ? 8 - (originalRows % 8) : 0;
            var colsToAdd = (originalCols % 8 != 0) ? 8 - (originalCols % 8) : 0;

            if (rowsToAdd == 0 && colsToAdd == 0)
                return array;

            long extendedHeight = 0;
            long extendedWidth = 0;

            if (isY)
            {
                YArrayInfo.ExtendedHeight = originalRows + rowsToAdd;
                YArrayInfo.ExtendedWidth = originalCols + colsToAdd;
                extendedHeight = YArrayInfo.ExtendedHeight;
                extendedWidth = YArrayInfo.ExtendedWidth;
            }
            else
            {
                CbCrArrayInfo.ExtendedHeight = originalRows + rowsToAdd;
                CbCrArrayInfo.ExtendedWidth = originalCols + colsToAdd;
                extendedHeight = CbCrArrayInfo.ExtendedHeight;
                extendedWidth = CbCrArrayInfo.ExtendedWidth;
            }

            float[,] newArray = new float[extendedHeight, extendedWidth];

            for (int i = 0; i < extendedHeight; i++)
            {
                for (int j = 0; j < extendedWidth; j++)
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
            var height = block.GetLength(0);
            var width = block.GetLength(1);
            var result = new float[height, width];

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
            var height = array.GetLength(0);
            var width = array.GetLength(1);

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    array[i, j] -= 128;

            return array;
        }

        private static List<float[,]> ApplyDCTToBlock(float[,] array)
        {
            var height = array.GetLength(0);
            var width = array.GetLength(1);
            var res = new List<float[,]>();

            for (int i = 0; i < height; i += blockSize)
            {
                for (int j = 0; j < width; j += blockSize)
                {
                    var block = new float[blockSize, blockSize];
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                            block[x, y] = array[i + x, j + y];
                    }
                    res.Add(CalculateDCT(block));
                }
            }
            return res;
        }

        private static float[,] QuantizeYBlock(float[,] block)
        {
            var res = new float[blockSize, blockSize];

            for (int i = 0; i < blockSize; i++)
                for (int j = 0; j < blockSize; j++)
                    res[i, j] = (float)Math.Round(block[i, j] / CalculationArrays.QuantizationYTable[i, j]);

            return res;
        }

        private static float[,] QuantizeCbCrBlock(float[,] block)
        {
            var res = new float[blockSize, blockSize];

            for (int i = 0; i < blockSize; i++)
                for (int j = 0; j < blockSize; j++)
                    res[i, j] = (float)Math.Round(block[i, j] / CalculationArrays.QuantizationCbCrTable[i, j]);

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

        private static int[] EncodeAllBlocks(List<float[,]> blocks)
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
        #endregion

        #region decoding
        private static byte[] DecodeData(string Y, string Cr, string Cb, long width, long height)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var decodedY = DecodeHuffmanData(Y, HuffmanCoding.YDataDictionary);
            var decodedCr = DecodeHuffmanData(Cr, HuffmanCoding.CrDataDictionary);
            var decodedCb = DecodeHuffmanData(Cb, HuffmanCoding.CbDataDictionary);

            var decodedZigZagY = DecodeZigZagDataIntoBlocks(decodedY);
            var decodedZigZagCb = DecodeZigZagDataIntoBlocks(decodedCr);
            var decodedZigZagCr = DecodeZigZagDataIntoBlocks(decodedCb);

            var dequantizedReshiftedY = DequantizeBlocks(decodedZigZagY, true);
            var dequantizedReshiftedCr = DequantizeBlocks(decodedZigZagCr);
            var dequantizedReshiftedCb = DequantizeBlocks(decodedZigZagCb);

            var extendedY = Return2DArrayFromDequantizedList(dequantizedReshiftedY, true);
            var extendedCb = Return2DArrayFromDequantizedList(dequantizedReshiftedCb);
            var extendedCr = Return2DArrayFromDequantizedList(dequantizedReshiftedCr);

            var originalY = RestoreOriginalSize(extendedY, true);
            var originalCb = RestoreOriginalSize(extendedCb);
            var originalCr = RestoreOriginalSize(extendedCr);

            var rgbData = ReturnRGBArray(originalY, originalCb, originalCr);
            var res = PixelToByteArray(rgbData);

            Console.WriteLine("Decoding time: " + (sw.ElapsedMilliseconds / 1000.0).ToString() + "s");
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
                result[i] = oneDimPixels[a].R;
                result[i + 1] = oneDimPixels[a].G;
                result[i + 2] = oneDimPixels[a].B;
                i += 3;
                a++;
            }
            return result;
        }

        private static RGBPixel[,] ReturnRGBArray(float[,] Y, float[,] Cb, float[,] Cr)
        {
            var width = YArrayInfo.StartingWidth;
            var height = YArrayInfo.StartingHeight;
            var res = new RGBPixel[height, width];

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                    res[i, j] = new RGBPixel(Y[i, j], Cb[i, j], Cr[i, j]);
            }
            return res;
        }

        private static float[,] RestoreOriginalSize(float[,] downsampledData, bool isY = false)
        {
            var width = isY ? YArrayInfo.StartingWidth : CbCrArrayInfo.StartingWidth;
            var height = isY ? YArrayInfo.StartingHeight : CbCrArrayInfo.StartingHeight;

            var xIter = 0;
            var yIter = 0;

            var firstRow = true;
            var firstCol = true;

            var res = new float[height, width];

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (isY)
                        res[i, j] = downsampledData[i, j];
                    else
                    {
                        if (j % 2 == 0 && !firstCol)
                            xIter++;
                        if (j % 2 == 0 && firstCol)
                            firstCol = false;

                        res[i, j] = downsampledData[yIter, xIter];
                    }
                }
                if (i % 2 == 0 && !firstRow)
                    yIter++;
                if (i % 2 == 0 && firstRow)
                    firstRow = false;
                xIter = 0;
            }

            return res;
        }

        private static float[,] Return2DArrayFromDequantizedList(List<float[,]> dequantizedData, bool isY = false)
        {
            var extendedHeight = isY ? YArrayInfo.ExtendedHeight : CbCrArrayInfo.ExtendedHeight;
            var extendedWidth = isY ? YArrayInfo.ExtendedWidth : CbCrArrayInfo.ExtendedWidth;

            var res = new float[extendedHeight, extendedWidth];
            var iter = 0;

            for (int i = 0; i < extendedHeight; i += blockSize)
            {
                for (int j = 0; j < extendedWidth; j += blockSize)
                {
                    for (int x = 0; x < blockSize; x++)
                        for (int y = 0; y < blockSize; y++)
                            res[i + x, j + y] = dequantizedData.ElementAt(iter)[x, y];
                    iter++;
                }
            }
            return res;
        }

        private static float[,] CalculateIDCTAndReshiftValues(float[,] block)
        {
            var alfaU = (float)(1 / Math.Sqrt(2));
            var alfaV = (float)(1 / Math.Sqrt(2));
            var pi = Math.PI;
            var blockSizeMultiplied = 2 * blockSize;
            var result = new float[blockSize, blockSize];
            var sum = 0.0f;

            for (int x = 0; x < blockSize; x++)
            {
                sum = 0.0f;
                for (int y = 0; y < blockSize; y++)
                {
                    for (int u = 0; u < blockSize; u++)
                    {
                        for (int v = 0; v < blockSize; v++)
                        {
                            float alphaU = (u == 0) ? alfaU : 1.0f;
                            float alphaV = (v == 0) ? alfaV : 1.0f;

                            sum += alphaU * alphaV * block[u, v] *
                                   (float)Math.Cos(((2 * x + 1) * u * pi) / (blockSizeMultiplied)) *
                                   (float)Math.Cos(((2 * y + 1) * v * pi) / (blockSizeMultiplied));
                        }
                    }
                    result[x, y] = 0.25f * sum + 128;
                }
            }
            return result;
        }

        private static List<float[,]> DequantizeBlocks(List<float[,]> blocks, bool isY = false)
        {
            for (int b = 0; b < blocks.Count(); b++)
            {
                var block = blocks[b];
                blocks[b] = CalculateIDCTAndReshiftValues(block);

                for (int i = 0; i < blockSize; i++)
                {
                    for (int j = 0; j < blockSize; j++)
                        block[i, j] = isY ? block[i, j] * CalculationArrays.QuantizationYTable[i, j] : block[i, j] * CalculationArrays.QuantizationCbCrTable[i, j];
                }
            }
            return blocks;
        }

        private static List<float[,]> DecodeZigZagDataIntoBlocks(List<int> decodedData)
        {
            var res = new List<float[,]>();
            var currentPos = 0;
            var blockShift = 64;

            while (currentPos < decodedData.Count())
            {
                var first64 = decodedData.GetRange(currentPos, blockShift).ToArray();
                res.Add(CreateBlockFromZigZagData(first64));
                currentPos += blockShift;
            }
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

        private static float[,] CreateBlockFromZigZagData(int[] zigZagData)
        {
            var res = new float[blockSize, blockSize];

            for (int i = 0; i < blockSize * blockSize; i++)
            {
                int x = CalculationArrays.ZigZagScan[i] / 8;
                int y = CalculationArrays.ZigZagScan[i] % 8;
                res[x, y] = zigZagData[i];
            }

            return res;
        }
        #endregion
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
