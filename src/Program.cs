using System;
using System.Windows;

namespace CodexPeek
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            if (args.Length > 0 && String.Equals(args[0], "--test", StringComparison.OrdinalIgnoreCase))
            {
                var config = PeekConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
                var overlay = new OverlayWindow(config);
                overlay.Closed += delegate { app.Shutdown(); };
                overlay.Show();
                app.Run();
                return;
            }

            using (var context = new PeekContext())
            {
                app.Run();
            }
        }
    }
}
