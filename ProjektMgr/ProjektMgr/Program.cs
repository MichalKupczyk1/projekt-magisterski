namespace ProjektMgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BitmapUtils.LoadBitmapFromFile(@"C:\Users\Michal\Desktop\projekt github\projekt-magisterski\kot.bmp");
            Console.WriteLine(BitmapUtils.Height + " " + BitmapUtils.Width);
        }
    }
}