using System;
using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class ScenarioPublishCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioPublishExportService _exportService;

        public ScenarioPublishCommandHandler(ScenarioPublishExportService exportService)
        {
            _exportService = exportService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = string.Equals(actionId, ScenarioAuthoringActionIds.ActionPublishExport, StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            ScenarioPublishExportResult result = _exportService != null ? _exportService.ExportActiveDraft(state) : null;
            message = result != null ? result.Message : "Export service is unavailable.";
            return true;
        }
    }
}
