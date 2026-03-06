using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class SourceReferenceService : ISourceReferenceService
    {
        private readonly IDecompilerClient _decompilerClient;

        public SourceReferenceService(IDecompilerClient decompilerClient)
        {
            _decompilerClient = decompilerClient;
        }

        public DecompilerResponse GetSource(DecompilerRequest request)
        {
            var response = _decompilerClient.Decompile(request);
            PopulateXmlDocumentation(request, response);
            return response;
        }

        public int MapSourceLineToOffset(string mapText, int sourceLine)
        {
            var map = ParseMap(mapText);
            if (map.ContainsKey(sourceLine))
            {
                return map[sourceLine];
            }

            var bestLine = -1;
            var bestDistance = int.MaxValue;
            foreach (var kv in map)
            {
                var distance = Math.Abs(kv.Key - sourceLine);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = kv.Key;
                }
            }

            return bestLine > 0 ? map[bestLine] : -1;
        }

        public int MapOffsetToSourceLine(string mapText, int ilOffset)
        {
            var map = ParseMap(mapText);
            var bestLine = -1;
            var bestDistance = int.MaxValue;
            foreach (var kv in map)
            {
                var distance = Math.Abs(kv.Value - ilOffset);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = kv.Key;
                }
            }

            return bestLine;
        }

        private static Dictionary<int, int> ParseMap(string mapText)
        {
            var map = new Dictionary<int, int>();
            if (string.IsNullOrEmpty(mapText))
            {
                return map;
            }

            var lines = mapText.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line) || line.StartsWith("LineNumber"))
                {
                    continue;
                }

                var parts = line.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }

                int sourceLine;
                int ilOffset;
                if (int.TryParse(parts[0], out sourceLine) && int.TryParse(parts[1], out ilOffset) && !map.ContainsKey(sourceLine))
                {
                    map[sourceLine] = ilOffset;
                }
            }

            return map;
        }

        private static void PopulateXmlDocumentation(DecompilerRequest request, DecompilerResponse response)
        {
            if (request == null || response == null || string.IsNullOrEmpty(request.AssemblyPath) || request.MetadataToken <= 0)
            {
                return;
            }

            var xmlPath = Path.ChangeExtension(request.AssemblyPath, ".xml");
            response.XmlDocumentationPath = xmlPath;
            if (!File.Exists(xmlPath))
            {
                return;
            }

            try
            {
                if (request.EntityKind == DecompilerEntityKind.Type)
                {
                    var type = ResolveType(request.AssemblyPath, request.MetadataToken);
                    if (type == null)
                    {
                        return;
                    }

                    response.ResolvedMemberDisplayName = type.FullName;
                    var document = XDocument.Load(xmlPath);
                    var builder = new StringBuilder();
                    AppendMemberDocumentation(document, BuildTypeMemberName(type), "Type", builder);
                    response.XmlDocumentationText = builder.ToString().Trim();
                    return;
                }

                var method = ResolveMethod(request.AssemblyPath, request.MetadataToken);
                if (method == null)
                {
                    return;
                }

                response.ResolvedMemberDisplayName = BuildMethodDisplayName(method);
                var document = XDocument.Load(xmlPath);
                var builder = new StringBuilder();

                AppendMemberDocumentation(document, BuildTypeMemberName(method.DeclaringType), "Type", builder);
                AppendMemberDocumentation(document, BuildMethodMemberName(method), "Method", builder);

                response.XmlDocumentationText = builder.ToString().Trim();
            }
            catch
            {
                response.XmlDocumentationText = string.Empty;
            }
        }

        private static Type ResolveType(string assemblyPath, int metadataToken)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (!string.Equals(Path.GetFullPath(loadedAssemblies[i].Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return loadedAssemblies[i].ManifestModule.ResolveType(metadataToken);
                }
                catch
                {
                }
            }

            try
            {
                return Assembly.LoadFrom(assemblyPath).ManifestModule.ResolveType(metadataToken);
            }
            catch
            {
                return null;
            }
        }

        private static MethodBase ResolveMethod(string assemblyPath, int metadataToken)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var assembly = loadedAssemblies[i];
                try
                {
                    if (!string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return assembly.ManifestModule.ResolveMethod(metadataToken);
                }
                catch
                {
                }
            }

            try
            {
                return Assembly.LoadFrom(assemblyPath).ManifestModule.ResolveMethod(metadataToken);
            }
            catch
            {
                return null;
            }
        }

        private static void AppendMemberDocumentation(XDocument document, string memberName, string label, StringBuilder builder)
        {
            if (document == null || string.IsNullOrEmpty(memberName) || builder == null)
            {
                return;
            }

            var members = document.Root != null ? document.Root.Element("members") : null;
            if (members == null)
            {
                return;
            }

            var member = FindMemberElement(members, memberName);
            if (member == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(label + ": " + memberName);
            AppendElementValue(builder, member, "summary", "Summary");
            AppendElementValue(builder, member, "remarks", "Remarks");
            AppendElementValue(builder, member, "returns", "Returns");

            var paramElements = member.Elements("param");
            foreach (var param in paramElements)
            {
                var name = param.Attribute("name") != null ? param.Attribute("name").Value : string.Empty;
                var text = NormalizeXmlText(param.Value);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.AppendLine("Param " + name + ": " + text);
                }
            }
        }

        private static XElement FindMemberElement(XElement members, string memberName)
        {
            var memberElements = members.Elements("member");
            foreach (var member in memberElements)
            {
                var name = member.Attribute("name") != null ? member.Attribute("name").Value : string.Empty;
                if (string.Equals(name, memberName, StringComparison.Ordinal))
                {
                    return member;
                }
            }

            return null;
        }

        private static void AppendElementValue(StringBuilder builder, XElement member, string elementName, string label)
        {
            var element = member.Element(elementName);
            if (element == null)
            {
                return;
            }

            var text = NormalizeXmlText(element.Value);
            if (!string.IsNullOrEmpty(text))
            {
                builder.AppendLine(label + ": " + text);
            }
        }

        private static string NormalizeXmlText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace("  ", " ").Trim();
        }

        private static string BuildTypeMemberName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            return "T:" + GetXmlTypeName(type);
        }

        private static string BuildMethodMemberName(MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append("M:");
            builder.Append(GetXmlTypeName(method.DeclaringType));
            builder.Append(".");
            builder.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                builder.Append("(");
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(parameters[i].ParameterType));
                }
                builder.Append(")");
            }

            return builder.ToString();
        }

        private static string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            return method.DeclaringType.FullName + "." + method.Name;
        }

        private static string GetXmlTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsByRef)
            {
                return GetXmlTypeName(type.GetElementType()) + "@";
            }

            if (type.IsArray)
            {
                return GetXmlTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var baseName = (genericType.FullName ?? genericType.Name).Replace('+', '.');
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    baseName = baseName.Substring(0, tickIndex);
                }

                var args = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(baseName);
                builder.Append("{");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(args[i]));
                }
                builder.Append("}");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name).Replace('+', '.');
        }
    }
}
