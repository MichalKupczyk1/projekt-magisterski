namespace ProjektMgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BitmapUtils.LoadBitmapFromFile(@"kot.bmp");
            JFIF.CreateJFIFFile(BitmapUtils.Bytes, BitmapUtils.Width, BitmapUtils.Height, BitmapUtils.Padding);
        }
    }
}