using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Cortex.Core.Services
{
    public static class DecompilerCachePathNaming
    {
        public static string BuildTypeCacheRelativePathStem(string assemblyIdentityOrPath, Type type)
        {
            return BuildTypeCacheRelativePathStem(assemblyIdentityOrPath, NormalizeTypeName(type));
        }

        public static string BuildMethodCacheRelativePathStem(string assemblyIdentityOrPath, MethodBase method, int metadataToken)
        {
            return BuildMethodCacheRelativePathStem(
                assemblyIdentityOrPath,
                method != null ? NormalizeTypeName(method.DeclaringType) : string.Empty,
                method != null ? method.Name : string.Empty,
                metadataToken);
        }

        public static string BuildTypeCacheRelativePathStem(string assemblyIdentityOrPath, string typeName)
        {
            var assemblySegment = SanitizePathSegment(Path.GetFileNameWithoutExtension(assemblyIdentityOrPath));
            if (string.IsNullOrEmpty(assemblySegment))
            {
                assemblySegment = "UnknownAssembly";
            }

            var relativeTypePath = BuildTypeRelativePath(typeName);
            return string.IsNullOrEmpty(relativeTypePath)
                ? assemblySegment
                : Path.Combine(assemblySegment, relativeTypePath);
        }

        public static string BuildMethodCacheRelativePathStem(string assemblyIdentityOrPath, string declaringTypeName, string methodName, int metadataToken)
        {
            var typeStem = BuildTypeCacheRelativePathStem(assemblyIdentityOrPath, declaringTypeName);
            var typeDirectory = Path.GetDirectoryName(typeStem) ?? string.Empty;
            var typeName = Path.GetFileName(typeStem);
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = "UnknownType";
            }

            var methodFileName = typeName + "." + SanitizeFileStem(methodName) + "_0x" + metadataToken.ToString("X8");
            return string.IsNullOrEmpty(typeDirectory)
                ? methodFileName
                : Path.Combine(typeDirectory, methodFileName);
        }

        public static bool IsLegacyCachePath(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            return fileName.IndexOf("_type_0x", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_method_0x", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildTypeRelativePath(string typeName)
        {
            var normalized = NormalizeTypeName(typeName);
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

        private static string NormalizeTypeName(Type type)
        {
            return type == null
                ? string.Empty
                : NormalizeTypeName(type.FullName ?? type.Name ?? string.Empty);
        }

        private static string NormalizeTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace('+', '.');
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
