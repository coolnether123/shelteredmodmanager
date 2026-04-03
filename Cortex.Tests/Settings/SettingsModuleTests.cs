using System.Reflection;
using Cortex;
using Cortex.Core.Models;
using Cortex.Modules.Settings;
using Cortex.Presentation.Models;
using Cortex.Services.Settings;
using Xunit;

namespace Cortex.Tests.Settings
{
    public sealed class SettingsModuleTests
    {
        [Fact]
        public void ResetToDefaults_RequestsOverviewSectionAfterRestore()
        {
            var module = new SettingsModule();
            var themeState = new ThemeState();
            var state = new CortexShellState
            {
                Settings = new CortexSettings
                {
                    SettingsActiveSectionId = "settings.themes",
                    SettingsSearchQuery = "theme",
                    SettingsShowModifiedOnly = true,
                    SettingsNavigationScrollY = 14f,
                    SettingsContentScrollY = 28f
                }
            };

            module.ResetToDefaults(null, themeState, state);

            var sessionStateField = typeof(SettingsModule).GetField("_sessionState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(sessionStateField);

            var sessionState = sessionStateField.GetValue(module) as SettingsSessionState;
            Assert.NotNull(sessionState);
            Assert.Equal("settings.sourceSetup.overview", sessionState.ActiveSectionId);
            Assert.Equal("settings.sourceSetup.overview", sessionState.PendingSectionJumpId);
            Assert.Equal("settings.sourceSetup.overview", state.Settings.SettingsActiveSectionId);
            Assert.Equal(0f, state.Settings.SettingsNavigationScrollY);
            Assert.Equal(0f, state.Settings.SettingsContentScrollY);
        }
    }
}
