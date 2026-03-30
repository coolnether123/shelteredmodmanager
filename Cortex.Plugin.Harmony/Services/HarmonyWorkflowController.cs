using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Plugin.Harmony.Services;
using Cortex.Plugin.Harmony.Services.Editor;
using Cortex.Plugin.Harmony.Services.Generation;
using Cortex.Plugin.Harmony.Services.Presentation;
using Cortex.Plugin.Harmony.Services.Resolution;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyMethodResolver _resolver;
        private readonly Runtime.HarmonyRuntimeInspectionService _runtimeInspectionService;
        private readonly HarmonyPatchDisplayService _displayService;
        private readonly HarmonyPatchTemplateService _templateService;
        private readonly HarmonyPatchInsertionService _insertionService;
        private readonly HarmonyTemplateNavigationService _templateNavigationService;
        private readonly HarmonyMethodInspectorNavigationActionFactory _navigationActionFactory;

        public HarmonyWorkflowController(HarmonyModuleStateStore stateStore)
            : this(
                stateStore,
                new HarmonyMethodResolver(),
                new Runtime.HarmonyRuntimeInspectionService(),
                new HarmonyPatchDisplayService(),
                new HarmonyPatchTemplateService(),
                new HarmonyPatchInsertionService(stateStore),
                new HarmonyTemplateNavigationService(stateStore),
                new HarmonyMethodInspectorNavigationActionFactory())
        {
        }

        internal HarmonyWorkflowController(
            HarmonyModuleStateStore stateStore,
            HarmonyMethodResolver resolver,
            Runtime.HarmonyRuntimeInspectionService runtimeInspectionService,
            HarmonyPatchDisplayService displayService,
            HarmonyPatchTemplateService templateService,
            HarmonyPatchInsertionService insertionService,
            HarmonyTemplateNavigationService templateNavigationService,
            HarmonyMethodInspectorNavigationActionFactory navigationActionFactory)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _resolver = resolver ?? new HarmonyMethodResolver();
            _runtimeInspectionService = runtimeInspectionService ?? new Runtime.HarmonyRuntimeInspectionService();
            _displayService = displayService ?? new HarmonyPatchDisplayService();
            _templateService = templateService ?? new HarmonyPatchTemplateService();
            _insertionService = insertionService ?? new HarmonyPatchInsertionService(_stateStore);
            _templateNavigationService = templateNavigationService ?? new HarmonyTemplateNavigationService(_stateStore);
            _navigationActionFactory = navigationActionFactory ?? new HarmonyMethodInspectorNavigationActionFactory();
        }

        public HarmonyPatchDisplayService DisplayService
        {
            get { return _displayService; }
        }

        public bool IsRuntimeAvailable
        {
            get { return _runtimeInspectionService != null && _runtimeInspectionService.IsAvailable; }
        }

        public string GetUnavailableMessage()
        {
            return IsRuntimeAvailable
                ? string.Empty
                : (_runtimeInspectionService != null ? _runtimeInspectionService.UnavailableReason : "Harmony module is unavailable.");
        }

        private static void SetStatus(IWorkbenchRuntimeAccess runtimeAccess, string message)
        {
            if (runtimeAccess != null && runtimeAccess.Feedback != null)
            {
                runtimeAccess.Feedback.SetStatusMessage(message ?? string.Empty);
            }
        }

        private static string BuildTargetDisplay(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(target.QualifiedSymbolDisplay))
            {
                return target.QualifiedSymbolDisplay;
            }

            return !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : target.MetadataName ?? string.Empty;
        }

        private static string Join(string[] values)
        {
            return values != null && values.Length > 0 ? string.Join(", ", values) : "-";
        }
    }
}
