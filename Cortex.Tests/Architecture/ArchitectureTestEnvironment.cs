using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Cortex.Tests.Architecture
{
    internal static class ArchitectureTestEnvironment
    {
        public static readonly string RepoRoot = ResolveRepoRoot();

        public static string ReadRepoFile(params string[] relativeSegments)
        {
            return File.ReadAllText(GetRepoPath(relativeSegments));
        }

        public static string GetRepoPath(params string[] relativeSegments)
        {
            var segments = new string[relativeSegments.Length + 1];
            segments[0] = RepoRoot;
            Array.Copy(relativeSegments, 0, segments, 1, relativeSegments.Length);
            return Path.Combine(segments);
        }

        public static string[] GetProjectSourceFiles(IEnumerable<string> projectNames)
        {
            return projectNames
                .SelectMany(GetProjectSourceFiles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] GetProjectSourceFiles(string projectName)
        {
            var projectDirectory = Path.GetDirectoryName(GetProjectPath(projectName));
            Assert.True(!string.IsNullOrEmpty(projectDirectory), "Could not resolve project directory for " + projectName + ".");

            return Directory
                .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IList<string> LoadProjectReferenceNames(string projectName)
        {
            var document = XDocument.Load(GetProjectPath(projectName));
            XNamespace xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            return document
                .Descendants()
                .Where(element => element.Name == xmlNamespace + "ProjectReference" || element.Name.LocalName == "ProjectReference")
                .Select(element => element.Attribute("Include"))
                .Where(attribute => attribute != null && !string.IsNullOrEmpty(attribute.Value))
                .Select(attribute => Path.GetFileNameWithoutExtension(attribute.Value))
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        public static string GetProjectPath(string projectName)
        {
            var projectPath = Directory
                .GetFiles(RepoRoot, projectName + ".csproj", SearchOption.AllDirectories)
                .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), projectName, StringComparison.Ordinal));

            Assert.True(projectPath != null, "Could not locate project file for " + projectName + ".");
            return projectPath;
        }

        private static string ResolveRepoRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Cortex.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate the repository root from the test base directory.");
        }
    }
}
