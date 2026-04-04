using System;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostPathPolicy
    {
        public string ResolveDataRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cortex.Host.Avalonia");
        }

        public string ResolveLogFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "cortex-desktop.log");
        }

        public string ResolveShellStateFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "desktop-shell-state.json");
        }

        public string ResolveDockLayoutFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "desktop-dock-layout.json");
        }
    }
}
