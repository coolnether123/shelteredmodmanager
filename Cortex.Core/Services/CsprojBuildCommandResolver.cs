using System;
using System.IO;
using System.Xml;
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
                var doc = new XmlDocument();
                doc.Load(project.ProjectFilePath);
                var assemblyName = GetElementValue(doc, "AssemblyName");
                if (string.IsNullOrEmpty(assemblyName))
                {
                    assemblyName = Path.GetFileNameWithoutExtension(project.ProjectFilePath);
                }

                var outputPath = GetOutputPath(doc, configuration ?? "Debug");
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

        private static string GetOutputPath(XmlDocument doc, string configuration)
        {
            var propertyGroups = doc != null ? doc.GetElementsByTagName("*") : null;
            if (propertyGroups == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < propertyGroups.Count; i++)
            {
                var group = propertyGroups[i] as XmlElement;
                if (group == null || !string.Equals(group.LocalName, "PropertyGroup", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var condition = group.HasAttribute("Condition") ? group.GetAttribute("Condition") : string.Empty;
                if (!string.IsNullOrEmpty(condition) &&
                    condition.IndexOf(configuration, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var outputPath = GetChildElementValue(group, "OutputPath");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    return outputPath;
                }
            }

            return string.Empty;
        }

        private static string GetElementValue(XmlDocument doc, string name)
        {
            var elements = doc != null ? doc.GetElementsByTagName("*") : null;
            if (elements == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i] as XmlElement;
                if (element != null && string.Equals(element.LocalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (element.InnerText ?? string.Empty).Trim();
                }
            }

            return string.Empty;
        }

        private static string GetChildElementValue(XmlElement parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            for (var i = 0; i < parent.ChildNodes.Count; i++)
            {
                var child = parent.ChildNodes[i] as XmlElement;
                if (child != null && string.Equals(child.LocalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (child.InnerText ?? string.Empty).Trim();
                }
            }

            return string.Empty;
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
