using Avalonia;
using Avalonia.Skia;
using Cortex.Host.Avalonia.Composition;
using Serilog;

namespace Cortex.Host.Avalonia
{
    internal static class Program
    {
        [System.STAThread]
        public static void Main(string[] args)
        {
            try
            {
                new DesktopHostLaunchCoordinator().Run(args);
            }
            catch (System.Exception ex)
            {
                Log.Fatal(ex, "Cortex desktop host terminated during startup.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseWin32()
                .UseSkia()
                .LogToTrace();
        }
    }
}
