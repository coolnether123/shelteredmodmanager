using System.IO;

namespace Manager.Core.Games.Models
{
    public sealed class GameRuntimeLayout
    {
        public string RuntimeFolderName { get; set; }
        public string BinFolderName { get; set; }
        public string ModsFolderName { get; set; }
        public string DoorstopConfigFileName { get; set; }
        public string[] DoorstopTargetAssemblyRelativeCandidates { get; set; }
        public string DoorstopProxyDllName { get; set; }
        public string DoorstopSupportFolderName { get; set; }

        public GameRuntimeLayout()
        {
            RuntimeFolderName = "SMM";
            BinFolderName = "bin";
            ModsFolderName = "mods";
            DoorstopConfigFileName = "doorstop_config.ini";
            DoorstopTargetAssemblyRelativeCandidates = new string[]
            {
                @"SMM\bin\Doorstop.dll",
                @"SMM\Doorstop.dll"
            };
            DoorstopProxyDllName = "winhttp.dll";
            DoorstopSupportFolderName = "Doorstop";
        }

        public string GetRuntimePath(string gameDirectory)
        {
            return Path.Combine(gameDirectory ?? string.Empty, RuntimeFolderName ?? string.Empty);
        }

        public string GetBinPath(string gameDirectory)
        {
            return Path.Combine(GetRuntimePath(gameDirectory), BinFolderName ?? string.Empty);
        }

        public string GetModsPath(string gameDirectory)
        {
            return Path.Combine(gameDirectory ?? string.Empty, ModsFolderName ?? "mods");
        }

        public string GetDoorstopSupportPath(string gameDirectory)
        {
            return Path.Combine(GetRuntimePath(gameDirectory), DoorstopSupportFolderName ?? string.Empty);
        }
    }
}
