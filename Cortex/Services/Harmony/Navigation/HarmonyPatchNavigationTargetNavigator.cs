using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Navigation;

namespace Cortex.Services.Harmony.Navigation
{
    internal static class HarmonyPatchNavigationTargetNavigator
    {
        public static bool TryOpen(
            ICortexNavigationService navigationService,
            CortexShellState state,
            HarmonyPatchNavigationTarget target,
            string successMessage,
            string failureMessage)
        {
            if (navigationService == null || state == null || target == null)
            {
                if (state != null)
                {
                    state.StatusMessage = failureMessage;
                }

                return false;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath))
            {
                if (!CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
                {
                    if (navigationService.OpenDocument(state, target.DocumentPath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                    {
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(target.AssemblyPath) && target.MetadataToken > 0)
            {
                if (navigationService.DecompileAndOpen(state, target.AssemblyPath, target.MetadataToken, DecompilerEntityKind.Method, false, successMessage, failureMessage))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.CachePath) && File.Exists(target.CachePath))
            {
                if (navigationService.OpenDocument(state, target.CachePath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath))
            {
                if (navigationService.OpenDocument(state, target.DocumentPath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                {
                    return true;
                }
            }

            state.StatusMessage = failureMessage;
            return false;
        }
    }
}
