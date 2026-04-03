using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsApplicationService
    {
        public CortexSettings Apply(
            SettingsDraftService draftService,
            SettingsDraftState draftState,
            SettingsSessionService sessionService,
            SettingsSessionState sessionState,
            WorkbenchPresentationSnapshot snapshot,
            ThemeState themeState,
            CortexSettings settings,
            float navigationScrollY,
            float contentScrollY)
        {
            var effectiveSettings = settings ?? new CortexSettings();
            if (draftService != null)
            {
                draftService.ApplyDraft(draftState, snapshot, effectiveSettings);
            }

            if (sessionService != null)
            {
                sessionService.Persist(sessionState, effectiveSettings, navigationScrollY, contentScrollY);
            }

            if (themeState != null)
            {
                themeState.ThemeId = effectiveSettings.ThemeId;
            }

            return effectiveSettings;
        }
    }
}
