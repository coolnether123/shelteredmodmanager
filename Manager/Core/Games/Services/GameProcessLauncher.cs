using System.Diagnostics;
using System.IO;
using Manager.Core.Models;

namespace Manager.Core.Games.Services
{
    public sealed class GameProcessLauncher
    {
        public bool IsGameRunning(AppSettings settings)
        {
            try
            {
                if (settings == null || string.IsNullOrEmpty(settings.GamePath))
                    return false;

                string processName = Path.GetFileNameWithoutExtension(settings.GamePath);
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public void Launch(AppSettings settings)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = settings.GamePath;
            startInfo.WorkingDirectory = Path.GetDirectoryName(settings.GamePath);
            startInfo.UseShellExecute = false;
            Process.Start(startInfo);
        }
    }
}
