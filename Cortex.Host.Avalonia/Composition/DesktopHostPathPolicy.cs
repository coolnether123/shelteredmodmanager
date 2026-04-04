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
    }
}
