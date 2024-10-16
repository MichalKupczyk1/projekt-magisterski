namespace ProjektMgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BitmapUtils.LoadBitmapFromFile(@"C:\Users\Michal\Desktop\projekt github\projekt-magisterski\kot.bmp");
            JFIF.CreateJFIFFile(BitmapUtils.PixelBytes, BitmapUtils.Width, BitmapUtils.Height, BitmapUtils.Padding);
        }
    }
}