using System;
using System.IO;

namespace Manager.Core.Services
{
    internal static class ModPackageSafety
    {
        internal static bool TryReadRequiredAbout(
            string modRoot,
            out global::Manager.ModTypes.ModAboutInfo about,
            out string normalizedId,
            out string errorMessage)
        {
            about = null;
            normalizedId = string.Empty;
            errorMessage = null;

            if (string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
            {
                errorMessage = "Mod folder does not exist.";
                return false;
            }

            string displayName;
            string previewPath;
            if (!global::Manager.ModAboutReader.TryLoad(modRoot, out about, out normalizedId, out displayName, out previewPath) || about == null)
            {
                errorMessage = "Mod is missing a readable About/About.json file.";
                return false;
            }

            if (string.IsNullOrEmpty(about.id) || string.IsNullOrEmpty(about.name) || string.IsNullOrEmpty(about.version))
            {
                errorMessage = "About.json is missing required id, name, or version metadata.";
                return false;
            }

            normalizedId = NormalizeModId(about.id);
            return true;
        }

        internal static bool ValidateUploadRoot(
            string modRoot,
            out global::Manager.ModTypes.ModAboutInfo about,
            out string normalizedId,
            out string errorMessage)
        {
            if (!TryReadRequiredAbout(modRoot, out about, out normalizedId, out errorMessage))
                return false;

            string folderName = Path.GetFileName(Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (IsReservedModFolderName(folderName))
            {
                errorMessage = "Reserved manager/runtime folders cannot be packaged as Nexus mods: " + folderName + ".";
                return false;
            }

            return true;
        }

        internal static bool ValidateInstallTarget(
            string modsPath,
            string targetPath,
            string sourceModId,
            NexusInstallTargetContext targetContext,
            out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
            {
                errorMessage = "Mods folder is not configured.";
                return false;
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                errorMessage = "Install target folder is empty.";
                return false;
            }

            string fullModsPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(modsPath));
            string fullTargetPath = Path.GetFullPath(targetPath);
            if (!IsDirectChildOf(fullModsPath, fullTargetPath))
            {
                errorMessage = "Nexus installs must target one direct mod folder under the configured Mods folder.";
                return false;
            }

            string folderName = Path.GetFileName(fullTargetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (IsReservedModFolderName(folderName))
            {
                errorMessage = "Nexus install refused reserved manager/runtime folder name: " + folderName + ".";
                return false;
            }

            if (targetContext != null && targetContext.HasExpectedLocalMod &&
                !string.Equals(NormalizeModId(sourceModId), NormalizeModId(targetContext.ExpectedLocalModId), StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Downloaded mod id '" + sourceModId + "' does not match selected installed mod id '" + targetContext.ExpectedLocalModId + "'.";
                return false;
            }

            if (Directory.Exists(fullTargetPath))
            {
                global::Manager.ModTypes.ModAboutInfo existingAbout;
                string existingId;
                string readError;
                if (TryReadRequiredAbout(fullTargetPath, out existingAbout, out existingId, out readError) &&
                    !string.Equals(existingId, NormalizeModId(sourceModId), StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Install target already contains a different mod id '" + existingAbout.id + "'.";
                    return false;
                }
            }

            string duplicatePath;
            if (TryFindDuplicateActiveMod(modsPath, sourceModId, fullTargetPath, out duplicatePath))
            {
                errorMessage = "Another installed mod already uses id '" + sourceModId + "': " + duplicatePath + ".";
                return false;
            }

            return true;
        }

        internal static bool IsReservedModFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return true;

            return string.Equals(folderName, "disabled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(folderName, "SMM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(folderName, "ModAPI", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("_smm_", StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizeModId(string modId)
        {
            return (modId ?? string.Empty).Trim().ToLowerInvariant();
        }

        internal static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static bool IsDirectChildOf(string fullParentWithSlash, string fullChildPath)
        {
            if (string.IsNullOrEmpty(fullParentWithSlash) || string.IsNullOrEmpty(fullChildPath))
                return false;

            if (!fullChildPath.StartsWith(fullParentWithSlash, StringComparison.OrdinalIgnoreCase))
                return false;

            string relative = fullChildPath.Substring(fullParentWithSlash.Length)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.Length > 0
                && relative.IndexOf(Path.DirectorySeparatorChar) < 0
                && relative.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        private static bool TryFindDuplicateActiveMod(string modsPath, string sourceModId, string fullTargetPath, out string duplicatePath)
        {
            duplicatePath = null;
            string normalizedSourceId = NormalizeModId(sourceModId);
            if (string.IsNullOrEmpty(normalizedSourceId))
                return false;

            string fullTarget = Path.GetFullPath(fullTargetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(modsPath);
            }
            catch
            {
                return false;
            }

            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                string folderName = Path.GetFileName(directory);
                if (IsReservedModFolderName(folderName))
                    continue;

                string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(fullDirectory, fullTarget, StringComparison.OrdinalIgnoreCase))
                    continue;

                global::Manager.ModTypes.ModAboutInfo about;
                string normalizedId;
                string readError;
                if (TryReadRequiredAbout(directory, out about, out normalizedId, out readError) &&
                    string.Equals(normalizedId, normalizedSourceId, StringComparison.OrdinalIgnoreCase))
                {
                    duplicatePath = directory;
                    return true;
                }
            }

            return false;
        }
    }
}
