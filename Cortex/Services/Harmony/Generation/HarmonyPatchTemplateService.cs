using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Cortex.Core.Models;

using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Generation
{
    internal sealed class HarmonyPatchTemplateService
    {
        public HarmonyPatchGenerationPreview BuildSnippet(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            var preview = new HarmonyPatchGenerationPreview
            {
                SnippetText = string.Empty,
                PreviewText = string.Empty,
                Placeholders = new GeneratedTemplatePlaceholder[0],
                StatusMessage = "Harmony generation preview is not available.",
                CanApply = false
            };

            var method = resolvedTarget != null ? resolvedTarget.Method : null;
            if (method == null || request == null)
            {
                preview.StatusMessage = "Select a method before generating a Harmony patch.";
                return preview;
            }

            var genericMethod = method as MethodInfo;
            if ((method.DeclaringType != null && method.DeclaringType.ContainsGenericParameters) ||
                (genericMethod != null && genericMethod.ContainsGenericParameters))
            {
                preview.StatusMessage = "Automatic Harmony stub generation does not yet support open generic method signatures safely.";
                return preview;
            }

            var builder = new StringBuilder();
            var placeholders = new List<GeneratedTemplatePlaceholder>();
            var methodInfo = method as MethodInfo;
            var hasReturnValue = methodInfo != null && methodInfo.ReturnType != typeof(void);

            builder.AppendLine("using System;");
            builder.AppendLine("using HarmonyLib;");
            builder.AppendLine();
            builder.Append("namespace ");
            builder.Append(string.IsNullOrEmpty(request.NamespaceName) ? "GeneratedHarmonyPatches" : request.NamespaceName);
            builder.AppendLine();
            builder.AppendLine("{");
            if (!string.IsNullOrEmpty(request.HarmonyIdMetadata))
            {
                builder.Append("    // Harmony ID: ");
                builder.Append(request.HarmonyIdMetadata);
                builder.AppendLine();
            }
            builder.Append("    [HarmonyPatch(typeof(");
            builder.Append(BuildTypeReference(method.DeclaringType, false));
            builder.Append("), ");
            if (method.IsConstructor)
            {
                builder.Append("MethodType.Constructor");
            }
            else
            {
                builder.Append("\"");
                builder.Append(method.Name);
                builder.Append("\"");
            }

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                builder.Append(", new Type[] { ");
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append("typeof(");
                    builder.Append(BuildTypeReference(parameters[i].ParameterType, true));
                    builder.Append(")");
                }
                builder.Append(" }");
            }

            builder.AppendLine(")]");
            builder.Append("    internal static class ");
            builder.Append(string.IsNullOrEmpty(request.PatchClassName) ? "GeneratedPatch" : request.PatchClassName);
            builder.AppendLine();
            builder.AppendLine("    {");
            builder.Append("        [");
            builder.Append(request.GenerationKind == HarmonyPatchGenerationKind.Prefix ? "HarmonyPrefix" : "HarmonyPostfix");
            builder.AppendLine("]");
            builder.Append("        private static ");
            builder.Append(request.GenerationKind == HarmonyPatchGenerationKind.Prefix && request.UseSkipOriginalPattern ? "bool" : "void");
            builder.Append(" ");
            builder.Append(string.IsNullOrEmpty(request.PatchMethodName)
                ? (request.GenerationKind == HarmonyPatchGenerationKind.Prefix ? "Prefix" : "Postfix")
                : request.PatchMethodName);
            builder.Append("(");
            AppendPatchParameters(builder, placeholders, method, request, hasReturnValue);
            builder.AppendLine(")");
            builder.AppendLine("        {");

            AppendPlaceholderLine(
                builder,
                placeholders,
                "body",
                "            // TODO: implement " + (request.GenerationKind == HarmonyPatchGenerationKind.Prefix ? "prefix" : "postfix") + " logic",
                "Patch logic");

            if (request.GenerationKind == HarmonyPatchGenerationKind.Prefix && request.UseSkipOriginalPattern)
            {
                builder.AppendLine("            return true;");
            }

            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            preview.SnippetText = builder.ToString();
            preview.PreviewText = preview.SnippetText;
            preview.Placeholders = placeholders.ToArray();
            preview.StatusMessage = "Generated " + request.GenerationKind + " patch preview for " + (resolvedTarget.DisplayName ?? method.Name) + ".";
            preview.CanApply = true;
            return preview;
        }

        internal static string BuildTypeReference(Type type, bool stripByRef)
        {
            if (type == null)
            {
                return "object";
            }

            if (stripByRef && type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type == typeof(void))
            {
                return "void";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(double))
            {
                return "double";
            }

            if (type == typeof(object))
            {
                return "object";
            }

            if (type == typeof(byte))
            {
                return "byte";
            }

            if (type == typeof(char))
            {
                return "char";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(short))
            {
                return "short";
            }

            if (type == typeof(uint))
            {
                return "uint";
            }

            if (type == typeof(ulong))
            {
                return "ulong";
            }

            if (type == typeof(ushort))
            {
                return "ushort";
            }

            if (type == typeof(decimal))
            {
                return "decimal";
            }

            if (type.IsArray)
            {
                return BuildTypeReference(type.GetElementType(), false) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericDefinition = type.GetGenericTypeDefinition();
                var fullName = genericDefinition.FullName ?? genericDefinition.Name ?? "object";
                var tickIndex = fullName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    fullName = fullName.Substring(0, tickIndex);
                }

                var arguments = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append("global::");
                builder.Append(fullName.Replace('+', '.'));
                builder.Append("<");
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(BuildTypeReference(arguments[i], false));
                }
                builder.Append(">");
                return builder.ToString();
            }

            return "global::" + ((type.FullName ?? type.Name ?? "object").Replace('+', '.'));
        }

        private static void AppendPatchParameters(StringBuilder builder, List<GeneratedTemplatePlaceholder> placeholders, MethodBase targetMethod, HarmonyPatchGenerationRequest request, bool hasReturnValue)
        {
            var first = true;
            var targetParameters = targetMethod.GetParameters();

            if (request.IncludeInstanceParameter && targetMethod.DeclaringType != null && !targetMethod.IsStatic && !targetMethod.IsConstructor)
            {
                AppendParameter(builder, ref first, BuildTypeReference(targetMethod.DeclaringType, false), "__instance");
            }

            if (request.IncludeArgumentParameters && targetParameters.Length > 0)
            {
                for (var i = 0; i < targetParameters.Length; i++)
                {
                    var parameter = targetParameters[i];
                    var type = parameter.ParameterType;
                    var modifier = string.Empty;
                    if (type.IsByRef)
                    {
                        modifier = parameter.IsOut ? "out " : "ref ";
                        type = type.GetElementType();
                    }

                    AppendParameter(builder, ref first, modifier + BuildTypeReference(type, false), parameter.Name ?? ("arg" + i));
                }
            }

            if (request.IncludeStateParameter)
            {
                AppendRawParameter(builder, ref first, request.GenerationKind == HarmonyPatchGenerationKind.Prefix ? "out " : string.Empty, placeholders);
            }

            if (request.IncludeResultParameter && hasReturnValue)
            {
                var methodInfo = targetMethod as MethodInfo;
                AppendParameter(builder, ref first, "ref " + BuildTypeReference(methodInfo != null ? methodInfo.ReturnType : typeof(object), false), "__result");
            }
        }

        private static void AppendRawParameter(StringBuilder builder, ref bool first, string modifier, List<GeneratedTemplatePlaceholder> placeholders)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(modifier);
            var placeholderStart = builder.Length;
            builder.Append("object");
            placeholders.Add(new GeneratedTemplatePlaceholder
            {
                PlaceholderId = "stateType",
                Start = placeholderStart,
                Length = "object".Length,
                DefaultText = "object",
                Description = "__state type"
            });
            builder.Append(" __state");
            first = false;
        }

        private static void AppendPlaceholderLine(StringBuilder builder, List<GeneratedTemplatePlaceholder> placeholders, string placeholderId, string defaultText, string description)
        {
            var start = builder.Length;
            builder.AppendLine(defaultText);
            placeholders.Add(new GeneratedTemplatePlaceholder
            {
                PlaceholderId = placeholderId,
                Start = start,
                Length = defaultText.Length,
                DefaultText = defaultText,
                Description = description
            });
        }

        private static void AppendParameter(StringBuilder builder, ref bool first, string typeName, string name)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(typeName);
            builder.Append(" ");
            builder.Append(name);
            first = false;
        }
    }
}
