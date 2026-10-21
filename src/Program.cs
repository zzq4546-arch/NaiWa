using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace NaiWa
{
    static class Program
    {
        private static Mutex mutex;

        [STAThread]
        static void Main()
        {
            Log.Write("start v0.1 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            bool createdNew;
            mutex = new Mutex(true, "NaiWa-SingleInstance-9f2c7a", out createdNew);
            if (!createdNew) return;

            try
            {
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var settings = Settings.Load();
                var window = new PetWindow(settings);
                var tray = new TrayIcon(window);

                window.Closed += delegate
                {
                    tray.Dispose();
                    app.Shutdown();
                };

                window.Show();

                if (!settings.TipShown)
                {
                    settings.TipShown = true;
                    settings.Save();
                    tray.ShowStartupTip();
                }

                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "NaiWa");
            }
        }
    }
}