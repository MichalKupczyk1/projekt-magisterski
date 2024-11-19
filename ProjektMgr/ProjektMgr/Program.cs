namespace ProjektMgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BitmapUtils.LoadBitmapFromFile(@"C:\Users\Michal\Desktop\projekt github\projekt-magisterski\kot.bmp");
            var jfifRes = JFIF.CreateJFIFFile(BitmapUtils.PixelBytes, BitmapUtils.Width, BitmapUtils.Height, BitmapUtils.Padding);

            var res = new byte[BitmapUtils.PixelBytes.Length + BitmapUtils.HeaderInfoBytes.Length];

            Array.Copy(BitmapUtils.HeaderInfoBytes, 0, res, 0, BitmapUtils.HeaderInfoBytes.Length);
            Array.Copy(jfifRes, 0, res, 54, jfifRes.Length);

            var path = "C:\\Users\\Michal\\Desktop\\projekt github\\projekt-magisterski\\res.bmp";
            File.WriteAllBytes(path, res);
        }
    }
}