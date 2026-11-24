using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioPackageContents
    {
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly Action<ScenarioPackageStep, string> _beforeStep;

        internal ScenarioPackageContents(IScenarioDefinitionSerializer serializer, IScenarioDefinitionValidator validator,
            Action<ScenarioPackageStep, string> beforeStep = null)
        {
            _serializer = serializer;
            _validator = validator;
            _beforeStep = beforeStep;
        }

        internal ScenarioValidationResult Replace(string destination, string expectedId, Func<IEnumerable<ScenarioPackageEntry>> entries)
        {
            Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ScenarioValidationResult validation = null;
            ShelteredScenarioPackages.Replace(destination,
                delegate(string stage) { Write(stage, entries(), hashes); },
                delegate(string stage)
                {
                    VerifyFiles(stage, hashes);
                    validation = Validate(stage, expectedId);
                },
                _beforeStep);
            return validation;
        }

        internal static IEnumerable<ScenarioPackageEntry> ReadDirectory(string path)
        {
            string root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (FileInfo file in EnumerateFiles(new DirectoryInfo(root)))
                yield return new ScenarioPackageEntry { RelativePath = file.FullName.Substring(root.Length + 1), SourcePath = file.FullName };
        }

        internal static string SafeFolderName(string value)
        {
            char[] name = (value ?? string.Empty).Trim().ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < name.Length; i++)
                if (char.IsWhiteSpace(name[i]) || Array.IndexOf(invalid, name[i]) >= 0)
                    name[i] = '_';
            string safe = new string(name).Trim('_', '.');
            return string.IsNullOrEmpty(safe) ? "scenario" : safe;
        }

        private static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo directory)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked directories are not supported in scenario packages.");

            foreach (FileSystemInfo entry in directory.GetFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked files are not supported in scenario packages.");
                DirectoryInfo child = entry as DirectoryInfo;
                if (child == null)
                    yield return (FileInfo)entry;
                else
                    foreach (FileInfo file in EnumerateFiles(child))
                        yield return file;
            }
        }

        private static void Write(string stage, IEnumerable<ScenarioPackageEntry> entries, Dictionary<string, string> hashes)
        {
            foreach (ScenarioPackageEntry entry in entries)
            {
                string target = ScenarioPackagePlan.ResolveContainedPath(stage, entry.RelativePath);
                if (target == null || hashes.ContainsKey(target))
                    throw new IOException("The package contains an invalid or duplicate file name.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (entry.Content != null)
                {
                    using (MemoryStream input = new MemoryStream(entry.Content, false))
                        hashes.Add(target, Hash(input));
                    using (FileStream output = new FileStream(target, FileMode.CreateNew))
                        output.Write(entry.Content, 0, entry.Content.Length);
                }
                else
                {
                    using (FileStream input = File.OpenRead(entry.SourcePath))
                    {
                        hashes.Add(target, Hash(input));
                        File.Copy(entry.SourcePath, target, false);
                    }
                }
            }
        }

        private static void VerifyFiles(string stage, Dictionary<string, string> hashes)
        {
            int count = 0;
            foreach (FileInfo file in EnumerateFiles(new DirectoryInfo(stage)))
            {
                using (FileStream input = file.OpenRead())
                {
                    string expected;
                    if (!hashes.TryGetValue(file.FullName, out expected) || Hash(input) != expected)
                        throw new IOException("A staged package file failed its SHA-256 check.");
                }
                count++;
            }
            if (count != hashes.Count)
                throw new IOException("The staged package is missing files.");
        }

        private ScenarioValidationResult Validate(string stage, string expectedId)
        {
            string scenarioPath = Path.Combine(stage, ScenarioEditorDefinitionSerializer.DefaultFileName);
            ScenarioDefinition definition = _serializer.Load(scenarioPath);
            if (definition == null || string.IsNullOrEmpty(expectedId)
                || !string.Equals(definition.Id, expectedId, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The staged package has the wrong scenario identity.");

            XmlDocument manifest = new XmlDocument { XmlResolver = null };
            XmlReaderSettings settings = new XmlReaderSettings { ProhibitDtd = true, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(Path.Combine(stage, ScenarioPackagePlanner.ManifestFileName), settings))
                manifest.Load(reader);
            XmlElement package = manifest.DocumentElement;
            if (package == null || package.Name != "ScenarioPackage" || package.GetAttribute("formatVersion") != "1"
                || package.SelectNodes("Id").Count != 1
                || !string.Equals(package.SelectSingleNode("Id").InnerText, definition.Id, StringComparison.OrdinalIgnoreCase)
                || package.SelectNodes("Title").Count != 1 || package.SelectNodes("Version").Count != 1
                || package.SelectNodes("Author").Count != 1 || package.SelectNodes("RequiredMods").Count != 1
                || package.SelectNodes("KnownLimitations").Count != 1 || package.SelectNodes("AssetCredits").Count != 1)
                throw new IOException("The staged scenario package manifest is invalid.");

            foreach (string relative in ScenarioPackagePlanner.CollectAssetPaths(definition))
            {
                string path = ScenarioPackagePlan.ResolveContainedPath(stage, relative);
                if (path == null || !File.Exists(path))
                    throw new IOException("The staged scenario package is missing a referenced asset.");
            }

            ScenarioValidationResult validation = _validator.Validate(definition, scenarioPath);
            if (validation == null)
                throw new IOException("Scenario validation could not run.");
            foreach (ScenarioValidationIssue issue in validation.Issues)
                if (issue != null && issue.Severity == ScenarioIssueSeverity.Error)
                    throw new IOException("Staged scenario validation failed: " + issue.Message);
            return validation;
        }

        private static string Hash(Stream stream)
        {
            using (SHA256 hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(stream));
        }
    }
}
