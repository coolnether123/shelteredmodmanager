using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ModAPI.Core;
using ModAPI.Networking;

namespace ShelteredAPI.Networking.Compatibility
{
    internal sealed class ShelteredMultiplayerCompatibilityHasher
    {
        private const string NonGameplayTag = "non-gameplay-affecting";
        private const string UiOnlyTag = "ui-only";

        public ShelteredMultiplayerCompatibilityHash ComputeHash(ShelteredMultiplayerCompatibilityInput input)
        {
            ShelteredMultiplayerCompatibilityInput normalized = Normalize(input);
            string manifest = BuildManifest(normalized);
            byte[] bytes = Encoding.UTF8.GetBytes(manifest);
            using (SHA256Managed sha = new SHA256Managed())
            {
                return new ShelteredMultiplayerCompatibilityHash(ToHex(sha.ComputeHash(bytes)), manifest, normalized);
            }
        }

        public ShelteredMultiplayerCompatibilityInput CaptureCurrent()
        {
            ShelteredMultiplayerCompatibilityInput input = new ShelteredMultiplayerCompatibilityInput();
            input.SmmVersion = GetAssemblyVersion(typeof(ModRegistry).Assembly);
            input.ModApiVersion = RuntimeCompat.ModApiVersion;
            input.ShelteredApiVersion = GetAssemblyVersion(typeof(ShelteredMultiplayerCompatibilityHasher).Assembly);
            input.ModApiNetworkingVersion = GetAssemblyVersion(typeof(NetworkDefaults).Assembly);
            input.ProtocolVersion = NetworkDefaults.ProtocolVersion.ToString();

            List<ModEntry> mods = ModRegistry.GetLoadedMods();
            for (int i = 0; i < mods.Count; i++)
            {
                ModEntry mod = mods[i];
                if (mod == null)
                    continue;

                input.EnabledMods.Add(new ShelteredMultiplayerCompatibilityMod
                {
                    ModId = mod.Id ?? string.Empty,
                    Version = FirstNonEmpty(mod.Version, mod.About != null ? mod.About.version : string.Empty),
                    RequiredModApiVersion = mod.About != null
                        ? FirstNonEmpty(mod.About.requiredModApiVersion, mod.About.modApiVersion)
                        : string.Empty,
                    RequiredShelteredApiVersion = mod.About != null
                        ? FirstNonEmpty(mod.About.requiredShelteredApiVersion, mod.About.shelteredApiVersion)
                        : string.Empty,
                    NonGameplayAffecting = IsExplicitlyNonGameplayAffecting(mod.About)
                });
            }

            return input;
        }

        public string CaptureCurrentHash()
        {
            return ComputeHash(CaptureCurrent()).Hash;
        }

        private static ShelteredMultiplayerCompatibilityInput Normalize(ShelteredMultiplayerCompatibilityInput input)
        {
            if (input == null)
                input = new ShelteredMultiplayerCompatibilityInput();

            ShelteredMultiplayerCompatibilityInput normalized = new ShelteredMultiplayerCompatibilityInput();
            normalized.SmmVersion = NormalizeText(input.SmmVersion);
            normalized.ModApiVersion = NormalizeText(input.ModApiVersion);
            normalized.ShelteredApiVersion = NormalizeText(input.ShelteredApiVersion);
            normalized.ModApiNetworkingVersion = NormalizeText(input.ModApiNetworkingVersion);
            normalized.ProtocolVersion = NormalizeText(input.ProtocolVersion);
            normalized.ScenarioId = NormalizeText(input.ScenarioId);
            normalized.ScenarioStorageId = NormalizeText(input.ScenarioStorageId);
            normalized.EnabledMods = NormalizeMods(input.EnabledMods);
            normalized.CustomContentIds = NormalizeStrings(input.CustomContentIds);
            normalized.CustomRecipeIds = NormalizeStrings(input.CustomRecipeIds);
            return normalized;
        }

        private static List<ShelteredMultiplayerCompatibilityMod> NormalizeMods(
            List<ShelteredMultiplayerCompatibilityMod> mods)
        {
            List<ShelteredMultiplayerCompatibilityMod> normalized = new List<ShelteredMultiplayerCompatibilityMod>();
            if (mods != null)
            {
                for (int i = 0; i < mods.Count; i++)
                {
                    ShelteredMultiplayerCompatibilityMod mod = mods[i];
                    if (mod == null || NormalizeText(mod.ModId).Length == 0)
                        continue;
                    if (mod.NonGameplayAffecting)
                        continue;

                    normalized.Add(new ShelteredMultiplayerCompatibilityMod
                    {
                        ModId = NormalizeText(mod.ModId),
                        Version = NormalizeText(mod.Version),
                        RequiredModApiVersion = NormalizeText(mod.RequiredModApiVersion),
                        RequiredShelteredApiVersion = NormalizeText(mod.RequiredShelteredApiVersion),
                        NonGameplayAffecting = false
                    });
                }
            }

            normalized.Sort(CompareMods);
            return normalized;
        }

        private static List<string> NormalizeStrings(List<string> values)
        {
            List<string> normalized = new List<string>();
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    string value = NormalizeText(values[i]);
                    if (value.Length > 0 && !ContainsOrdinalIgnoreCase(normalized, value))
                        normalized.Add(value);
                }
            }

            normalized.Sort(StringComparer.OrdinalIgnoreCase);
            return normalized;
        }

        private static string BuildManifest(ShelteredMultiplayerCompatibilityInput input)
        {
            StringBuilder builder = new StringBuilder();
            AppendLine(builder, "smm", input.SmmVersion);
            AppendLine(builder, "modapi", input.ModApiVersion);
            AppendLine(builder, "shelteredapi", input.ShelteredApiVersion);
            AppendLine(builder, "networking", input.ModApiNetworkingVersion);
            AppendLine(builder, "protocol", input.ProtocolVersion);
            AppendLine(builder, "scenario", input.ScenarioId);
            AppendLine(builder, "scenarioStorage", input.ScenarioStorageId);

            for (int i = 0; i < input.EnabledMods.Count; i++)
            {
                ShelteredMultiplayerCompatibilityMod mod = input.EnabledMods[i];
                AppendLine(builder, "mod",
                    mod.ModId + "|" + mod.Version + "|" + mod.RequiredModApiVersion + "|" + mod.RequiredShelteredApiVersion);
            }

            for (int i = 0; i < input.CustomContentIds.Count; i++)
                AppendLine(builder, "content", input.CustomContentIds[i]);
            for (int i = 0; i < input.CustomRecipeIds.Count; i++)
                AppendLine(builder, "recipe", input.CustomRecipeIds[i]);

            return builder.ToString();
        }

        private static void AppendLine(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').Append(value ?? string.Empty).Append('\n');
        }

        private static bool IsExplicitlyNonGameplayAffecting(ModAbout about)
        {
            if (about == null || about.tags == null)
                return false;

            for (int i = 0; i < about.tags.Length; i++)
            {
                string tag = NormalizeText(about.tags[i]);
                if (string.Equals(tag, NonGameplayTag, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, UiOnlyTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareMods(
            ShelteredMultiplayerCompatibilityMod left,
            ShelteredMultiplayerCompatibilityMod right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return string.Compare(left.ModId, right.ModId, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes != null ? bytes.Length * 2 : 0);
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrEmpty(first) ? first : (second ?? string.Empty);
        }

        private static string GetAssemblyVersion(Assembly assembly)
        {
            if (assembly == null || assembly.GetName() == null || assembly.GetName().Version == null)
                return string.Empty;

            return assembly.GetName().Version.ToString();
        }
    }
}
