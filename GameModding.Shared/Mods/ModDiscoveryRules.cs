using System;

namespace GameModding.Shared.Mods
{
    public static class ModDiscoveryRules
    {
        public static bool IsReservedFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return true;
            }

            if (string.Equals(folderName, "disabled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "SMM", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "ModAPI", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return folderName.StartsWith("_smm_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
