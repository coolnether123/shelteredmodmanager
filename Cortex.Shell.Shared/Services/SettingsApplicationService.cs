using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class SettingsApplicationService
    {
        public ShellSettings Apply(
            SettingsDraftService draftService,
            SettingsDraftState draftState,
            SettingsSessionState sessionState,
            WorkbenchCatalogSnapshot catalog,
            ShellSettings settings)
        {
            var effectiveSettings = settings ?? new ShellSettings();
            if (draftService != null)
            {
                draftService.Apply(draftState, catalog, effectiveSettings);
            }

            if (sessionState != null)
            {
                effectiveSettings.SettingsActiveSectionId = sessionState.ActiveSectionId ?? string.Empty;
                effectiveSettings.SettingsSearchQuery = sessionState.AppliedSearchQuery ?? string.Empty;
                effectiveSettings.SettingsShowModifiedOnly = sessionState.ShowModifiedOnly;
            }

            return effectiveSettings;
        }
    }
}
