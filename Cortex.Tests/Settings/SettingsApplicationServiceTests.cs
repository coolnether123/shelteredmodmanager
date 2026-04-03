using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Settings;
using Xunit;

namespace Cortex.Tests.Settings
{
    public sealed class SettingsApplicationServiceTests
    {
        [Fact]
        public void Apply_PersistsDraftAndSessionState()
        {
            var result = new SettingsApplicationService().Apply(
                new SettingsDraftService(),
                new SettingsDraftState
                {
                    SelectedThemeId = "theme.custom"
                },
                new SettingsSessionService(),
                new SettingsSessionState
                {
                    ActiveSectionId = "settings.themes",
                    AppliedSearchQuery = "theme",
                    ShowModifiedOnly = true
                },
                new WorkbenchPresentationSnapshot(),
                new ThemeState(),
                null,
                16f,
                32f);

            Assert.NotNull(result);
            Assert.Equal("theme.custom", result.ThemeId);
            Assert.Equal("settings.themes", result.SettingsActiveSectionId);
            Assert.Equal("theme", result.SettingsSearchQuery);
            Assert.True(result.SettingsShowModifiedOnly);
            Assert.Equal(16f, result.SettingsNavigationScrollY);
            Assert.Equal(32f, result.SettingsContentScrollY);
        }
    }
}
