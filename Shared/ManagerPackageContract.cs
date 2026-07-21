using System;
using System.IO;

namespace ShelteredModManager.Update
{
    internal static class ManagerPackageContract
    {
        private static readonly string[] RequiredFiles =
        {
            "Manager.exe",
            "ManagerUpdater.exe",
            "ModAPI.dll",
            @"bin\ShelteredAPI.dll",
            @"bin\Doorstop.dll",
            @"bin\0Harmony.dll",
            @"Doorstop\x86\winhttp.dll",
            @"Doorstop\x64\winhttp.dll"
        };

        public static bool TryValidateRoot(string rootPath, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                errorMessage = "The update archive does not contain an SMM package.";
                return false;
            }

            for (int i = 0; i < RequiredFiles.Length; i++)
            {
                if (!File.Exists(Path.Combine(rootPath, RequiredFiles[i])))
                {
                    errorMessage = "The update package is incomplete. Missing " +
                        RequiredFiles[i] + ".";
                    return false;
                }
            }
            return true;
        }
    }
}
