using ChessClient;

namespace Curs_test
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            // var a = new ChessClient.Form1(1, 10, PlayerTeam.Black); var b = new ChessClient.Form1(0, 10, PlayerTeam.White);
            Application.Run(new ChessClient.Form2());
            /*new Thread(() =>
            {
                Application.Run(b);
            }).Start();
            new Thread(() =>
            {
                Application.Run(a);
            }).Start();*/
        }
    }
}