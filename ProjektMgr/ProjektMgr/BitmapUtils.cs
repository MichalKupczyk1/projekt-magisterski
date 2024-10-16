namespace ProjektMgr
{
    internal static class BitmapUtils
    {
        //public properties later used for Pixel transformation
        public static long Width { get; set; }
        public static long Height { get; set; }
        public static int Padding { get; set; }
        public static byte[] HeaderInfoBytes { get; set; } = new byte[0];
        public static byte[] PixelBytes { get; set; } = new byte[0];

        public static void LoadBitmapFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var bytes = File.ReadAllBytes(filePath);
                if (bytes != null && bytes.Length > 0)
                {
                    CalculateWidthHeightAndPadding(bytes);

                    HeaderInfoBytes = new byte[54];
                    Array.Copy(bytes, 0, HeaderInfoBytes, 0, HeaderInfoBytes.Length);
                    PixelBytes = new byte[bytes.Length - 54];
                    Array.Copy(bytes, 54, PixelBytes, 0, PixelBytes.Length);
                }
            }
        }

        private static void CalculateWidthHeightAndPadding(byte[] pixels)
        {
            Width = (long)((int)pixels[18] + (256 * (int)pixels[19]) + ((Math.Pow(256, 2) * (int)pixels[20])) + (Math.Pow(256, 3) * (int)pixels[21]));
            Height = (long)((int)pixels[22] + (256 * (int)pixels[23]) + ((Math.Pow(256, 2) * (int)pixels[24])) + (Math.Pow(256, 3) * (int)pixels[25]));
            Padding = (Width % 4 != 0) ? (short)(4 - (Width % 4)) : 0;
        }
    }
}
