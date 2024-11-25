using System;
using System.Numerics;
using System.Linq;

class HuffmanCoding
{
    private static int blockSize = 8;

    public static void Main(string[] args)
    {
        var arr = new double[1000000][,];
        var length = arr.Length;
        for (int i = 0; i < length; i++)
            arr[i] = new double[blockSize, blockSize];

        var sum = 0.0;
        Parallel.ForEach(arr, block =>
        {
            block = CalculateDCT(block);
        });
    }

    public static double[,] CalculateDCT(double[,] arr)
    {
        var height = arr.GetLength(0);
        var width = arr.GetLength(1);
        var res = new double[height, width];

        for (int a = 0; a < blockSize; a++)
        {
            for (int b = 0; b < blockSize; b++)
            {
                var cu = a == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                var cv = b == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                var sum = 0.0;

                // SIMD optimized summation over a block
                Vector<double> vectorSum = new Vector<double>(0.0);
                for (int x = 0; x < blockSize; x++)
                {
                    for (int y = 0; y < blockSize; y += Vector<double>.Count)  // Loop with SIMD step size
                    {
                        // Take a slice of the block for the current y value
                        var data = new Vector<double>(arr.Cast<double>().Skip(x * blockSize + y).Take(Vector<double>.Count).ToArray());
                        vectorSum += data;
                    }
                }

                // Sum all elements in the vector
                sum = 0.0;
                for (int i = 0; i < Vector<double>.Count; i++)
                {
                    sum += vectorSum[i]; // Extracting elements from the vector and summing them
                }

                res[a, b] = 0.25 * cu * cv * sum;
            }
        }

        return res;
    }
}
