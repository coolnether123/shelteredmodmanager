using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class SourceReferenceService : ISourceReferenceService
    {
        private readonly IDecompilerClient _decompilerClient;
        private readonly IRuntimeAssemblyMemberService _runtimeAssemblyMemberService;

        public SourceReferenceService(IDecompilerClient decompilerClient)
            : this(decompilerClient, new RuntimeAssemblyMemberService())
        {
        }

        public SourceReferenceService(IDecompilerClient decompilerClient, IRuntimeAssemblyMemberService runtimeAssemblyMemberService)
        {
            _decompilerClient = decompilerClient;
            _runtimeAssemblyMemberService = runtimeAssemblyMemberService ?? new RuntimeAssemblyMemberService();
        }

        public DecompilerResponse GetSource(DecompilerRequest request)
        {
            var resolvedType = request != null && request.EntityKind == DecompilerEntityKind.Type
                ? _runtimeAssemblyMemberService.ResolveType(request.AssemblyPath, request.MetadataToken)
                : null;
            var resolvedMethod = request != null && request.EntityKind == DecompilerEntityKind.Method
                ? _runtimeAssemblyMemberService.ResolveMethod(request.AssemblyPath, request.MetadataToken)
                : null;
            var effectiveRequest = BuildEffectiveRequest(request, resolvedType, resolvedMethod);
            var response = _decompilerClient.Decompile(effectiveRequest);
            PopulateResolvedDisplayName(response, resolvedType, resolvedMethod);
            PopulateXmlDocumentation(effectiveRequest, response, resolvedType, resolvedMethod);
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

        private static void PopulateResolvedDisplayName(DecompilerResponse response, Type resolvedType, MethodBase resolvedMethod)
        {
            if (response == null)
            {
                return;
            }

            if (resolvedType != null)
            {
                response.ResolvedMemberDisplayName = resolvedType.FullName;
                return;
            }

            if (resolvedMethod != null)
            {
                response.ResolvedMemberDisplayName = BuildMethodDisplayName(resolvedMethod);
            }
        }

        private static DecompilerRequest BuildEffectiveRequest(DecompilerRequest request, Type resolvedType, MethodBase resolvedMethod)
        {
            if (request == null)
            {
                return null;
            }

            return new DecompilerRequest
            {
                AssemblyPath = request.AssemblyPath,
                MetadataToken = request.MetadataToken,
                IgnoreCache = request.IgnoreCache,
                EntityKind = request.EntityKind,
                CacheRelativePathStem = BuildCacheRelativePathStem(request, resolvedType, resolvedMethod)
            };
        }

        private void PopulateXmlDocumentation(DecompilerRequest request, DecompilerResponse response, Type resolvedType, MethodBase resolvedMethod)
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
                var xmlDocument = new XmlDocument();
                xmlDocument.Load(xmlPath);
                var xmlTextBuilder = new StringBuilder();

                if (request.EntityKind == DecompilerEntityKind.Type)
                {
                    var type = resolvedType ?? _runtimeAssemblyMemberService.ResolveType(request.AssemblyPath, request.MetadataToken);
                    if (type == null)
                    {
                        return;
                    }

                    response.ResolvedMemberDisplayName = type.FullName;
                    AppendMemberDocumentation(xmlDocument, BuildTypeMemberName(type), "Type", xmlTextBuilder);
                    response.XmlDocumentationText = xmlTextBuilder.ToString().Trim();
                    return;
                }

                var method = resolvedMethod ?? _runtimeAssemblyMemberService.ResolveMethod(request.AssemblyPath, request.MetadataToken);
                if (method == null)
                {
                    return;
                }

                response.ResolvedMemberDisplayName = BuildMethodDisplayName(method);

                AppendMemberDocumentation(xmlDocument, BuildTypeMemberName(method.DeclaringType), "Type", xmlTextBuilder);
                AppendMemberDocumentation(xmlDocument, BuildMethodMemberName(method), "Method", xmlTextBuilder);

                response.XmlDocumentationText = xmlTextBuilder.ToString().Trim();
            }
            catch
            {
                response.XmlDocumentationText = string.Empty;
            }
        }

        private static string BuildCacheRelativePathStem(DecompilerRequest request, Type resolvedType, MethodBase resolvedMethod)
        {
            if (request == null || string.IsNullOrEmpty(request.AssemblyPath))
            {
                return string.Empty;
            }

            var assemblySegment = SanitizePathSegment(Path.GetFileNameWithoutExtension(request.AssemblyPath));
            if (string.IsNullOrEmpty(assemblySegment))
            {
                assemblySegment = "UnknownAssembly";
            }

            if (resolvedType != null)
            {
                return Path.Combine(assemblySegment, BuildTypeRelativePath(resolvedType));
            }

            if (resolvedMethod != null)
            {
                return Path.Combine(assemblySegment, BuildMethodRelativePath(resolvedMethod, request.MetadataToken));
            }

            var entityPrefix = request.EntityKind == DecompilerEntityKind.Type ? "type" : "method";
            return Path.Combine(assemblySegment, entityPrefix + "_0x" + request.MetadataToken.ToString("X8"));
        }

        private static string BuildTypeRelativePath(Type type)
        {
            var normalized = NormalizeTypeName(type);
            if (string.IsNullOrEmpty(normalized))
            {
                return "UnknownType";
            }

            var segments = normalized.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return "UnknownType";
            }

            if (segments.Length == 1)
            {
                return SanitizeFileStem(segments[0]);
            }

            var path = SanitizePathSegment(segments[0]);
            for (var i = 1; i < segments.Length - 1; i++)
            {
                path = Path.Combine(path, SanitizePathSegment(segments[i]));
            }

            return Path.Combine(path, SanitizeFileStem(segments[segments.Length - 1]));
        }

        private static string BuildMethodRelativePath(MethodBase method, int metadataToken)
        {
            var declaringType = method != null ? method.DeclaringType : null;
            var typePath = BuildTypeRelativePath(declaringType);
            var typeDirectory = Path.GetDirectoryName(typePath) ?? string.Empty;
            var typeName = Path.GetFileName(typePath);
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = "UnknownType";
            }

            var methodName = SanitizeFileStem(method != null ? method.Name : "Method");
            var fileName = typeName + "." + methodName + "_0x" + metadataToken.ToString("X8");
            return string.IsNullOrEmpty(typeDirectory)
                ? fileName
                : Path.Combine(typeDirectory, fileName);
        }

        private static void AppendMemberDocumentation(XmlDocument document, string memberName, string label, StringBuilder builder)
        {
            if (document == null || string.IsNullOrEmpty(memberName) || builder == null)
            {
                return;
            }

            var members = document.DocumentElement != null
                ? document.DocumentElement.SelectSingleNode("members") as XmlElement
                : null;
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

            var paramElements = member.SelectNodes("param");
            if (paramElements == null)
            {
                return;
            }

            for (var i = 0; i < paramElements.Count; i++)
            {
                var param = paramElements[i] as XmlElement;
                if (param == null)
                {
                    continue;
                }

                var name = param.HasAttribute("name") ? param.GetAttribute("name") : string.Empty;
                var text = NormalizeXmlText(param.InnerText);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.AppendLine("Param " + name + ": " + text);
                }
            }
        }

        private static XmlElement FindMemberElement(XmlElement members, string memberName)
        {
            var memberElements = members.SelectNodes("member");
            if (memberElements == null)
            {
                return null;
            }

            for (var i = 0; i < memberElements.Count; i++)
            {
                var member = memberElements[i] as XmlElement;
                if (member == null)
                {
                    continue;
                }

                var name = member.HasAttribute("name") ? member.GetAttribute("name") : string.Empty;
                if (string.Equals(name, memberName, StringComparison.Ordinal))
                {
                    return member;
                }
            }

            return null;
        }

        private static void AppendElementValue(StringBuilder builder, XmlElement member, string elementName, string label)
        {
            var element = member != null ? member.SelectSingleNode(elementName) as XmlElement : null;
            if (element == null)
            {
                return;
            }

            var text = NormalizeXmlText(element.InnerText);
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

        private static string NormalizeTypeName(Type type)
        {
            return type == null
                ? string.Empty
                : (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.');
        }

        private static string SanitizeFileStem(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (current == '`')
                {
                    builder.Append('_');
                    continue;
                }

                builder.Append(Array.IndexOf(invalid, current) >= 0 ? '_' : current);
            }

            return builder.ToString().Trim('.', ' ');
        }

        private static string SanitizePathSegment(string value)
        {
            var sanitized = SanitizeFileStem(value);
            return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
        }
    }
}
