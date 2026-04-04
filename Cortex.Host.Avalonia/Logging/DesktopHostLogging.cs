using Serilog;

namespace Cortex.Host.Avalonia.Logging
{
    internal static class DesktopHostLogging
    {
        public static void Initialize(string logFilePath)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("Host", "Avalonia")
                .WriteTo.Debug()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();

            Log.Information("Initialized Cortex desktop host logging at {LogFilePath}", logFilePath);
        }

        public static void Dispose()
        {
            Log.CloseAndFlush();
        }
    }
}
