using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ModAPI.Networking.Tests
{
    internal static class ArchitectureBoundaryTests
    {
        private static readonly string[] ForbiddenProjectReferences =
        {
            "0Harmony",
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass",
            "Harmony",
            "HarmonyLib",
            "Manager",
            "ShelteredAPI",
            "UnityEngine",
            "UnityEngine.CoreModule",
            "UnityEngine.UI"
        };

        private static readonly string[] ForbiddenSourceSymbols =
        {
            "Assembly-CSharp",
            "Assembly_CSharp",
            "HarmonyLib",
            "ShelteredAPI",
            "UnityEngine"
        };

        private static readonly string[] ShelteredGameplayTerms =
        {
            "Bunker",
            "Bunkers",
            "EncounterCharacter",
            "EncounterManager",
            "Expedition",
            "ExpeditionMap",
            "ExplorationParty",
            "Faction",
            "FamilyManager",
            "FamilyMember",
            "GameTime",
            "InventoryManager",
            "ItemManager",
            "Loot",
            "MapRegion",
            "NpcVisitor",
            "Raid",
            "Raids",
            "SaveData",
            "SaveEntry",
            "SaveManager",
            "Settlement",
            "Settlements",
            "ShelterDefense",
            "TradingPanel",
            "WeatherManager"
        };

        private const string SilentCatchAllowToken = "GuardrailAllow: SilentCatch";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Architecture_ModApiNetworkingProjectHasNoHostReferences", ProjectHasNoHostReferences));
            tests.Add(new TestCase("Architecture_ModApiNetworkingSourceHasNoHostReferences", SourceHasNoHostReferences));
            tests.Add(new TestCase("Architecture_ModApiNetworkingSourceHasNoShelteredGameplayTerms", SourceHasNoShelteredGameplayTerms));
            tests.Add(new TestCase("Architecture_CoreAndNetworkingHaveNoUnsafeSilentCatches", CoreAndNetworkingHaveNoUnsafeSilentCatches));
            tests.Add(new TestCase("Architecture_CoreUnityRuntimeProbesUseApprovedHelpers", CoreUnityRuntimeProbesUseApprovedHelpers));
        }

        private static void ProjectHasNoHostReferences()
        {
            string repoRoot = FindRepoRoot();
            string projectPath = Path.Combine(repoRoot, Path.Combine("ModAPI.Networking", "ModAPI.Networking.csproj"));
            string text = File.ReadAllText(projectPath);

            List<string> findings = new List<string>();
            for (int i = 0; i < ForbiddenProjectReferences.Length; i++)
            {
                string forbidden = ForbiddenProjectReferences[i];
                string referencePattern = "<Reference\\s+Include=\"[^\"]*\\b" + Regex.Escape(forbidden) + "\\b";
                string projectReferencePattern = "<ProjectReference\\s+Include=\"[^\"]*" + Regex.Escape(forbidden) + "\\.csproj";
                if (Regex.IsMatch(text, referencePattern) || Regex.IsMatch(text, projectReferencePattern, RegexOptions.IgnoreCase))
                    findings.Add("ModAPI.Networking.csproj references " + forbidden);
            }

            AssertNoFindings(
                findings,
                "ModAPI.Networking must stay host-neutral. Move Unity/Harmony/Sheltered references to ShelteredAPI or another host layer.");
        }

        private static void SourceHasNoHostReferences()
        {
            string repoRoot = FindRepoRoot();
            string sourceRoot = Path.Combine(repoRoot, "ModAPI.Networking");
            Regex pattern = new Regex(BuildWordPattern(ForbiddenSourceSymbols));

            AssertNoFindings(
                FindSourceMatches(sourceRoot, pattern),
                "ModAPI.Networking source must not reference UnityEngine, HarmonyLib, ShelteredAPI, or Assembly-CSharp.");
        }

        private static void SourceHasNoShelteredGameplayTerms()
        {
            string repoRoot = FindRepoRoot();
            string sourceRoot = Path.Combine(repoRoot, "ModAPI.Networking");
            Regex pattern = new Regex(BuildWordPattern(ShelteredGameplayTerms));

            AssertNoFindings(
                FindSourceMatches(sourceRoot, pattern),
                "ModAPI.Networking is the neutral transport/session layer. Keep Sheltered gameplay vocabulary in ShelteredAPI.");
        }

        private static void CoreAndNetworkingHaveNoUnsafeSilentCatches()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();
            AddUnsafeSilentCatchFindings(Path.Combine(repoRoot, Path.Combine("ModAPI", "Core")), findings);
            AddUnsafeSilentCatchFindings(Path.Combine(repoRoot, "ModAPI.Networking"), findings);

            AssertNoFindings(
                findings,
                "Empty catch blocks in core/networking paths must be explicit best-effort behavior. Add a nearby " + SilentCatchAllowToken + " comment with the reason, or log/handle the exception.");
        }

        private static void CoreUnityRuntimeProbesUseApprovedHelpers()
        {
            string repoRoot = FindRepoRoot();
            string sourceRoot = Path.Combine(repoRoot, Path.Combine("ModAPI", "Core"));
            Regex pattern = new Regex(@"\bApplication\.(dataPath|platform|unityVersion)\b");
            List<string> findings = new List<string>();
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string relative = ToRepoRelativePath(files[i]).Replace('\\', '/');
                if (relative == "ModAPI/Core/RuntimeCompat.cs" || relative == "ModAPI/Core/RuntimeEnvironmentInfo.cs")
                    continue;

                string[] lines = File.ReadAllLines(files[i]);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Match match = pattern.Match(lines[lineIndex]);
                    if (match.Success)
                        findings.Add(relative + ":" + (lineIndex + 1) + " contains " + match.Value);
                }
            }

            AssertNoFindings(
                findings,
                "Core startup/logging code should use RuntimeCompat/RuntimeEnvironmentInfo for Unity runtime probes.");
        }

        private static List<string> FindSourceMatches(string sourceRoot, Regex pattern)
        {
            List<string> findings = new List<string>();
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Match match = pattern.Match(lines[lineIndex]);
                    if (!match.Success)
                        continue;

                    findings.Add(ToRepoRelativePath(file) + ":" + (lineIndex + 1) + " contains " + match.Value);
                }
            }

            return findings;
        }

        private static void AddUnsafeSilentCatchFindings(string sourceRoot, List<string> findings)
        {
            Regex pattern = new Regex(@"catch\s*(?:\([^)]*\))?\s*\{\s*\}", RegexOptions.Singleline);
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string text = File.ReadAllText(file);
                string[] lines = File.ReadAllLines(file);
                MatchCollection matches = pattern.Matches(text);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    int lineNumber = CountLinesBefore(text, matches[matchIndex].Index) + 1;
                    if (!HasAllowComment(lines, lineNumber))
                        findings.Add(ToRepoRelativePath(file) + ":" + lineNumber + " has an undocumented empty catch");
                }
            }
        }

        private static int CountLinesBefore(string text, int index)
        {
            int count = 0;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    count++;
            }
            return count;
        }

        private static bool HasAllowComment(string[] lines, int lineNumber)
        {
            int start = Math.Max(1, lineNumber - 4);
            int end = Math.Min(lines.Length, lineNumber + 4);
            for (int i = start; i <= end; i++)
            {
                if (lines[i - 1].IndexOf(SilentCatchAllowToken, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static string BuildWordPattern(string[] values)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("\\b(");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    builder.Append("|");
                builder.Append(Regex.Escape(values[i]));
            }
            builder.Append(")\\b");
            return builder.ToString();
        }

        private static bool IsGeneratedPath(string path)
        {
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AssertNoFindings(List<string> findings, string message)
        {
            if (findings.Count == 0)
                return;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(message);
            builder.AppendLine("Findings:");
            int limit = Math.Min(findings.Count, 10);
            for (int i = 0; i < limit; i++)
                builder.AppendLine(findings[i]);

            if (findings.Count > limit)
                builder.AppendLine("... " + (findings.Count - limit) + " more");

            throw new InvalidOperationException(builder.ToString());
        }

        private static string FindRepoRoot()
        {
            string fromCurrentDirectory = FindRepoRootFrom(Directory.GetCurrentDirectory());
            if (fromCurrentDirectory.Length > 0)
                return fromCurrentDirectory;

            string fromBaseDirectory = FindRepoRootFrom(AppDomain.CurrentDomain.BaseDirectory);
            if (fromBaseDirectory.Length > 0)
                return fromBaseDirectory;

            throw new InvalidOperationException("Could not locate repo root containing ShelteredModManager.sln.");
        }

        private static string FindRepoRootFrom(string start)
        {
            if (string.IsNullOrEmpty(start))
                return string.Empty;

            DirectoryInfo dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ShelteredModManager.sln"))
                    && Directory.Exists(Path.Combine(dir.FullName, "ModAPI.Networking")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return string.Empty;
        }

        private static string ToRepoRelativePath(string path)
        {
            string repoRoot = FindRepoRoot();
            if (path.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                return path.Substring(repoRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return path;
        }
    }
}
