using System;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Settings;
using Xunit;

namespace Cortex.Tests.Settings
{
    public sealed class SettingsDocumentBuilderTests
    {
        [Fact]
        public void BuildDocument_CreatesWorkspaceAndContributedScopeSections()
        {
            var snapshot = new WorkbenchPresentationSnapshot();
            snapshot.Settings.Add(new SettingContribution
            {
                SettingId = "workspace.custom",
                Scope = "Workspace",
                ValueKind = SettingValueKind.String
            });
            snapshot.Settings.Add(new SettingContribution
            {
                SettingId = "logs.follow",
                Scope = "Logs",
                ValueKind = SettingValueKind.Boolean
            });
            snapshot.Settings.Add(new SettingContribution
            {
                SettingId = nameof(CortexSettings.ThemeId),
                Scope = "Appearance",
                ValueKind = SettingValueKind.String
            });

            var document = new SettingsDocumentBuilder().BuildDocument(snapshot);

            Assert.Contains(document.Sections, section => section.SectionKind == SettingsSectionKind.WorkspaceSettingsContributions);
            Assert.Contains(document.Sections, section => section.SectionKind == SettingsSectionKind.ContributionScope && string.Equals(section.ContributionScope, "Logs", StringComparison.Ordinal));
            Assert.DoesNotContain(document.Sections, section => section.SectionKind == SettingsSectionKind.ContributionScope && string.Equals(section.ContributionScope, "Appearance", StringComparison.Ordinal));
        }

        [Fact]
        public void SearchService_ResolvesFirstVisibleSectionWhenActiveSectionIsFilteredOut()
        {
            var document = new SettingsDocumentModel();
            var group = new SettingsNavigationGroupModel("general", "General", 0);
            var hidden = new SettingsSectionModel("section.hidden", "general", "General", "Shell", "Hidden", "Hidden", "shell hidden", 0, SettingsSectionKind.Actions, string.Empty);
            var visible = new SettingsSectionModel("section.visible", "general", "General", "Appearance", "Themes", "Themes", "theme appearance", 10, SettingsSectionKind.Themes, string.Empty);
            group.Sections.Add(hidden);
            group.Sections.Add(visible);
            document.Groups.Add(group);
            document.Sections.Add(hidden);
            document.Sections.Add(visible);

            var renderSectionId = new SettingsSearchService().ResolveRenderActiveSectionId(
                document,
                "section.hidden",
                "theme",
                false,
                delegate(SettingsSectionModel section) { return 0; });

            Assert.Equal("section.visible", renderSectionId);
        }
    }
}
