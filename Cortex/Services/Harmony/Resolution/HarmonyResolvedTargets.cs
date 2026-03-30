using System.Reflection;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonyResolvedMethodTarget
    {
        public HarmonyPatchInspectionRequest InspectionRequest;
        public MethodBase Method;
        public CortexProjectDefinition Project;
        public string DisplayName = string.Empty;
    }

    internal sealed class HarmonyResolvedTypeTarget
    {
        public string AssemblyPath = string.Empty;
        public System.Type DeclaringType;
        public CortexProjectDefinition Project;
        public string DisplayName = string.Empty;
    }

    internal sealed class HarmonyPatchAttributeBinding
    {
        public string TypeName = string.Empty;
        public string MethodName = string.Empty;
        public string[] ParameterTypeNames = new string[0];
    }

    internal sealed class HarmonySourcePatchContext
    {
        public string PatchKind = string.Empty;
        public string SourceMethodName = string.Empty;
        public string ResolutionSource = string.Empty;
        public HarmonyResolvedMethodTarget Target;
    }
}
