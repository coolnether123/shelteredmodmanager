using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Reads ModPrivacy metadata directly from assembly metadata
    /// so policy can be enforced before expensive decompilation work.
    /// Precedence: Method > Declaring Type > Assembly.
    /// </summary>
    public sealed class PrivacyInspector
    {
        private const string PrivacyAttributeName = "ModPrivacyAttribute";
        private const string PrivacyAttributeFullName = "ModAPI.Core.ModPrivacyAttribute";

        /// <summary>
        /// Returns privacy policy and stable method signature information for the provided token.
        /// </summary>
        public PrivacyCheckResult Check(string assemblyPath, int methodToken)
        {
            using (var fs = System.IO.File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(fs))
            {
                var reader = peReader.GetMetadataReader();
                var methodHandle = MetadataTokens.MethodDefinitionHandle(methodToken);
                if (methodHandle.IsNil)
                {
                    throw new ArgumentException("Token is not a valid method definition token.", nameof(methodToken));
                }

                var methodDef = reader.GetMethodDefinition(methodHandle);
                var declaringTypeHandle = methodDef.GetDeclaringType();
                var typeDef = reader.GetTypeDefinition(declaringTypeHandle);

                var provider = new SignatureTypeNameProvider(reader);
                var methodName = reader.GetString(methodDef.Name);
                var signature = BuildMethodSignature(reader, methodDef, methodName, provider);

                var decision = ResolvePrivacy(reader, methodDef, typeDef, provider);
                return new PrivacyCheckResult
                {
                    Level = decision.Level,
                    Reason = decision.Reason ?? string.Empty,
                    MethodName = methodName,
                    MethodSignature = signature
                };
            }
        }

        /// <summary>
        /// Resolves effective privacy using policy precedence.
        /// </summary>
        internal static PrivacyDecision ResolvePrivacy(MetadataReader reader, MethodDefinition methodDef, TypeDefinition typeDef, SignatureTypeNameProvider provider)
        {
            PrivacyDecision decision;

            if (TryGetPrivacy(reader, methodDef.GetCustomAttributes(), provider, out decision))
            {
                return decision;
            }

            if (TryGetPrivacy(reader, typeDef.GetCustomAttributes(), provider, out decision))
            {
                return decision;
            }

            var asmDef = reader.GetAssemblyDefinition();
            if (TryGetPrivacy(reader, asmDef.GetCustomAttributes(), provider, out decision))
            {
                return decision;
            }

            return new PrivacyDecision { Level = PrivacyLevel.Public, Reason = string.Empty };
        }

        /// <summary>
        /// Builds a readable method signature from metadata-only information.
        /// </summary>
        internal static string BuildMethodSignature(MetadataReader reader, MethodDefinition methodDef, string methodName, SignatureTypeNameProvider provider)
        {
            var decoded = methodDef.DecodeSignature(provider, null);
            var paramNames = new List<string>();
            foreach (var ph in methodDef.GetParameters())
            {
                var parameter = reader.GetParameter(ph);
                if (parameter.SequenceNumber == 0)
                {
                    continue;
                }

                var pName = reader.GetString(parameter.Name);
                if (string.IsNullOrEmpty(pName))
                {
                    pName = "arg" + (parameter.SequenceNumber - 1);
                }

                paramNames.Add(pName);
            }

            var parts = new List<string>();
            for (var i = 0; i < decoded.ParameterTypes.Length; i++)
            {
                var typeName = decoded.ParameterTypes[i] ?? "object";
                var pName = i < paramNames.Count ? paramNames[i] : "arg" + i;
                parts.Add(typeName + " " + pName);
            }

            return (decoded.ReturnType ?? "void") + " " + methodName + "(" + string.Join(", ", parts.ToArray()) + ")";
        }

        private static bool TryGetPrivacy(
            MetadataReader reader,
            CustomAttributeHandleCollection attributes,
            SignatureTypeNameProvider provider,
            out PrivacyDecision decision)
        {
            foreach (var attrHandle in attributes)
            {
                var attr = reader.GetCustomAttribute(attrHandle);
                var attrTypeName = GetAttributeTypeName(reader, attr);
                if (!IsPrivacyAttribute(attrTypeName))
                {
                    continue;
                }

                var parsed = ParsePrivacyAttribute(attr, provider);
                decision = parsed;
                return true;
            }

            decision = new PrivacyDecision { Level = PrivacyLevel.Public, Reason = string.Empty };
            return false;
        }

        private static bool IsPrivacyAttribute(string attrTypeName)
        {
            if (string.IsNullOrEmpty(attrTypeName))
            {
                return false;
            }

            return string.Equals(attrTypeName, PrivacyAttributeName, StringComparison.Ordinal) ||
                   string.Equals(attrTypeName, PrivacyAttributeFullName, StringComparison.Ordinal) ||
                   attrTypeName.EndsWith("." + PrivacyAttributeName, StringComparison.Ordinal);
        }

        private static PrivacyDecision ParsePrivacyAttribute(CustomAttribute attr, SignatureTypeNameProvider provider)
        {
            try
            {
                var value = attr.DecodeValue(provider);
                var level = PrivacyLevel.Public;
                var reason = string.Empty;

                if (value.FixedArguments.Length > 0)
                {
                    var rawLevel = value.FixedArguments[0].Value;
                    if (rawLevel != null)
                    {
                        level = CoerceLevel(rawLevel);
                    }
                }
                if (value.FixedArguments.Length > 1)
                {
                    var fixedReason = value.FixedArguments[1].Value;
                    if (fixedReason != null)
                    {
                        reason = fixedReason.ToString() ?? string.Empty;
                    }
                }

                for (var i = 0; i < value.NamedArguments.Length; i++)
                {
                    var named = value.NamedArguments[i];
                    if (string.Equals(named.Name, "Level", StringComparison.Ordinal))
                    {
                        if (named.Value != null)
                        {
                            level = CoerceLevel(named.Value);
                        }
                    }
                    else if (string.Equals(named.Name, "Reason", StringComparison.Ordinal))
                    {
                        reason = named.Value == null ? reason : (named.Value.ToString() ?? reason);
                    }
                }

                return new PrivacyDecision { Level = level, Reason = reason ?? string.Empty };
            }
            catch
            {
                return new PrivacyDecision { Level = PrivacyLevel.Public, Reason = string.Empty };
            }
        }

        private static PrivacyLevel CoerceLevel(object raw)
        {
            if (raw == null)
            {
                return PrivacyLevel.Public;
            }

            if (raw is int i)
            {
                if (i <= 0) return PrivacyLevel.Public;
                if (i == 1) return PrivacyLevel.Obfuscated;
                return PrivacyLevel.Private;
            }

            if (raw is ushort us)
            {
                return CoerceLevel((int)us);
            }

            if (raw is byte b)
            {
                return CoerceLevel((int)b);
            }

            if (raw is string s)
            {
                if (string.Equals(s, "Private", StringComparison.OrdinalIgnoreCase)) return PrivacyLevel.Private;
                if (string.Equals(s, "Obfuscated", StringComparison.OrdinalIgnoreCase)) return PrivacyLevel.Obfuscated;
            }

            return PrivacyLevel.Public;
        }

        private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attr)
        {
            var ctor = attr.Constructor;
            if (ctor.Kind == HandleKind.MemberReference)
            {
                var mr = reader.GetMemberReference((MemberReferenceHandle)ctor);
                return GetTypeName(reader, mr.Parent);
            }

            if (ctor.Kind == HandleKind.MethodDefinition)
            {
                var md = reader.GetMethodDefinition((MethodDefinitionHandle)ctor);
                return GetTypeName(reader, md.GetDeclaringType());
            }

            return string.Empty;
        }

        private static string GetTypeName(MetadataReader reader, EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeReference)
            {
                var tr = reader.GetTypeReference((TypeReferenceHandle)handle);
                return JoinNamespace(reader.GetString(tr.Namespace), reader.GetString(tr.Name));
            }

            if (handle.Kind == HandleKind.TypeDefinition)
            {
                var td = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return JoinNamespace(reader.GetString(td.Namespace), reader.GetString(td.Name));
            }

            return string.Empty;
        }

        private static string JoinNamespace(string ns, string name)
        {
            if (string.IsNullOrEmpty(ns))
            {
                return name ?? string.Empty;
            }

            return ns + "." + (name ?? string.Empty);
        }
    }
}
