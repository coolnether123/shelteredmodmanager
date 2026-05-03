using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioDraftMetadataUpdate
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    internal sealed class ScenarioDraftMetadataEditService
    {
        private readonly ScenarioAuthoringDraftRepository _draftRepository;

        public ScenarioDraftMetadataEditService(ScenarioAuthoringDraftRepository draftRepository)
        {
            if (draftRepository == null) throw new ArgumentNullException("draftRepository");

            _draftRepository = draftRepository;
        }

        public bool TryUpdate(string draftId, ScenarioDraftMetadataUpdate update, out ScenarioInfo updatedInfo, out string error)
        {
            updatedInfo = null;
            error = null;

            if (string.IsNullOrEmpty(draftId))
            {
                error = "No draft was selected.";
                return false;
            }

            if (update == null)
            {
                error = "No draft details were provided.";
                return false;
            }

            string displayName = NormalizeDisplayName(update.DisplayName);
            if (string.IsNullOrEmpty(displayName))
            {
                error = "Scenario name is required.";
                return false;
            }

            return _draftRepository.TryUpdateMetadata(
                draftId,
                displayName,
                NormalizeDescription(update.Description),
                out updatedInfo,
                out error);
        }

        private static string NormalizeDisplayName(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static string NormalizeDescription(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
