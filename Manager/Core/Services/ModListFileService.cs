using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Manager.Core.Models;
using ShelteredModManager.ContentPacks;

namespace Manager.Core.Services
{
    /// <summary>
    /// A saved set of active mods in load order. Profiles and lists shared
    /// with other players use the same file (<see cref="ModListFileService.FileExtension"/>).
    /// </summary>
    public sealed class ModListFile
    {
        public string format { get; set; }
        public int version { get; set; }
        public string name { get; set; }
        public string savedUtc { get; set; }
        public ModListEntry[] mods { get; set; }
    }

    public sealed class ModListEntry
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string nexusGameDomain { get; set; }
        public int nexusModId { get; set; }
    }

    /// <summary>
    /// Reads and writes mod lists, and keeps profiles in the mods folder under
    /// "_smm_profiles" (a reserved name, so it is never mistaken for a mod).
    /// Imported files are untrusted: sizes, counts and every field are checked.
    /// </summary>
    public sealed class ModListFileService
    {
        public const string FileExtension = ".smmlist";
        public const string FileFilter = "Sheltered mod list (*.smmlist)|*.smmlist";
        private const string Format = "smm-modlist";
        private const string ProfilesFolderName = "_smm_profiles";
        private const string ActiveProfileFileName = "active.txt";
        private const long MaximumFileBytes = 1024 * 1024;
        private const int MaximumEntries = 2000;
        private const int MaximumProfileNameLength = 40;

        private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_.-]{1,200}$", RegexOptions.CultureInvariant);
        private static readonly Regex DomainPattern = new Regex("^[a-z0-9-]{1,64}$", RegexOptions.CultureInvariant);
        private static readonly string[] ReservedDeviceNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public ModListFile Build(string name, IEnumerable<ModItem> activeMods)
        {
            List<ModListEntry> entries = new List<ModListEntry>();
            foreach (ModItem mod in activeMods)
            {
                if (mod == null)
                    continue;
                ModListEntry entry = new ModListEntry();
                entry.id = mod.Id;
                entry.name = mod.DisplayName;
                entry.version = mod.Version;
                if (mod.HasNexusReference)
                {
                    entry.nexusGameDomain = mod.NexusGameDomain;
                    entry.nexusModId = mod.NexusModId;
                }
                entries.Add(entry);
            }
            ModListFile list = new ModListFile();
            list.format = Format;
            list.version = 1;
            list.name = name ?? string.Empty;
            list.savedUtc = DateTime.UtcNow.ToString("o");
            list.mods = entries.ToArray();
            return list;
        }

        public void Save(string path, ModListFile list)
        {
            // Stable member order, so saving the same list always writes the same file.
            string json = StableJson.Serialize(new JavaScriptSerializer(), list);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }
            try
            {
                // Swap in one step so an interrupted save never leaves no file.
                File.Replace(temporary, path, null);
            }
            catch (IOException)
            {
                File.Delete(path);
                File.Move(temporary, path);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(path);
                File.Move(temporary, path);
            }
        }

        public bool TryLoad(string path, out ModListFile list, out string error)
        {
            list = null;
            error = null;
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists)
                {
                    error = "The file no longer exists.";
                    return false;
                }
                if (info.Length > MaximumFileBytes)
                {
                    error = "This file is too large to be a mod list.";
                    return false;
                }
                ModListFile loaded = new JavaScriptSerializer().Deserialize<ModListFile>(File.ReadAllText(path));
                if (loaded == null || !string.Equals(loaded.format, Format, StringComparison.Ordinal) || loaded.version != 1 || loaded.mods == null)
                {
                    error = "This isn't a Sheltered Mod Manager mod list.";
                    return false;
                }
                if (loaded.mods.Length > MaximumEntries)
                {
                    error = "This mod list has too many entries.";
                    return false;
                }

                List<ModListEntry> entries = new List<ModListEntry>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ModListEntry entry in loaded.mods)
                {
                    if (entry == null || entry.id == null || !IdPattern.IsMatch(entry.id.Trim()) || !seen.Add(entry.id.Trim()))
                        continue;
                    ModListEntry clean = new ModListEntry();
                    clean.id = entry.id.Trim().ToLowerInvariant();
                    clean.name = Limit(entry.name, 200);
                    clean.version = Limit(entry.version, 60);
                    string domain = (entry.nexusGameDomain ?? string.Empty).Trim().ToLowerInvariant();
                    if (entry.nexusModId > 0 && DomainPattern.IsMatch(domain))
                    {
                        clean.nexusGameDomain = domain;
                        clean.nexusModId = entry.nexusModId;
                    }
                    entries.Add(clean);
                }
                loaded.name = Limit(loaded.name, 80);
                loaded.mods = entries.ToArray();
                list = loaded;
                return true;
            }
            catch (Exception ex)
            {
                error = "The mod list could not be read: " + ex.Message;
                return false;
            }
        }

        public static string[] OrderOf(ModListFile list)
        {
            if (list == null || list.mods == null)
                return new string[0];
            string[] ids = new string[list.mods.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = list.mods[i].id;
            return ids;
        }

        // ------------------------------------------------------------ profiles

        public List<string> ListProfiles(string modsPath)
        {
            List<string> names = new List<string>();
            string folder = ProfilesFolder(modsPath);
            try
            {
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder, "*" + FileExtension))
                        names.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            // Ordinal so the order is the same on every PC, whatever its language.
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public bool TryLoadProfile(string modsPath, string name, out ModListFile list, out string error)
        {
            list = null;
            if (!IsValidProfileName(name, out error))
                return false;
            return TryLoad(ProfilePath(modsPath, name), out list, out error);
        }

        public void SaveProfile(string modsPath, string name, IEnumerable<ModItem> activeMods)
        {
            Save(ProfilePath(modsPath, name), Build(name, activeMods));
        }

        public void DeleteProfile(string modsPath, string name)
        {
            string path = ProfilePath(modsPath, name);
            bool wasActive = string.Equals(GetActiveProfile(modsPath), name, StringComparison.OrdinalIgnoreCase);
            if (File.Exists(path))
                File.Delete(path);
            if (wasActive)
                SetActiveProfile(modsPath, null);
        }

        public bool RenameProfile(string modsPath, string oldName, string newName, out string error)
        {
            if (!IsValidProfileName(oldName, out error) || !IsValidProfileName(newName, out error))
                return false;
            string target = ProfilePath(modsPath, newName);
            if (File.Exists(target) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                error = "A profile with that name already exists.";
                return false;
            }
            // Checked first: once the old file is gone the old name no longer resolves.
            bool wasActive = string.Equals(GetActiveProfile(modsPath), oldName, StringComparison.OrdinalIgnoreCase);
            ModListFile list;
            if (!TryLoadProfile(modsPath, oldName, out list, out error))
                return false;
            list.name = newName.Trim();
            string source = ProfilePath(modsPath, oldName);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                // A change of case only: step through a temporary name so the file takes the new casing.
                string temporary = source + ".renaming";
                File.Move(source, temporary);
                try
                {
                    Save(target, list);
                }
                catch
                {
                    File.Move(temporary, source);
                    throw;
                }
                File.Delete(temporary);
            }
            else
            {
                Save(target, list);
                if (File.Exists(source))
                    File.Delete(source);
            }
            if (wasActive)
                SetActiveProfile(modsPath, newName.Trim());
            return true;
        }

        public bool ProfileExists(string modsPath, string name)
        {
            string error;
            if (!IsValidProfileName(name, out error))
                return false;
            return File.Exists(ProfilePath(modsPath, name));
        }

        /// <summary>The profile that follows changes to the load order, or null.</summary>
        public string GetActiveProfile(string modsPath)
        {
            try
            {
                string path = Path.Combine(ProfilesFolder(modsPath), ActiveProfileFileName);
                if (!File.Exists(path))
                    return null;
                string name = File.ReadAllText(path).Trim();
                string ignored;
                return IsValidProfileName(name, out ignored) && ProfileExists(modsPath, name) ? name : null;
            }
            catch
            {
                return null;
            }
        }

        public void SetActiveProfile(string modsPath, string name)
        {
            string folder = ProfilesFolder(modsPath);
            string path = Path.Combine(folder, ActiveProfileFileName);
            if (string.IsNullOrEmpty(name))
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            string error;
            if (!IsValidProfileName(name, out error))
                throw new ArgumentException(error, "name");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(path, name.Trim(), new UTF8Encoding(false));
        }

        public static bool IsValidProfileName(string name, out string error)
        {
            error = null;
            string value = (name ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                error = "Give the profile a name.";
                return false;
            }
            if (value.Length > MaximumProfileNameLength)
            {
                error = "Keep the name under " + MaximumProfileNameLength + " characters.";
                return false;
            }
            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.StartsWith(".", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal))
            {
                error = "Profile names can't contain \\ / : * ? \" < > | or start or end with a dot.";
                return false;
            }
            // Windows keeps these names for devices, so no file can use them.
            string device = value.Split('.')[0].Trim().ToUpperInvariant();
            if (Array.IndexOf(ReservedDeviceNames, device) >= 0)
            {
                error = "“" + value + "” is reserved by Windows. Choose another name.";
                return false;
            }
            return true;
        }

        private static string ProfilesFolder(string modsPath)
        {
            return Path.Combine(modsPath ?? string.Empty, ProfilesFolderName);
        }

        private static string ProfilePath(string modsPath, string name)
        {
            string error;
            if (!IsValidProfileName(name, out error))
                throw new ArgumentException(error, "name");
            return Path.Combine(ProfilesFolder(modsPath), name.Trim() + FileExtension);
        }

        private static string Limit(string value, int length)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length > length ? text.Substring(0, length) : text;
        }
    }
}
