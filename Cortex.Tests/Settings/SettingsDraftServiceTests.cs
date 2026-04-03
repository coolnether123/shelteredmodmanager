using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Settings;
using Xunit;

namespace Cortex.Tests.Settings
{
    public sealed class SettingsDraftServiceTests
    {
        [Fact]
        public void ApplyDraft_WritesContributionValuesAndThemeSelection()
        {
            var service = new SettingsDraftService();
            var draftState = new SettingsDraftState();
            var snapshot = new WorkbenchPresentationSnapshot();
            snapshot.Settings.Add(new SettingContribution
            {
                SettingId = nameof(CortexSettings.EnableFileSaving),
                ValueKind = SettingValueKind.Boolean
            });

            draftState.ToggleValues[nameof(CortexSettings.EnableFileSaving)] = true;
            draftState.SelectedThemeId = "theme.custom";

            var settings = new CortexSettings();
            service.ApplyDraft(draftState, snapshot, settings);

            Assert.True(settings.EnableFileSaving);
            Assert.Equal("theme.custom", settings.ThemeId);
        }

        [Fact]
        public void GetValidationResult_WarnsWhenPathDoesNotExist()
        {
            var service = new SettingsDraftService();
            var draftState = new SettingsDraftState();
            var contribution = new SettingContribution
            {
                SettingId = "workspace.root",
                ValueKind = SettingValueKind.String,
                EditorKind = SettingEditorKind.Path
            };

            draftState.TextValues[contribution.SettingId] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var result = service.GetValidationResult(draftState, contribution);

            Assert.NotNull(result);
            Assert.Equal(SettingValidationSeverity.Warning, result.Severity);
            Assert.Equal("The path does not exist on disk.", result.Message);
        }
    }
}
