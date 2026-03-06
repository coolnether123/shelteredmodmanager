using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class CsprojBuildCommandResolver : IBuildCommandResolver
    {
        public BuildCommand Resolve(CortexProjectDefinition project, bool clean, string configuration)
        {
            if (project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(project.BuildCommandOverride))
            {
                return ResolveOverride(project, configuration);
            }

            var isSdkStyle = IsSdkStyleProject(project.ProjectFilePath);
            var command = new BuildCommand();
            command.FileName = "dotnet";
            command.WorkingDirectory = Path.GetDirectoryName(project.ProjectFilePath);

            if (isSdkStyle)
            {
                command.Arguments = (clean ? "clean " : "build ") + Quote(project.ProjectFilePath) + " --configuration " + (configuration ?? "Debug");
            }
            else
            {
                command.Arguments = "msbuild " + Quote(project.ProjectFilePath) + " /t:" + (clean ? "Clean;Build" : "Build") + " /p:Configuration=" + (configuration ?? "Debug");
            }

            ResolveOutputs(project, configuration, command);
            return command;
        }

        private static BuildCommand ResolveOverride(CortexProjectDefinition project, string configuration)
        {
            var overrideText = project.BuildCommandOverride.Trim();
            var separator = overrideText.IndexOf(' ');
            var command = new BuildCommand();
            if (separator < 0)
            {
                command.FileName = overrideText;
                command.Arguments = string.Empty;
            }
            else
            {
                command.FileName = overrideText.Substring(0, separator);
                command.Arguments = overrideText.Substring(separator + 1);
            }

            command.WorkingDirectory = Path.GetDirectoryName(project.ProjectFilePath);
            ResolveOutputs(project, configuration, command);
            return command;
        }

        private static void ResolveOutputs(CortexProjectDefinition project, string configuration, BuildCommand command)
        {
            command.OutputAssemblyPath = project.OutputAssemblyPath;
            command.OutputPdbPath = project.OutputPdbPath;

            if (!string.IsNullOrEmpty(command.OutputAssemblyPath))
            {
                return;
            }

            try
            {
                var doc = XDocument.Load(project.ProjectFilePath);
                XNamespace ns = doc.Root.Name.Namespace;
                var assemblyName = GetElementValue(doc, ns, "AssemblyName");
                if (string.IsNullOrEmpty(assemblyName))
                {
                    assemblyName = Path.GetFileNameWithoutExtension(project.ProjectFilePath);
                }

                var outputPath = GetOutputPath(doc, ns, configuration ?? "Debug");
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = Path.Combine("bin", configuration ?? "Debug");
                }

                var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
                command.OutputAssemblyPath = Path.GetFullPath(Path.Combine(Path.Combine(projectDir, outputPath), assemblyName + ".dll"));
                command.OutputPdbPath = Path.GetFullPath(Path.Combine(Path.Combine(projectDir, outputPath), assemblyName + ".pdb"));
            }
            catch
            {
            }
        }

        private static string GetOutputPath(XDocument doc, XNamespace ns, string configuration)
        {
            foreach (var group in doc.Descendants(ns + "PropertyGroup"))
            {
                var condition = (string)group.Attribute("Condition");
                if (!string.IsNullOrEmpty(condition) &&
                    condition.IndexOf(configuration, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var outputPath = group.Element(ns + "OutputPath");
                if (outputPath != null && !string.IsNullOrEmpty(outputPath.Value))
                {
                    return outputPath.Value.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetElementValue(XDocument doc, XNamespace ns, string name)
        {
            var element = doc.Root.Descendants(ns + name).FirstOrDefault();
            return element != null ? element.Value.Trim() : string.Empty;
        }

        private static bool IsSdkStyleProject(string projectFilePath)
        {
            var firstLine = File.ReadAllText(projectFilePath);
            return firstLine.IndexOf("<Project Sdk=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Quote(string text)
        {
            return "\"" + (text ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
