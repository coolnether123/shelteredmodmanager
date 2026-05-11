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
            tests.Add(new TestCase("Architecture_GameplayMustNotReadNetworkSessionSessionIdOutsideStartupBridges", NetworkSessionSessionIdReadsStayApproved));
            tests.Add(new TestCase("Architecture_WorldTickAuthorityMustNotUseDateTimeUtcNow", WorldTickAuthorityMustNotUseDateTimeUtcNow));
            tests.Add(new TestCase("Architecture_DateTimeUseInNetworkingMustStayDiagnosticsOnly", GameplayDateTimeUseStaysAllowlisted));
            tests.Add(new TestCase("Architecture_HostClockSamplesMustNotBecomePeriodicBroadcastLoop", HostClockSamplesMustNotBecomePeriodicBroadcastLoop));
            tests.Add(new TestCase("Architecture_SharedWorldSystemsMustNotReadUnityDeltaTimeOutsideRuntimeBridge", SharedWorldDeterministicSystemsMustNotReadUnityDeltaTimeOutsideRuntimeBridge));
            tests.Add(new TestCase("Architecture_FastSlowPolicyMustNotMutateSharedWorldTick", FastSlowPolicyMustNotMutateSharedWorldTick));
            tests.Add(new TestCase("Architecture_GameplayRandomUseRequiresDeterministicStreams", GameplayRandomUseRequiresDeterministicStreams));
            tests.Add(new TestCase("Architecture_SteamSensitiveSceneManagerUseStaysInRuntimeCompat", SteamSensitiveSceneManagerUseStaysInRuntimeCompat));
            tests.Add(new TestCase("Architecture_UnityRandomInitStateStaysInRuntimeCompat", UnityRandomInitStateStaysInRuntimeCompat));
            tests.Add(new TestCase("Architecture_NetworkingCatchBlocksMustBeLoggedOrDocumented", NetworkingHasNoUnsafeSilentCatches));
            tests.Add(new TestCase("Architecture_SetupDtosPopulateCoordinatorContext", SetupDtosPopulateCoordinatorContext));
            tests.Add(new TestCase("Architecture_EventRegistriesKeepIdempotencyIndexes", EventRegistriesKeepIdempotencyIndexes));
            tests.Add(new TestCase("Architecture_RawShelterSaveSyncStaysRemoved", RawShelterSaveSyncStaysRemoved));
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
            // Startup connection bridge only. Sheltered gameplay identity must come from the coordinator context.
            allowed["ShelteredAPI/Networking/MultiplayerConnectionTestService.cs"] = 1;
            // Temporary setup wire bridge: resolves the first coordinator context before gameplay systems are active.
            allowed["ShelteredAPI/Networking/ShelteredMultiplayerSetupService.cs"] = 2;

            List<string> findings = FindCountedFindings(
                FindDirectNetworkSessionSessionIdReads(Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking"))),
                allowed);

            AssertNoFindings(
                findings,
                "Sheltered gameplay code must read identity from ShelteredMultiplayerSessionCoordinator/ShelteredMultiplayerSessionContext. Do not add direct NetworkSession.SessionId reads outside approved startup integration points.");
        }

        private static void WorldTickAuthorityMustNotUseDateTimeUtcNow()
        {
            string repoRoot = FindRepoRoot();
            Regex pattern = new Regex(@"\bDateTime\.(?:UtcNow|Now)\b");
            List<string> findings = new List<string>();

            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "AdvanceFixedSteps", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "AdvanceRuntimeBridgeDelta", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "AdvanceFixedDelta", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "ApplyRemoteSample", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "ApplyRemoteSampleDetailed", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "TryApplyAuthoritativeEventDetailed", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "Advance", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs", "Reset", pattern, findings);

            AssertNoFindings(
                findings,
                "WorldTick authority must be derived from session context, accepted events, and deterministic fixed-tick inputs. DateTime is allowed only for diagnostic timestamps and sample-age reporting.");
        }

        private static void GameplayDateTimeUseStaysAllowlisted()
        {
            Dictionary<string, int> allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Connection panel status timestamps are display-only.
            allowed["ShelteredAPI/Networking/MultiplayerConnectionTestService.cs"] = 1;
            // Diagnostics age formatting is display-only.
            allowed["ShelteredAPI/Networking/MultiplayerDiagnosticsFormatter.cs"] = 1;
            // Multiplayer timeline timestamps are diagnostic metadata only.
            allowed["ShelteredAPI/Networking/Diagnostics/ShelteredMultiplayerTimeline.cs"] = 1;
            // Persistence timestamps describe snapshot metadata, not simulation time.
            allowed["ShelteredAPI/Networking/Persistence/ShelteredMultiplayerWorldSnapshot.cs"] = 1;
            // Temporary Dev-1.4 bridge: clock samples carry diagnostic UTC metadata only, never tick authority.
            allowed["ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs"] = 4;
            // Temporary Dev-1.4 bridge: missing sample timestamps are normalized for diagnostics only.
            allowed["ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClockContracts.cs"] = 1;
            // Event CreatedUtc is journal metadata; WorldTick comes from the coordinator.
            allowed["ShelteredAPI/Networking/World/ShelteredWorldEventJournal.cs"] = 1;

            Regex pattern = new Regex(@"\bDateTime\.(?:UtcNow|Now)\b");
            List<string> findings = FindCountedFindings(
                FindSourceOccurrences(Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking")), pattern),
                allowed);

            AssertNoFindings(
                findings,
                "DateTime is allowed for diagnostics/logging/sample timestamps only. Gameplay decisions should use coordinator world tick or deterministic event data.");
        }

        private static void HostClockSamplesMustNotBecomePeriodicBroadcastLoop()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();
            string sourceRoot = Path.Combine(repoRoot, Path.Combine("ShelteredAPI", "Networking"));

            AddDisallowedBroadcastLocalSampleCallFindings(sourceRoot, findings);
            AddDisallowedWorldClockSampleBroadcastFindings(sourceRoot, findings);

            AssertNoFindings(
                findings,
                "World.ClockSample is a rare correction/diagnostic event. Do not call BroadcastLocalSample or broadcast clock samples from runtime/update loops as the primary clock.");
        }

        private static void SharedWorldDeterministicSystemsMustNotReadUnityDeltaTimeOutsideRuntimeBridge()
        {
            Dictionary<string, int> allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Temporary Dev-1.4 bridge until the signed-off fixed-step scheduler replaces Unity frame delta.
            allowed["ShelteredAPI/Networking/ShelteredMultiplayerRuntimeDriver.cs"] = 1;

            Regex pattern = new Regex(@"\bTime\.deltaTime\b");
            List<string> findings = FindCountedFindings(
                FindSourceOccurrences(Path.Combine(FindRepoRoot(), Path.Combine("ShelteredAPI", "Networking")), pattern),
                allowed);

            AssertNoFindings(
                findings,
                "Shared-world deterministic systems must not read Unity Time.deltaTime directly. Route temporary runtime bridge deltas through the approved scheduler/correction service only.");
        }

        private static void FastSlowPolicyMustNotMutateSharedWorldTick()
        {
            string repoRoot = FindRepoRoot();
            Regex pattern = new Regex(@"\b(?:WorldTick|SetWorldTick|AdvanceFixedDelta|ShelteredMultiplayerWorldClock)\b");
            List<string> findings = new List<string>();

            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "SetLocalBunkerIntensityMode", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "TryHandleFastForward", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "TryHandleSlowDown", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "IsFastModeActive", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "IsSlowModeActive", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs", "ApplyTravelDistance", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePatches.cs", "StartFastForwardPrefix", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePatches.cs", "EndFastForwardPrefix", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePatches.cs", "StartSlowDownPrefix", pattern, findings);
            RequireMethodDoesNotContain(repoRoot, "ShelteredAPI/Networking/ShelteredMultiplayerTimePatches.cs", "EndSlowDownPrefix", pattern, findings);

            AssertNoFindings(
                findings,
                "Fast/slow policy is local bunker intensity only. It must not read, write, advance, or otherwise affect shared WorldTick.");
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

        private static void SteamSensitiveSceneManagerUseStaysInRuntimeCompat()
        {
            List<string> findings = FindShelteredApiDirectSceneManagerFindings();

            AssertNoFindings(
                findings,
                "ShelteredAPI runtime code must not directly call Steam-sensitive SceneManager APIs. Use ModAPI.Core.RuntimeCompat scene helpers so Unity 5.3 can fall back to legacy APIs.");
        }

        private static void UnityRandomInitStateStaysInRuntimeCompat()
        {
            string repoRoot = FindRepoRoot();
            Regex pattern = new Regex(@"\b(?:UnityEngine\.)?Random\s*\.\s*InitState\s*\(");
            List<Occurrence> occurrences = new List<Occurrence>();
            occurrences.AddRange(FindSourceOccurrences(Path.Combine(repoRoot, "ModAPI"), pattern));
            occurrences.AddRange(FindSourceOccurrences(Path.Combine(repoRoot, "ShelteredAPI"), pattern));

            List<string> findings = new List<string>();
            for (int i = 0; i < occurrences.Count; i++)
            {
                if (string.Equals(occurrences[i].Path, "ModAPI/Core/RuntimeCompat.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                findings.Add(occurrences[i].ToString());
            }

            AssertNoFindings(
                findings,
                "UnityEngine.Random.InitState is not available on all supported Sheltered Unity runtimes. Route Unity RNG seeding through ModAPI.Core.RuntimeCompat.TrySetUnityRandomSeed.");
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

        private static void RawShelterSaveSyncStaysRemoved()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();

            string legacyService = Path.Combine(repoRoot, Path.Combine(Path.Combine("ShelteredAPI", "Networking"), "ShelteredMultiplayerSaveSyncService.cs"));
            if (File.Exists(legacyService))
                findings.Add("ShelteredMultiplayerSaveSyncService.cs must stay removed. Shelters are local; multiplayer sync should target shared-world/map state.");

            string connectionServicePath = Path.Combine(repoRoot, Path.Combine(Path.Combine("ShelteredAPI", "Networking"), "MultiplayerConnectionTestService.cs"));
            string connectionService = File.ReadAllText(connectionServicePath);
            if (connectionService.IndexOf("SaveSync", StringComparison.OrdinalIgnoreCase) >= 0)
                findings.Add("MultiplayerConnectionTestService.cs must not compose or route raw save-sync messages.");

            string projectPath = Path.Combine(repoRoot, Path.Combine("ShelteredAPI", "ShelteredAPI.csproj"));
            string projectText = File.ReadAllText(projectPath);
            if (projectText.IndexOf("ShelteredMultiplayerSaveSyncService.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                findings.Add("ShelteredAPI.csproj must not compile raw shelter save-sync service code.");

            AssertNoFindings(
                findings,
                "Shelter saves are player-local. Future save-backed multiplayer data must be narrow shared-world/map state, not raw slot or vanilla save snapshots.");
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

        private static void RequireMethodDoesNotContain(
            string repoRoot,
            string relativePath,
            string methodName,
            Regex pattern,
            List<string> findings)
        {
            string path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string text = File.ReadAllText(path);
            int blockStartIndex;
            string methodBlock = ExtractMethodBlock(text, methodName, out blockStartIndex);
            if (methodBlock.Length == 0)
            {
                findings.Add(relativePath + " should contain method '" + methodName + "'.");
                return;
            }

            MatchCollection matches = pattern.Matches(methodBlock);
            for (int i = 0; i < matches.Count; i++)
            {
                int lineNumber = CountLinesBefore(text, blockStartIndex + matches[i].Index) + 1;
                findings.Add(relativePath + ":" + lineNumber + " contains " + matches[i].Value + " inside " + methodName);
            }
        }

        private static string ExtractMethodBlock(string text, string methodName, out int blockStartIndex)
        {
            blockStartIndex = -1;
            Regex methodPattern = new Regex(
                @"(?m)^\s*(?:public|private|internal|protected)\s+"
                + @"(?:(?:static|virtual|override|sealed|new|extern|async)\s+)*"
                + @"[A-Za-z_][A-Za-z0-9_<>,\[\]\.?]*\s+"
                + Regex.Escape(methodName)
                + @"\s*\(");
            Match match = methodPattern.Match(text);
            while (match.Success)
            {
                int openBrace = text.IndexOf('{', match.Index);
                if (openBrace < 0)
                    return string.Empty;

                int depth = 0;
                for (int i = openBrace; i < text.Length; i++)
                {
                    if (text[i] == '{')
                        depth++;
                    else if (text[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            blockStartIndex = openBrace;
                            return text.Substring(openBrace, i - openBrace + 1);
                        }
                    }
                }

                match = match.NextMatch();
            }

            return string.Empty;
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

        private static List<string> FindShelteredApiDirectSceneManagerFindings()
        {
            string repoRoot = FindRepoRoot();
            List<string> findings = new List<string>();
            Regex getActiveScene = new Regex(@"\bSceneManager\s*\.\s*GetActiveScene\s*\(");
            Regex loadScene = new Regex(@"\bSceneManager\s*\.\s*LoadScene\s*\(");
            Regex sceneEventSubscription = new Regex(@"\bSceneManager\s*\.\s*scene(?:Loaded|Unloaded)\s*(?:\+=|-=)");

            string[] files = Directory.GetFiles(Path.Combine(repoRoot, "ShelteredAPI"), "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string relativePath = ToRepoRelativePath(file);
                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    AddSceneManagerFinding(getActiveScene, relativePath, lineIndex + 1, line, "GetActiveScene", findings);
                    AddSceneManagerFinding(loadScene, relativePath, lineIndex + 1, line, "LoadScene", findings);
                    AddSceneManagerFinding(sceneEventSubscription, relativePath, lineIndex + 1, line, "sceneLoaded/sceneUnloaded subscription", findings);
                }
            }

            return findings;
        }

        private static void AddSceneManagerFinding(
            Regex pattern,
            string relativePath,
            int lineNumber,
            string line,
            string apiName,
            List<string> findings)
        {
            Match match = pattern.Match(line);
            if (match.Success)
                findings.Add(relativePath + ":" + lineNumber + " directly uses SceneManager." + apiName);
        }

        private static void AddDisallowedBroadcastLocalSampleCallFindings(string sourceRoot, List<string> findings)
        {
            Regex pattern = new Regex(@"\bBroadcastLocalSample\s*\(");
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string relativePath = ToRepoRelativePath(file);
                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (!pattern.IsMatch(lines[lineIndex]))
                        continue;

                    if (relativePath == "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs"
                        && lines[lineIndex].IndexOf("bool BroadcastLocalSample", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    findings.Add(relativePath + ":" + (lineIndex + 1) + " calls BroadcastLocalSample");
                }
            }
        }

        private static void AddDisallowedWorldClockSampleBroadcastFindings(string sourceRoot, List<string> findings)
        {
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsGeneratedPath(file))
                    continue;

                string relativePath = ToRepoRelativePath(file);
                if (relativePath == "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs")
                    continue;

                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lines[lineIndex].IndexOf("BroadcastAuthoritative", StringComparison.Ordinal) < 0)
                        continue;

                    if (NearbyLinesContain(lines, lineIndex + 1, "ShelteredWorldClockSampleCodec")
                        || NearbyLinesContain(lines, lineIndex + 1, "WorldClockSample"))
                    {
                        findings.Add(relativePath + ":" + (lineIndex + 1) + " broadcasts a World.ClockSample");
                    }
                }
            }
        }

        private static bool NearbyLinesContain(string[] lines, int lineNumber, string value)
        {
            int start = Math.Max(1, lineNumber - 4);
            int end = Math.Min(lines.Length, lineNumber + 4);
            for (int i = start; i <= end; i++)
            {
                if (lines[i - 1].IndexOf(value, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static void AddFileOccurrenceFindings(string repoRoot, string relativePath, Regex pattern, List<string> findings)
        {
            string path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                Match match = pattern.Match(lines[lineIndex]);
                if (match.Success)
                    findings.Add(relativePath + ":" + (lineIndex + 1) + " contains " + match.Value);
            }
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
