using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioPackageEntry
    {
        public string RelativePath { get; set; }
        public string SourcePath { get; set; }
        public byte[] Content { get; set; }
        public long Size { get; set; }
    }

    internal sealed class ScenarioPackagePlan
    {
        public ScenarioPackagePlan()
        {
            Entries = new List<ScenarioPackageEntry>();
            Problems = new List<string>();
            RequiredMods = new List<string>();
            DeclaredMods = new List<string>();
        }

        public string PackageRoot { get; set; }
        public List<ScenarioPackageEntry> Entries { get; private set; }
        public List<string> Problems { get; private set; }
        public List<string> RequiredMods { get; private set; }
        public List<string> DeclaredMods { get; private set; }
        public long TotalSize { get; set; }

        public void Write()
        {
            if (string.IsNullOrEmpty(PackageRoot))
                throw new InvalidOperationException("Package root is required.");

            if (!Directory.Exists(PackageRoot))
                Directory.CreateDirectory(PackageRoot);
            RemoveOrphans(PackageRoot, Entries);

            for (int i = 0; i < Entries.Count; i++)
            {
                ScenarioPackageEntry entry = Entries[i];
                string destination = ResolveContainedPath(PackageRoot, entry.RelativePath);
                if (string.IsNullOrEmpty(destination))
                    throw new InvalidOperationException("Package entry escapes its root: " + entry.RelativePath);
                string directory = Path.GetDirectoryName(destination);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                if (entry.Content != null)
                    File.WriteAllBytes(destination, entry.Content);
                else
                    File.Copy(entry.SourcePath, destination, true);
            }
        }

        internal static string ResolveContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
                return null;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        private static void RemoveOrphans(string root, List<ScenarioPackageEntry> entries)
        {
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                bool expected = false;
                for (int e = 0; e < entries.Count; e++)
                {
                    if (string.Equals(Normalize(relative), Normalize(entries[e].RelativePath), StringComparison.OrdinalIgnoreCase))
                    {
                        expected = true;
                        break;
                    }
                }
                if (!expected)
                    File.Delete(files[i]);
            }
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }

    internal sealed class ScenarioPackagePlanner
    {
        internal const string ReadmeFileName = "README.txt";
        internal const string ManifestFileName = "scenario-package.xml";

        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly ScenarioAuthorTestChecklistService _testChecklistService;

        public ScenarioPackagePlanner(IScenarioDefinitionSerializer serializer)
            : this(serializer, new ScenarioAuthorTestChecklistService())
        {
        }

        public ScenarioPackagePlanner(
            IScenarioDefinitionSerializer serializer,
            ScenarioAuthorTestChecklistService testChecklistService)
        {
            _serializer = serializer;
            _testChecklistService = testChecklistService ?? new ScenarioAuthorTestChecklistService();
        }

        public ScenarioPackagePlan Build(
            ScenarioDefinition definition,
            string draftScenarioPath,
            string packageRoot,
            bool includeReadme,
            ScenarioValidationResult validation)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            ScenarioPackagePlan plan = new ScenarioPackagePlan { PackageRoot = packageRoot };
            AddGenerated(plan, ScenarioDefinitionSerializer.DefaultFileName, Serialize(definition));
            AddAssets(plan, definition, draftScenarioPath);
            AddDependencies(plan, definition);

            string[] limitations = GetUnsupportedWarnings(validation);
            if (includeReadme)
                AddGenerated(plan, ReadmeFileName, Encoding.UTF8.GetBytes(BuildReadme(definition, plan.RequiredMods, limitations)));
            AddGenerated(plan, ManifestFileName, Encoding.UTF8.GetBytes(BuildManifest(definition, plan.RequiredMods, limitations)));
            FindOrphans(plan);
            return plan;
        }

        private byte[] Serialize(ScenarioDefinition definition)
        {
            string temp = Path.Combine(Path.GetTempPath(), "sheltered-package-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                _serializer.Save(definition, temp);
                return File.ReadAllBytes(temp);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        private static void AddAssets(ScenarioPackagePlan plan, ScenarioDefinition definition, string draftScenarioPath)
        {
            List<string> paths = CollectAssetPaths(definition);
            string draftRoot = !string.IsNullOrEmpty(draftScenarioPath) ? Path.GetDirectoryName(draftScenarioPath) : null;
            string assetsRoot = ScenarioAuthoringStoragePaths.GetAssetsRootPath();
            for (int i = 0; i < paths.Count; i++)
            {
                string source = ResolveSource(paths[i], draftRoot, assetsRoot);
                if (string.IsNullOrEmpty(source))
                {
                    plan.Problems.Add("Referenced asset is missing: " + paths[i]);
                    continue;
                }
                AddSource(plan, paths[i], source);
            }
        }

        internal static List<string> CollectAssetPaths(ScenarioDefinition definition)
        {
            List<string> paths = new List<string>();
            if (definition == null)
                return paths;
            AssetReferencesDefinition assets = definition.AssetReferences;
            for (int i = 0; assets != null && assets.CustomSprites != null && i < assets.CustomSprites.Count; i++) AddUnique(paths, assets.CustomSprites[i] != null ? assets.CustomSprites[i].RelativePath : null);
            for (int i = 0; assets != null && assets.CustomIcons != null && i < assets.CustomIcons.Count; i++) AddUnique(paths, assets.CustomIcons[i] != null ? assets.CustomIcons[i].RelativePath : null);
            for (int i = 0; assets != null && assets.SpriteSwaps != null && i < assets.SpriteSwaps.Count; i++) AddUnique(paths, assets.SpriteSwaps[i] != null ? assets.SpriteSwaps[i].RelativePath : null);
            for (int i = 0; assets != null && assets.SceneSpritePlacements != null && i < assets.SceneSpritePlacements.Count; i++) AddUnique(paths, assets.SceneSpritePlacements[i] != null ? assets.SceneSpritePlacements[i].RelativePath : null);
            for (int i = 0; assets != null && assets.SpritePatches != null && i < assets.SpritePatches.Count; i++) AddUnique(paths, assets.SpritePatches[i] != null ? assets.SpritePatches[i].BaseRelativePath : null);
            for (int i = 0; definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberAppearanceConfig appearance = definition.FamilySetup.Members[i] != null ? definition.FamilySetup.Members[i].Appearance : null;
                if (appearance == null) continue;
                AddUnique(paths, appearance.HeadTexturePath);
                AddUnique(paths, appearance.TorsoTexturePath);
                AddUnique(paths, appearance.LegTexturePath);
            }
            return paths;
        }

        private static void AddDependencies(ScenarioPackagePlan plan, ScenarioDefinition definition)
        {
            for (int i = 0; definition.ModDependencies != null && i < definition.ModDependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                if (dependency == null || string.IsNullOrEmpty(dependency.ModId))
                    continue;
                string label = dependency.ModId + (string.IsNullOrEmpty(dependency.Version) ? string.Empty : " " + dependency.Version);
                plan.DeclaredMods.Add(label + " (" + dependency.Kind + ")");
                if (dependency.Kind == ScenarioModDependencyKind.Required)
                    plan.RequiredMods.Add(label);
            }
        }

        private static string[] GetUnsupportedWarnings(ScenarioValidationResult validation)
        {
            List<string> warnings = new List<string>();
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                string message = issues[i] != null ? issues[i].Message : null;
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Warning && !string.IsNullOrEmpty(message)
                    && (message.IndexOf("unsupported", StringComparison.OrdinalIgnoreCase) >= 0
                        || message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0
                        || message.IndexOf("runtime supports", StringComparison.OrdinalIgnoreCase) >= 0))
                    warnings.Add(message);
            }
            return warnings.ToArray();
        }

        private string BuildReadme(ScenarioDefinition definition, List<string> requiredMods, string[] limitations)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(definition.DisplayName ?? "Untitled Scenario");
            text.AppendLine(new string('=', Math.Max(3, (definition.DisplayName ?? "Scenario").Length)));
            text.AppendLine();
            text.AppendLine("DESCRIPTION"); text.AppendLine(Safe(definition.Description)); text.AppendLine();
            text.AppendLine("GOAL"); text.AppendLine(Safe(definition.Goal));
            string victory = FormatVictorySummary(definition);
            if (!string.IsNullOrEmpty(victory))
                text.AppendLine("Victory condition: " + victory);
            text.AppendLine();
            text.AppendLine("AUTHOR"); text.AppendLine(Safe(definition.Author));
            string honestyLine = _testChecklistService.BuildReadmeHonestyLine(definition);
            if (!string.IsNullOrEmpty(honestyLine))
                text.AppendLine(honestyLine);
            text.AppendLine("VERSION"); text.AppendLine(Safe(definition.Version));
            text.AppendLine("PLAY EXPERIENCE");
            text.AppendLine(definition.LaunchSetup != null ? definition.LaunchSetup.Mode.ToString() : ScenarioLaunchSetupMode.FullSetup.ToString());
            text.AppendLine("CREDITS"); text.AppendLine(Safe(definition.Credits)); text.AppendLine();
            text.AppendLine("INSTALLATION");
            text.AppendLine("Copy this scenario folder into <Sheltered game>\\mods\\<a loaded mod>\\Scenarios, then reopen the scenario book. Restart the game if the catalog was already cached.");
            AppendList(text, "REQUIRED MODS", requiredMods.ToArray(), "None declared.");
            AppendList(text, "KNOWN LIMITATIONS", limitations, "None reported by validation.");
            AppendAssetCredits(text, definition);
            return text.ToString();
        }

        private static string BuildManifest(ScenarioDefinition definition, List<string> requiredMods, string[] limitations)
        {
            StringBuilder output = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
            using (XmlWriter writer = XmlWriter.Create(output, settings))
            {
                writer.WriteStartElement("ScenarioPackage"); writer.WriteAttributeString("formatVersion", "1");
                writer.WriteElementString("Id", definition.Id ?? string.Empty);
                writer.WriteElementString("Title", definition.DisplayName ?? string.Empty);
                writer.WriteElementString("Version", definition.Version ?? string.Empty);
                writer.WriteElementString("Author", definition.Author ?? string.Empty);
                writer.WriteStartElement("RequiredMods");
                for (int i = 0; i < requiredMods.Count; i++) writer.WriteElementString("Mod", requiredMods[i]);
                writer.WriteEndElement();
                writer.WriteStartElement("KnownLimitations");
                for (int i = 0; i < limitations.Length; i++) writer.WriteElementString("Limitation", limitations[i]);
                writer.WriteEndElement();
                writer.WriteStartElement("AssetCredits");
                List<ScenarioAssetCreditDefinition> credits = definition.AssetReferences != null ? definition.AssetReferences.AssetCredits : null;
                for (int i = 0; credits != null && i < credits.Count; i++)
                {
                    ScenarioAssetCreditDefinition credit = credits[i];
                    if (!ShouldExportCredit(definition, credit)) continue;
                    writer.WriteStartElement("Asset");
                    writer.WriteAttributeString("path", credit.RelativePath);
                    writer.WriteString(credit.Credit);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            return output.ToString();
        }

        private static void AppendList(StringBuilder text, string heading, string[] values, string empty)
        {
            text.AppendLine(); text.AppendLine(heading);
            if (values == null || values.Length == 0) text.AppendLine(empty);
            else for (int i = 0; i < values.Length; i++) text.AppendLine("- " + values[i]);
        }

        private static void AppendAssetCredits(StringBuilder text, ScenarioDefinition definition)
        {
            List<ScenarioAssetCreditDefinition> credits = definition != null && definition.AssetReferences != null ? definition.AssetReferences.AssetCredits : null;
            text.AppendLine();
            text.AppendLine("ASSET CREDITS");
            bool wrote = false;
            for (int i = 0; credits != null && i < credits.Count; i++)
            {
                ScenarioAssetCreditDefinition credit = credits[i];
                if (!ShouldExportCredit(definition, credit)) continue;
                text.AppendLine("- " + credit.RelativePath + ": " + credit.Credit);
                wrote = true;
            }
            if (!wrote) text.AppendLine("None provided.");
        }

        private static bool ShouldExportCredit(ScenarioDefinition definition, ScenarioAssetCreditDefinition credit)
        {
            if (credit == null || string.IsNullOrEmpty(credit.RelativePath) || string.IsNullOrEmpty(credit.Credit)) return false;
            List<string> paths = CollectAssetPaths(definition);
            for (int i = 0; i < paths.Count; i++)
                if (string.Equals(NormalizeAssetPath(paths[i]), NormalizeAssetPath(credit.RelativePath), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string Safe(string value) { return string.IsNullOrEmpty(value) ? "Not provided." : value.Trim(); }

        // Only returns text when the scenario actually declares an end state, so the
        // README shows the victory beside the goal only when one exists.
        private static string FormatVictorySummary(ScenarioDefinition definition)
        {
            WinLossConditionsDefinition winLoss = definition != null ? definition.WinLossConditions : null;
            int wins = winLoss != null && winLoss.WinConditions != null ? winLoss.WinConditions.Count : 0;
            int losses = winLoss != null && winLoss.LossConditions != null ? winLoss.LossConditions.Count : 0;
            if (wins + losses == 0)
                return null;
            return wins.ToString(System.Globalization.CultureInfo.InvariantCulture) + " win / "
                + losses.ToString(System.Globalization.CultureInfo.InvariantCulture) + " loss condition(s)";
        }

        private static void AddUnique(List<string> paths, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) return;
            for (int i = 0; i < paths.Count; i++) if (string.Equals(paths[i], relativePath, StringComparison.OrdinalIgnoreCase)) return;
            paths.Add(relativePath);
        }

        private static string ResolveSource(string relativePath, string draftRoot, string assetsRoot)
        {
            string source = ScenarioPackagePlan.ResolveContainedPath(draftRoot, relativePath);
            if (!string.IsNullOrEmpty(source) && File.Exists(source)) return source;
            source = ScenarioPackagePlan.ResolveContainedPath(assetsRoot, relativePath);
            return !string.IsNullOrEmpty(source) && File.Exists(source) ? source : null;
        }

        private static void AddGenerated(ScenarioPackagePlan plan, string path, byte[] content)
        {
            plan.Entries.Add(new ScenarioPackageEntry { RelativePath = path, Content = content, Size = content != null ? content.LongLength : 0L });
            plan.TotalSize += content != null ? content.LongLength : 0L;
        }

        private static void AddSource(ScenarioPackagePlan plan, string path, string source)
        {
            long size = new FileInfo(source).Length;
            plan.Entries.Add(new ScenarioPackageEntry { RelativePath = path, SourcePath = source, Size = size });
            plan.TotalSize += size;
        }

        private static void FindOrphans(ScenarioPackagePlan plan)
        {
            if (string.IsNullOrEmpty(plan.PackageRoot) || !Directory.Exists(plan.PackageRoot)) return;
            string root = Path.GetFullPath(plan.PackageRoot).TrimEnd(Path.DirectorySeparatorChar);
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(root.Length + 1);
                bool expected = false;
                for (int e = 0; e < plan.Entries.Count; e++) if (string.Equals(relative.Replace('/', '\\'), plan.Entries[e].RelativePath.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase)) { expected = true; break; }
                if (!expected) plan.Problems.Add("Orphan staging file will be removed on export: " + relative);
            }
        }
    }
}
