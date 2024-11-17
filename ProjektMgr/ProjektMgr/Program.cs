namespace ProjektMgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BitmapUtils.LoadBitmapFromFile(@"C:\Users\Michal\Desktop\projekt github\projekt-magisterski\kot.bmp");
            var jfifRes = JFIF.CreateJFIFFile(BitmapUtils.PixelBytes, BitmapUtils.Width, BitmapUtils.Height, BitmapUtils.Padding);

            var res = new byte[BitmapUtils.PixelBytes.Length + BitmapUtils.HeaderInfoBytes.Length];

            Console.WriteLine("similarity: " + CalculateSimilarity(jfifRes, BitmapUtils.PixelBytes).ToString("F4"));

            Array.Copy(BitmapUtils.HeaderInfoBytes, 0, res, 0, BitmapUtils.HeaderInfoBytes.Length);
            Array.Copy(jfifRes, 0, res, 54, jfifRes.Length);

            var path = "C:\\Users\\Michal\\Desktop\\projekt github\\projekt-magisterski\\res.bmp";
            File.WriteAllBytes(path, res);
        }
        public static double CalculateSimilarity(byte[] array1, byte[] array2)
        {
            int matchingBytes = 0;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] == array2[i])
                    matchingBytes++;
            }

            return (double)matchingBytes / array1.Length * 100.0;
        }

    }
}