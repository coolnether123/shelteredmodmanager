using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ArchitectureGuardrailTests
    {
        private const string PublicDeclarationPattern =
            @"(?m)^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(class|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)";
        private const string NamespacePattern = @"(?m)^\s*namespace\s+([A-Za-z0-9_.]+)";
        private const string SilentCatchAllowToken = "GuardrailAllow: SilentCatch";

        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Architecture_PublicSurfaceMatchesBaseline", PublicSurfaceMatchesBaseline));
            tests.Add(new TestCase("Architecture_NetworkSessionSessionIdReadsStayApproved", NetworkSessionSessionIdReadsStayApproved));
            tests.Add(new TestCase("Architecture_GameplayDateTimeUseStaysAllowlisted", GameplayDateTimeUseStaysAllowlisted));
            tests.Add(new TestCase("Architecture_GameplayRandomUseRequiresDeterministicStreams", GameplayRandomUseRequiresDeterministicStreams));
            tests.Add(new TestCase("Architecture_NetworkingHasNoUnsafeSilentCatches", NetworkingHasNoUnsafeSilentCatches));
            tests.Add(new TestCase("Architecture_SetupDtosPopulateCoordinatorContext", SetupDtosPopulateCoordinatorContext));
            tests.Add(new TestCase("Architecture_EventRegistriesKeepIdempotencyIndexes", EventRegistriesKeepIdempotencyIndexes));
        }

        private static void PublicSurfaceMatchesBaseline()
        {
            string repoRoot = FindRepoRoot();
            Dictionary<string, string> baseline = ReadPublicSurfaceBaseline(
                Path.Combine(repoRoot, Path.Combine("documentation", "ShelteredAPI_PublicSurface_Baseline.tsv")));
            Dictionary<string, string> current = ReadCurrentPublicSurface(Path.Combine(repoRoot, "ShelteredAPI"));

            List<string> findings = new List<string>();
            foreach (KeyValuePair<string, string> entry in current)
            {
                if (!baseline.ContainsKey(entry.Key))
                    findings.Add(entry.Value);
            }

            AssertNoFindings(
                findings,
                "ShelteredAPI public surface changed. Make accidental APIs internal, or update the public-surface baseline with documentation.");
        }

        private static void NetworkSessionSessionIdReadsStayApproved()
        {
            Dictionary<string, int> allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            allowed["ShelteredAPI/Networking/MultiplayerConnectionTestService.cs"] = 1;
            allowed["ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs"] = 2;

            List<string> findings = FindCountedFindings(
                FindDirectNetworkSessionSessionIdReads(Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking"))),
                allowed);

            AssertNoFindings(
                findings,
                "Sheltered gameplay code must read identity from ShelteredMultiplayerSessionCoordinator/ShelteredMultiplayerSessionContext. Do not add direct NetworkSession.SessionId reads outside approved startup integration points.");
        }

        private static void GameplayDateTimeUseStaysAllowlisted()
        {
            Dictionary<string, int> allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            allowed["ShelteredAPI/Networking/MultiplayerConnectionTestService.cs"] = 1;
            allowed["ShelteredAPI/Networking/MultiplayerDiagnosticsFormatter.cs"] = 1;
            allowed["ShelteredAPI/Networking/Persistence/ShelteredMultiplayerWorldSnapshot.cs"] = 1;
            allowed["ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs"] = 3;
            allowed["ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs"] = 4;
            allowed["ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClockContracts.cs"] = 1;
            allowed["ShelteredAPI/Networking/World/ShelteredWorldEventJournal.cs"] = 1;

            Regex pattern = new Regex(@"\bDateTime\.(?:UtcNow|Now)\b");
            List<string> findings = FindCountedFindings(
                FindSourceOccurrences(Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking")), pattern),
                allowed);

            AssertNoFindings(
                findings,
                "DateTime is allowed for diagnostics/logging/sample timestamps only. Gameplay decisions should use coordinator world tick or deterministic event data.");
        }

        private static void GameplayRandomUseRequiresDeterministicStreams()
        {
            Regex pattern = new Regex(@"\bnew\s+Random\s*\(|\b(?:UnityEngine\.)?Random\.(?:Range|value)\b");
            List<Occurrence> findings = FindSourceOccurrences(
                Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking")),
                pattern);

            AssertNoFindings(
                FormatOccurrences(findings),
                "Multiplayer-sensitive random placement must use session/stable ids and named deterministic streams, not unseeded Random or UnityEngine.Random.Range/value.");
        }

        private static void NetworkingHasNoUnsafeSilentCatches()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();
            AddUnsafeSilentCatchFindings(Path.Combine(repoRoot, Path.Combine("ShelteredAPI", "Networking")), findings);

            AssertNoFindings(
                findings,
                "Empty catch blocks in ShelteredAPI networking paths must be explicit best-effort behavior. Add a nearby " + SilentCatchAllowToken + " comment with the reason, or log/handle the exception.");
        }

        private static void SetupDtosPopulateCoordinatorContext()
        {
            string repoRoot = FindRepoRoot();
            string setupPath = Path.Combine(repoRoot, Path.Combine(Path.Combine("ShelteredAPI", "Networking"), "ShelteredMultiplayerSetupService.cs"));
            string text = File.ReadAllText(setupPath);
            List<string> findings = new List<string>();

            if (text.IndexOf("ApplyReceivedSetup(", StringComparison.Ordinal) < 0)
                findings.Add("ShelteredMultiplayerSetupService.cs should publish received setup DTOs into ShelteredMultiplayerSessionCoordinator.ApplyReceivedSetup.");
            if (text.IndexOf("BeginSetupPreparation(", StringComparison.Ordinal) < 0)
                findings.Add("ShelteredMultiplayerSetupService.cs should prepare host setup through ShelteredMultiplayerSessionCoordinator.BeginSetupPreparation.");
            if (text.IndexOf("ShelteredMultiplayerSessionSeed.TryApply", StringComparison.Ordinal) >= 0)
                findings.Add("ShelteredMultiplayerSetupService.cs must not apply session seeds directly.");
            if (text.IndexOf("ShelteredBunkers.", StringComparison.Ordinal) >= 0)
                findings.Add("ShelteredMultiplayerSetupService.cs must not apply bunker gameplay directly; use coordinator lifecycle handlers.");

            AssertNoFindings(
                findings,
                "Setup wire DTOs may carry identity over the network, but they must populate the coordinator instead of becoming a long-lived identity/gameplay source.");
        }

        private static void EventRegistriesKeepIdempotencyIndexes()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();

            RequireFileContains(repoRoot, "ShelteredAPI/Networking/World/ShelteredWorldEventJournal.cs", "_recordsById", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/World/ShelteredWorldEventJournal.cs", "Contains(normalized.EventId)", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Encounters/ShelteredEncounterNegotiationStateRegistry.cs", "_appliedEventIds", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Trade/ShelteredMultiplayerTradeStateRegistry.cs", "_appliedEventIds", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Travel/ShelteredTravelStateRegistry.cs", "_appliedEventIds", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Locations/ShelteredLocationStateRegistry.cs", "_takenEventIds", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Raids/ShelteredRaidStateRegistry.cs", "_appliedEventIds", findings);
            RequireFileContains(repoRoot, "ShelteredAPI/Networking/Settlements/ShelteredSettlementStateRegistry.cs", "_appliedEventIds", findings);

            AssertNoFindings(
                findings,
                "Authoritative event journals and state registries must keep event/correlation id indexes so duplicate network delivery cannot apply dangerous mutations twice.");
        }

        private static Dictionary<string, string> ReadPublicSurfaceBaseline(string baselinePath)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(baselinePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#"))
                    continue;

                string[] parts = line.Split('\t');
                if (parts.Length < 5)
                    throw new InvalidOperationException("Invalid public-surface baseline line " + (i + 1) + ".");

                string key = parts[0] + "\t" + parts[1] + "\t" + parts[2];
                entries[key] = parts[3];
            }

            return entries;
        }

        private static Dictionary<string, string> ReadCurrentPublicSurface(string apiRoot)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
            Regex declaration = new Regex(PublicDeclarationPattern);
            Regex namespaceRegex = new Regex(NamespacePattern);
            string[] files = Directory.GetFiles(apiRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string text = File.ReadAllText(file);
                Match namespaceMatch = namespaceRegex.Match(text);
                string ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
                MatchCollection declarations = declaration.Matches(text);
                for (int j = 0; j < declarations.Count; j++)
                {
                    Match match = declarations[j];
                    string key = match.Groups[1].Value + "\t" + ns + "\t" + match.Groups[2].Value;
                    entries[key] = match.Groups[1].Value + "\t" + ns + "\t" + match.Groups[2].Value + "\t" + ToRepoRelativePath(file);
                }
            }

            return entries;
        }

        private static List<Occurrence> FindDirectNetworkSessionSessionIdReads(string sourceRoot)
        {
            Regex declarationPattern = new Regex(@"\bNetworkSession\s+([A-Za-z_][A-Za-z0-9_]*)");
            List<Occurrence> findings = new List<Occurrence>();
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string text = File.ReadAllText(file);
                Dictionary<string, bool> sessionNames = new Dictionary<string, bool>(StringComparer.Ordinal);
                MatchCollection declarations = declarationPattern.Matches(text);
                for (int j = 0; j < declarations.Count; j++)
                    sessionNames[declarations[j].Groups[1].Value] = true;

                if (sessionNames.Count == 0)
                    continue;

                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    foreach (string name in sessionNames.Keys)
                    {
                        if (Regex.IsMatch(lines[lineIndex], @"\b" + Regex.Escape(name) + @"\.SessionId\b"))
                            findings.Add(new Occurrence(ToRepoRelativePath(file), lineIndex + 1, name + ".SessionId"));
                    }
                }
            }

            return findings;
        }

        private static List<Occurrence> FindSourceOccurrences(string sourceRoot, Regex pattern)
        {
            List<Occurrence> findings = new List<Occurrence>();
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
                    if (match.Success)
                        findings.Add(new Occurrence(ToRepoRelativePath(file), lineIndex + 1, match.Value));
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

        private static List<string> FindCountedFindings(List<Occurrence> occurrences, Dictionary<string, int> allowed)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<string> findings = new List<string>();

            for (int i = 0; i < occurrences.Count; i++)
            {
                Occurrence occurrence = occurrences[i];
                int count;
                counts.TryGetValue(occurrence.Path, out count);
                count++;
                counts[occurrence.Path] = count;

                int allowedCount;
                if (!allowed.TryGetValue(occurrence.Path, out allowedCount) || count > allowedCount)
                    findings.Add(occurrence.ToString());
            }

            return findings;
        }

        private static List<string> FormatOccurrences(List<Occurrence> occurrences)
        {
            List<string> formatted = new List<string>();
            for (int i = 0; i < occurrences.Count; i++)
                formatted.Add(occurrences[i].ToString());
            return formatted;
        }

        private static void RequireFileContains(string repoRoot, string relativePath, string requiredText, List<string> findings)
        {
            string path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string text = File.ReadAllText(path);
            if (text.IndexOf(requiredText, StringComparison.Ordinal) < 0)
                findings.Add(relativePath + " should contain '" + requiredText + "'.");
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
            int limit = Math.Min(findings.Count, 12);
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
                    && Directory.Exists(Path.Combine(dir.FullName, "ShelteredAPI")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return string.Empty;
        }

        private static string ToRepoRelativePath(string path)
        {
            string repoRoot = FindRepoRoot();
            string relative = path;
            if (path.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                relative = path.Substring(repoRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private sealed class Occurrence
        {
            public readonly string Path;
            private readonly int _line;
            private readonly string _symbol;

            public Occurrence(string path, int line, string symbol)
            {
                Path = path;
                _line = line;
                _symbol = symbol ?? string.Empty;
            }

            public override string ToString()
            {
                return Path + ":" + _line + " contains " + _symbol;
            }
        }
    }
}
