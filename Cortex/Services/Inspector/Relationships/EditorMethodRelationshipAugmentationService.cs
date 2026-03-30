using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Cortex.Core.Models;
using Cortex.Services.Inspector.Identity;

namespace Cortex.Services.Inspector.Relationships
{
    internal interface IEditorMethodRelationshipAugmentationService
    {
        void AppendOutgoingRelationships(List<EditorMethodRelationshipItem> items, EditorCommandTarget target);
        void AppendIncomingRelationships(List<EditorMethodRelationshipItem> items, EditorCommandTarget target);
    }

    internal sealed class EditorMethodRelationshipAugmentationService : IEditorMethodRelationshipAugmentationService
    {
        private readonly IEditorMethodIdentityResolver _identityResolver;

        public EditorMethodRelationshipAugmentationService(IEditorMethodIdentityResolver identityResolver)
        {
            _identityResolver = identityResolver;
        }

        public void AppendOutgoingRelationships(List<EditorMethodRelationshipItem> items, EditorCommandTarget target)
        {
            MethodBase method;
            if (items == null || _identityResolver == null || !_identityResolver.TryResolve(target, out method) || method == null)
            {
                return;
            }

            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || parameter.ParameterType == null)
                {
                    continue;
                }

                EditorMethodRelationshipSet.AddDistinct(items, CreateTypeRelationship(
                    parameter.ParameterType,
                    "Parameter Type",
                    parameter.Name ?? string.Empty));
            }

            var methodInfo = method as MethodInfo;
            if (methodInfo != null && methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void))
            {
                EditorMethodRelationshipSet.AddDistinct(items, CreateTypeRelationship(methodInfo.ReturnType, "Return Type", string.Empty));
            }
        }

        public void AppendIncomingRelationships(List<EditorMethodRelationshipItem> items, EditorCommandTarget target)
        {
            MethodBase method;
            if (items == null || _identityResolver == null || !_identityResolver.TryResolve(target, out method) || method == null || method.DeclaringType == null)
            {
                return;
            }

            var methodInfo = method as MethodInfo;
            if (methodInfo != null)
            {
                var baseDefinition = methodInfo.GetBaseDefinition();
                if (baseDefinition != null && baseDefinition != methodInfo)
                {
                    EditorMethodRelationshipSet.AddDistinct(items, CreateMethodRelationship(baseDefinition, "Override Contract"));
                }
            }

            var interfaces = method.DeclaringType.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                InterfaceMapping map;
                try
                {
                    map = method.DeclaringType.GetInterfaceMap(interfaces[i]);
                }
                catch
                {
                    continue;
                }

                for (var index = 0; index < map.TargetMethods.Length; index++)
                {
                    if (map.TargetMethods[index] != method)
                    {
                        continue;
                    }

                    EditorMethodRelationshipSet.AddDistinct(items, CreateMethodRelationship(map.InterfaceMethods[index], "Implemented Contract"));
                }
            }
        }

        private static EditorMethodRelationshipItem CreateTypeRelationship(Type type, string relationship, string detail)
        {
            return new EditorMethodRelationshipItem
            {
                Title = type != null ? (type.Name ?? string.Empty) : string.Empty,
                Detail = detail ?? string.Empty,
                SymbolKind = GetTypeSymbolKind(type),
                MetadataName = type != null ? (type.Name ?? string.Empty) : string.Empty,
                ContainingTypeName = GetNormalizedTypeName(type),
                ContainingAssemblyName = type != null && type.Assembly != null ? type.Assembly.GetName().Name ?? string.Empty : string.Empty,
                DocumentationCommentId = BuildTypeDocumentationId(type),
                Relationship = relationship ?? string.Empty,
                CallCount = 1
            };
        }

        private static EditorMethodRelationshipItem CreateMethodRelationship(MethodBase method, string relationship)
        {
            var declaringType = method != null ? method.DeclaringType : null;
            return new EditorMethodRelationshipItem
            {
                Title = method != null ? BuildMethodDisplayName(method) : string.Empty,
                Detail = declaringType != null ? (declaringType.FullName ?? declaringType.Name ?? string.Empty) : string.Empty,
                SymbolKind = method != null && method.IsConstructor ? "Constructor" : "Method",
                MetadataName = method != null ? method.Name ?? string.Empty : string.Empty,
                ContainingTypeName = declaringType != null ? (declaringType.FullName ?? declaringType.Name ?? string.Empty) : string.Empty,
                ContainingAssemblyName = declaringType != null && declaringType.Assembly != null ? declaringType.Assembly.GetName().Name ?? string.Empty : string.Empty,
                DocumentationCommentId = BuildMethodDocumentationId(method),
                Relationship = relationship ?? string.Empty,
                CallCount = 1
            };
        }

        private static string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(method.Name ?? string.Empty);
            builder.Append("(");
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[i].ParameterType != null ? parameters[i].ParameterType.Name ?? string.Empty : string.Empty);
            }

            builder.Append(")");
            return builder.ToString();
        }

        private static string GetTypeSymbolKind(Type type)
        {
            if (type == null)
            {
                return "NamedType";
            }

            if (type.IsInterface)
            {
                return "Interface";
            }

            if (type.IsEnum)
            {
                return "Enum";
            }

            if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
            {
                return "Delegate";
            }

            if (type.IsValueType)
            {
                return "Struct";
            }

            return "Class";
        }

        private static string GetNormalizedTypeName(Type type)
        {
            return type != null
                ? (type.FullName ?? type.Name ?? string.Empty)
                : string.Empty;
        }

        private static string BuildTypeDocumentationId(Type type)
        {
            return type != null
                ? "T:" + GetXmlTypeName(type)
                : string.Empty;
        }

        private static string BuildMethodDocumentationId(MethodBase method)
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

            return (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.');
        }
    }
}
