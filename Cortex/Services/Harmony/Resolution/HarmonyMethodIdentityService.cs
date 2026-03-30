using System;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services.Harmony.Resolution
{
    internal interface IHarmonyMethodIdentityService
    {
        bool TryResolveMethod(string assemblyPath, int metadataToken, out MethodBase method);
        HarmonyPatchInspectionRequest CreateInspectionRequest(MethodBase method, string assemblyPath, string displayName, string documentPath, string cachePath, string documentationCommentId);
        HarmonyResolvedMethodTarget CreateResolvedTarget(HarmonyPatchInspectionRequest request, MethodBase method, IProjectCatalog projectCatalog);
        CortexProjectDefinition FindProjectForDocument(IProjectCatalog projectCatalog, string documentPath, string assemblyPath);
        string BuildMethodDisplayName(MethodBase method);
        string BuildMethodSignature(MethodBase method);
    }

    internal sealed class HarmonyMethodIdentityService : IHarmonyMethodIdentityService
    {
        private readonly IRuntimeAssemblyMemberService _runtimeAssemblyMemberService;

        public HarmonyMethodIdentityService()
            : this(new RuntimeAssemblyMemberService())
        {
        }

        internal HarmonyMethodIdentityService(IRuntimeAssemblyMemberService runtimeAssemblyMemberService)
        {
            _runtimeAssemblyMemberService = runtimeAssemblyMemberService ?? new RuntimeAssemblyMemberService();
        }

        public bool TryResolveMethod(string assemblyPath, int metadataToken, out MethodBase method)
        {
            method = _runtimeAssemblyMemberService.ResolveMethod(assemblyPath, metadataToken);
            return method != null;
        }

        public HarmonyPatchInspectionRequest CreateInspectionRequest(MethodBase method, string assemblyPath, string displayName, string documentPath, string cachePath, string documentationCommentId)
        {
            return new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath ?? string.Empty,
                MetadataToken = method != null ? method.MetadataToken : 0,
                DeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty,
                MethodName = method != null ? method.Name ?? string.Empty : string.Empty,
                Signature = BuildMethodSignature(method),
                DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : BuildMethodDisplayName(method),
                DocumentPath = documentPath ?? string.Empty,
                CachePath = cachePath ?? string.Empty,
                DocumentationCommentId = documentationCommentId ?? string.Empty
            };
        }

        public HarmonyResolvedMethodTarget CreateResolvedTarget(HarmonyPatchInspectionRequest request, MethodBase method, IProjectCatalog projectCatalog)
        {
            return new HarmonyResolvedMethodTarget
            {
                InspectionRequest = request,
                Method = method,
                Project = FindProjectForRequest(projectCatalog, request, method),
                DisplayName = request != null && !string.IsNullOrEmpty(request.DisplayName)
                    ? request.DisplayName
                    : BuildMethodDisplayName(method)
            };
        }

        public CortexProjectDefinition FindProjectForDocument(IProjectCatalog projectCatalog, string documentPath, string assemblyPath)
        {
            if (projectCatalog == null)
            {
                return null;
            }

            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(documentPath) && PathStartsWith(documentPath, project.SourceRootPath))
                {
                    return project;
                }

                if (!string.IsNullOrEmpty(assemblyPath) &&
                    !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                    string.Equals(Path.GetFullPath(project.OutputAssemblyPath), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        public string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var declaringType = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty;
            return declaringType + "." + method.Name + BuildMethodSignature(method);
        }

        public string BuildMethodSignature(MethodBase method)
        {
            if (method == null)
            {
                return "()";
            }

            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append("(");
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var parameterType = parameters[i].ParameterType;
                if (parameterType != null && parameterType.IsByRef)
                {
                    builder.Append(parameters[i].IsOut ? "out " : "ref ");
                    parameterType = parameterType.GetElementType();
                }

                builder.Append(parameterType != null ? parameterType.Name : "object");
                builder.Append(" ");
                builder.Append(parameters[i].Name ?? ("arg" + i));
            }

            builder.Append(")");
            return builder.ToString();
        }

        private CortexProjectDefinition FindProjectForRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, MethodBase method)
        {
            return FindProjectForDocument(
                projectCatalog,
                request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                request != null ? request.AssemblyPath ?? string.Empty : string.Empty) ??
                FindProjectByAssembly(projectCatalog, method != null && method.DeclaringType != null && method.DeclaringType.Assembly != null
                    ? method.DeclaringType.Assembly.Location
                    : string.Empty);
        }

        private static CortexProjectDefinition FindProjectByAssembly(IProjectCatalog projectCatalog, string assemblyPath)
        {
            if (projectCatalog == null || string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null || string.IsNullOrEmpty(project.OutputAssemblyPath))
                {
                    continue;
                }

                try
                {
                    if (string.Equals(Path.GetFullPath(project.OutputAssemblyPath), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return project;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
