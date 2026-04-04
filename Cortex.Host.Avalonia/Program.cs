using Avalonia;
using Cortex.Host.Avalonia.Composition;

namespace Cortex.Host.Avalonia
{
    internal static class Program
    {
        [System.STAThread]
        public static void Main(string[] args)
        {
            new DesktopHostLaunchCoordinator().Run(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
