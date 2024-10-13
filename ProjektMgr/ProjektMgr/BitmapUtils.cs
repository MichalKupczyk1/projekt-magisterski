namespace ProjektMgr
{
    internal static class BitmapUtils
    {
        //public properties later used for Pixel transformation
        public static long Width { get; set; }
        public static long Height { get; set; }
        public static int Padding { get; set; }
        public static byte[] Bytes { get; set; } = new byte[0];
        //private fields for calculations

        public static void LoadBitmapFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                Bytes = File.ReadAllBytes(filePath);
                if (Bytes != null && Bytes.Length > 0)
                    CalculateWidthHeightAndPadding();
            }
        }

        private static void CalculateWidthHeightAndPadding()
        {
            Width = (long)((int)Bytes[18] + (256 * (int)Bytes[19]) + ((Math.Pow(256, 2) * (int)Bytes[20])) + (Math.Pow(256, 3) * (int)Bytes[21]));
            Height = (long)((int)Bytes[22] + (256 * (int)Bytes[23]) + ((Math.Pow(256, 2) * (int)Bytes[24])) + (Math.Pow(256, 3) * (int)Bytes[25]));
            Padding = (Width % 4 != 0) ? (short)(4 - (Width % 4)) : 0;
        }
    }
}
