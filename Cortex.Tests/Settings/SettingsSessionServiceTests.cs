using Cortex.Core.Models;
using Cortex.Services.Settings;
using Xunit;

namespace Cortex.Tests.Settings
{
    public sealed class SettingsSessionServiceTests
    {
        [Fact]
        public void Restore_LoadsPersistedSessionValues()
        {
            var sessionState = new SettingsSessionState();
            float navigationScrollY;
            float contentScrollY;

            new SettingsSessionService().Restore(
                sessionState,
                new CortexSettings
                {
                    SettingsSearchQuery = "theme",
                    SettingsShowModifiedOnly = true,
                    SettingsActiveSectionId = "settings.themes",
                    SettingsNavigationScrollY = 14f,
                    SettingsContentScrollY = 28f
                },
                "settings.default",
                out navigationScrollY,
                out contentScrollY);

            Assert.Equal("theme", sessionState.SearchQuery);
            Assert.Equal("theme", sessionState.AppliedSearchQuery);
            Assert.True(sessionState.ShowModifiedOnly);
            Assert.Equal("settings.themes", sessionState.ActiveSectionId);
            Assert.Equal(14f, navigationScrollY);
            Assert.Equal(28f, contentScrollY);
        }

        [Fact]
        public void HandleSearchQueryChanged_RequestsFirstVisibleSection()
        {
            var service = new SettingsSessionService();
            var searchService = new SettingsSearchService();
            var sessionState = new SettingsSessionState
            {
                ActiveSectionId = "section.hidden",
                SearchQuery = "theme"
            };
            var document = new SettingsDocumentModel();
            var group = new SettingsNavigationGroupModel("general", "General", 0);
            var hidden = new SettingsSectionModel("section.hidden", "general", "General", "Shell", "Hidden", "Hidden", "shell hidden", 0, SettingsSectionKind.Actions, string.Empty);
            var visible = new SettingsSectionModel("section.visible", "general", "General", "Appearance", "Themes", "Themes", "theme appearance", 10, SettingsSectionKind.Themes, string.Empty);
            group.Sections.Add(hidden);
            group.Sections.Add(visible);
            document.Groups.Add(group);
            document.Sections.Add(hidden);
            document.Sections.Add(visible);

            service.CommitSearchQuery(sessionState);
            service.HandleSearchQueryChanged(
                sessionState,
                searchService,
                document,
                delegate(SettingsSectionModel section) { return 0; });

            Assert.Equal("section.visible", sessionState.ActiveSectionId);
            Assert.Equal("section.visible", sessionState.PendingSectionJumpId);
        }
    }
}
