using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsDocumentBuilder
    {
        private const string SourceSetupSectionId = "settings.sourceSetup";
        private const string WorkspaceOverviewSectionId = "settings.sourceSetup.overview";
        private const string ThemesSectionId = "settings.themes";
        private const string OnboardingSectionId = "settings.onboarding";
        private const string KeybindingsSectionId = "settings.keybindings";
        private const string EditorsSectionId = "settings.editors";
        private const string ActionsSectionId = "settings.actions";

        public SettingsDocumentModel BuildDocument(WorkbenchPresentationSnapshot snapshot)
        {
            var document = new SettingsDocumentModel();
            document.Sections.Add(CreateSection(
                WorkspaceOverviewSectionId,
                "workspace",
                "Workspace",
                "Workspace",
                "Workspace Overview",
                "Current workspace roots, linked project counts, and Cortex discovery health.",
                "workspace overview roots project discovery source runtime reference assembly",
                0,
                SettingsSectionKind.WorkspaceOverview));
            document.Sections.Add(CreateSection(
                SourceSetupSectionId,
                "workspace",
                "Workspace",
                "Workspace",
                "Workspace Paths",
                "Configure the active workspace root, runtime content root, and reference assembly paths.",
                "workspace paths root runtime content references assembly source cache",
                10,
                SettingsSectionKind.WorkspacePaths));
            if (HasVisibleContributionsForScope(snapshot, "Workspace"))
            {
                document.Sections.Add(CreateSection(
                    "settings.workspace.contributions",
                    "workspace",
                    "Workspace",
                    "Workspace",
                    "Workspace Settings",
                    "Additional workspace and project discovery settings contributed by Cortex services.",
                    BuildScopeSearchText(snapshot, "Workspace"),
                    20,
                    SettingsSectionKind.WorkspaceSettingsContributions));
            }

            document.Sections.Add(CreateSection(
                "settings.workspace.modLinks",
                "workspace",
                "Workspace",
                "Workspace",
                "Loaded Mod Source Links",
                "Map loaded runtime content back to editable source roots.",
                "workspace loaded mod links runtime content source roots mapping",
                30,
                SettingsSectionKind.WorkspaceModLinks));
            document.Sections.Add(CreateSection(
                "settings.workspace.currentPaths",
                "workspace",
                "Workspace",
                "Workspace",
                "Current Search Paths",
                "Review the effective search roots Cortex uses for source and decompiler navigation.",
                "workspace current search paths source roots decompiler cache navigation",
                40,
                SettingsSectionKind.WorkspaceCurrentPaths));

            var contributedSections = new List<SettingsSectionModel>();
            var seenScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
                    {
                        continue;
                    }

                    var scope = GetContributionScope(contribution);
                    if (!seenScopes.Add(scope) || !HasVisibleContributionsForScope(snapshot, scope))
                    {
                        continue;
                    }

                    contributedSections.Add(CreateSection(
                        "settings.scope." + scope.Replace(' ', '.').ToLowerInvariant(),
                        ClassifySectionGroupId(scope),
                        ClassifySectionGroupTitle(scope),
                        scope,
                        scope,
                        BuildScopeDescription(scope),
                        BuildScopeSearchText(snapshot, scope),
                        GetGroupSortOrder(ClassifySectionGroupId(scope)),
                        SettingsSectionKind.ContributionScope,
                        scope));
                }
            }

            contributedSections.Sort(delegate(SettingsSectionModel left, SettingsSectionModel right)
            {
                var order = GetGroupSortOrder(left != null ? left.GroupId : string.Empty)
                    .CompareTo(GetGroupSortOrder(right != null ? right.GroupId : string.Empty));
                if (order != 0)
                {
                    return order;
                }

                order = (left != null ? left.SortOrder : int.MaxValue)
                    .CompareTo(right != null ? right.SortOrder : int.MaxValue);
                if (order != 0)
                {
                    return order;
                }

                return string.Compare(
                    left != null ? left.Title : string.Empty,
                    right != null ? right.Title : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < contributedSections.Count; i++)
            {
                document.Sections.Add(contributedSections[i]);
            }

            document.Sections.Add(CreateSection(
                OnboardingSectionId,
                "shell",
                "Shell",
                "Onboarding",
                "Onboarding",
                "Review the active onboarding defaults or reopen onboarding to change your starting profile, layout, or theme.",
                "onboarding profile layout theme defaults setup",
                80,
                SettingsSectionKind.Onboarding));
            document.Sections.Add(CreateSection(
                ThemesSectionId,
                "appearance",
                "Appearance",
                "Appearance",
                "Themes",
                "Select the active shell theme and review the registered color palettes.",
                "themes appearance colors accent surface shell",
                90,
                SettingsSectionKind.Themes));
            document.Sections.Add(CreateSection(
                KeybindingsSectionId,
                "editor",
                "Editor",
                "Editing",
                "Editor Keybindings",
                "Configure editor command bindings and undo history limits.",
                "editor keybindings shortcuts undo history input bindings",
                100,
                SettingsSectionKind.Keybindings));
            document.Sections.Add(CreateSection(
                EditorsSectionId,
                "editor",
                "Editor",
                "Editing",
                "Registered Editors",
                "Inspect registered editor contributions and supported content types.",
                "editor registered editors content types extensions",
                110,
                SettingsSectionKind.Editors));
            document.Sections.Add(CreateSection(
                ActionsSectionId,
                "shell",
                "Shell",
                "Shell",
                "Window Actions",
                "Quick shell actions for returning to the editor or revealing detached windows.",
                "actions shell windows logs editor",
                120,
                SettingsSectionKind.Actions));

            BuildNavigationGroups(document);
            return document;
        }

        private static SettingsSectionModel CreateSection(
            string sectionId,
            string groupId,
            string groupTitle,
            string scope,
            string title,
            string description,
            string searchText,
            int sortOrder,
            SettingsSectionKind sectionKind,
            string contributionScope = "")
        {
            return new SettingsSectionModel(
                sectionId,
                groupId,
                groupTitle,
                scope,
                title,
                description,
                searchText,
                sortOrder,
                sectionKind,
                contributionScope);
        }

        private static void BuildNavigationGroups(SettingsDocumentModel document)
        {
            var groupsById = new Dictionary<string, SettingsNavigationGroupModel>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
                if (section == null)
                {
                    continue;
                }

                SettingsNavigationGroupModel group;
                if (!groupsById.TryGetValue(section.GroupId, out group))
                {
                    group = new SettingsNavigationGroupModel(section.GroupId, section.GroupTitle, GetGroupSortOrder(section.GroupId));
                    groupsById[section.GroupId] = group;
                    document.Groups.Add(group);
                }

                group.Sections.Add(section);
            }

            document.Groups.Sort(delegate(SettingsNavigationGroupModel left, SettingsNavigationGroupModel right)
            {
                var order = (left != null ? left.SortOrder : int.MaxValue)
                    .CompareTo(right != null ? right.SortOrder : int.MaxValue);
                if (order != 0)
                {
                    return order;
                }

                return string.Compare(
                    left != null ? left.Title : string.Empty,
                    right != null ? right.Title : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < document.Groups.Count; i++)
            {
                document.Groups[i].Sections.Sort(delegate(SettingsSectionModel left, SettingsSectionModel right)
                {
                    var order = (left != null ? left.SortOrder : int.MaxValue)
                        .CompareTo(right != null ? right.SortOrder : int.MaxValue);
                    if (order != 0)
                    {
                        return order;
                    }

                    return string.Compare(
                        left != null ? left.Title : string.Empty,
                        right != null ? right.Title : string.Empty,
                        StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        private static bool HasVisibleContributionsForScope(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            if (snapshot == null || string.IsNullOrEmpty(scope))
            {
                return false;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                if (ShouldRenderContribution(scope, snapshot.Settings[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldRenderContribution(string scope, SettingContribution contribution)
        {
            return contribution != null &&
                !string.IsNullOrEmpty(contribution.SettingId) &&
                !IsThemeSetting(contribution) &&
                string.Equals(GetContributionScope(contribution), scope, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThemeSetting(SettingContribution contribution)
        {
            return contribution != null && string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContributionScope(SettingContribution contribution)
        {
            return string.IsNullOrEmpty(contribution != null ? contribution.Scope : string.Empty)
                ? "General"
                : contribution.Scope;
        }

        private static string BuildScopeSearchText(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            var text = scope + " " + BuildScopeDescription(scope) + " ";
            var sectionContribution = FindSettingSectionContribution(snapshot, scope);
            if (sectionContribution != null)
            {
                text +=
                    (sectionContribution.GroupTitle ?? string.Empty) + " " +
                    (sectionContribution.SectionTitle ?? string.Empty) + " " +
                    (sectionContribution.Description ?? string.Empty) + " " +
                    BuildKeywordsText(sectionContribution.Keywords) + " ";
            }

            if (snapshot == null)
            {
                return text;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (!ShouldRenderContribution(scope, contribution))
                {
                    continue;
                }

                text += BuildContributionSearchText(contribution) + " ";
            }

            return text;
        }

        private static string BuildContributionSearchText(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            return
                (contribution.DisplayName ?? string.Empty) + " " +
                (contribution.Description ?? string.Empty) + " " +
                (contribution.HelpText ?? string.Empty) + " " +
                (contribution.PlaceholderText ?? string.Empty) + " " +
                (contribution.Scope ?? string.Empty) + " " +
                (contribution.SettingId ?? string.Empty) + " " +
                BuildKeywordsText(contribution.Keywords) + " " +
                BuildChoiceSearchText(contribution.Options);
        }

        private static string BuildChoiceSearchText(SettingChoiceOption[] options)
        {
            if (options == null || options.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                if (option == null)
                {
                    continue;
                }

                text +=
                    (option.Value ?? string.Empty) + " " +
                    (option.DisplayName ?? string.Empty) + " " +
                    (option.Description ?? string.Empty) + " ";
            }

            return text;
        }

        private static string BuildKeywordsText(string[] keywords)
        {
            return keywords == null || keywords.Length == 0
                ? string.Empty
                : string.Join(" ", keywords);
        }

        private static SettingSectionContribution FindSettingSectionContribution(WorkbenchPresentationSnapshot snapshot, string scope)
        {
            if (snapshot == null || string.IsNullOrEmpty(scope) || snapshot.SettingSections == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.SettingSections.Count; i++)
            {
                var contribution = snapshot.SettingSections[i];
                if (contribution != null && string.Equals(contribution.Scope, scope, StringComparison.OrdinalIgnoreCase))
                {
                    return contribution;
                }
            }

            return null;
        }

        private static string BuildScopeDescription(string scope)
        {
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "Additional workspace and project discovery settings.";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase))
            {
                return "Live log feed settings and file tail behavior.";
            }
            if (string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase))
            {
                return "Decompiler executable and cache behavior.";
            }
            if (string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase))
            {
                return "External language worker configuration and request limits.";
            }
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "AI completion settings contributed under the " + scope + " scope.";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "Editing permissions and write-back behavior.";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase))
            {
                return "Workbench layout and shell window persistence settings.";
            }
            if (string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "Build defaults and execution limits.";
            }

            return "Settings contributed under the " + scope + " scope.";
        }

        private static string ClassifySectionGroupId(string scope)
        {
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "ai";
            }
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "workspace";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "tooling";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "editor";
            }
            if (string.Equals(scope, "Appearance", StringComparison.OrdinalIgnoreCase))
            {
                return "appearance";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "General", StringComparison.OrdinalIgnoreCase))
            {
                return "shell";
            }

            var scopePrefix = GetScopePrefix(scope);
            return string.IsNullOrEmpty(scopePrefix) ? "extensions" : "ext." + scopePrefix.ToLowerInvariant();
        }

        private static string ClassifySectionGroupTitle(string scope)
        {
            if (scope.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return "AI";
            }
            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase))
            {
                return "Workspace";
            }
            if (string.Equals(scope, "Logs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Language Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return "Tooling";
            }
            if (string.Equals(scope, "Editing", StringComparison.OrdinalIgnoreCase))
            {
                return "Editor";
            }
            if (string.Equals(scope, "Appearance", StringComparison.OrdinalIgnoreCase))
            {
                return "Appearance";
            }
            if (string.Equals(scope, "Layout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "General", StringComparison.OrdinalIgnoreCase))
            {
                return "Shell";
            }

            var scopePrefix = GetScopePrefix(scope);
            return string.IsNullOrEmpty(scopePrefix) ? "Extensions" : scopePrefix;
        }

        private static string GetScopePrefix(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return string.Empty;
            }

            var separatorIndex = scope.IndexOf(" - ", StringComparison.Ordinal);
            return separatorIndex > 0 ? scope.Substring(0, separatorIndex) : scope;
        }

        internal static int GetGroupSortOrder(string groupId)
        {
            switch (groupId)
            {
                case "workspace": return 0;
                case "tooling": return 10;
                case "ai": return 20;
                case "appearance": return 30;
                case "editor": return 40;
                case "shell": return 50;
                case "extensions": return 60;
                default:
                    return groupId != null && groupId.StartsWith("ext.", StringComparison.OrdinalIgnoreCase) ? 60 : 70;
            }
        }
    }
}
