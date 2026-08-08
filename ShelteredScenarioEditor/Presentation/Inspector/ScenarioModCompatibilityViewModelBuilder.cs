using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Compatibility;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredScenarioEditor.Presentation.Inspector{
    internal sealed class ScenarioModCompatibilityViewModelBuilder
    {
        public List<ScenarioAuthoringInspectorItem> BuildItems(ScenarioModCompatibilityReport report)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Required Mods", Count(report != null ? report.RequiredMods : null)));
            items.Add(ScenarioInspectorItemFactory.Property("Optional Mods", Count(report != null ? report.OptionalMods : null)));
            items.Add(ScenarioInspectorItemFactory.Property("Missing Required", Count(report != null ? report.MissingRequiredMods : null)));
            items.Add(ScenarioInspectorItemFactory.Property("Version Mismatches", Count(report != null ? report.VersionMismatches : null)));
            if (HasModGatingFlags(report))
            {
                items.Add(ScenarioInspectorItemFactory.ActionItem(
                    ScenarioInspectorItemFactory.Action(
                        ShellUxCommand.HelpTopic(TutorialContent.TopicModGating),
                        "Resolve Mod Gating",
                        "Open guidance for required mods, version mismatches, and missing references.",
                        true,
                        false,
                        "MOD")));
            }

            AddDependencies(items, "Missing", report != null ? report.MissingRequiredMods : null);
            AddDependencies(items, "Required", report != null ? report.RequiredMods : null);
            for (int i = 0; report != null && report.UnknownReferences != null && i < report.UnknownReferences.Count && i < 6; i++)
                items.Add(ScenarioInspectorItemFactory.Property("Unknown Reference", report.UnknownReferences[i]));
            return items;
        }

        private static void AddDependencies(List<ScenarioAuthoringInspectorItem> items, string label, List<ScenarioModDependencyDefinition> dependencies)
        {
            for (int i = 0; dependencies != null && i < dependencies.Count && i < 8; i++)
            {
                ScenarioModDependencyDefinition dependency = dependencies[i];
                if (dependency != null)
                    items.Add(ScenarioInspectorItemFactory.Property(label + " " + dependency.ModId, FormatReasons(dependency)));
            }
        }

        private static string FormatReasons(ScenarioModDependencyDefinition dependency)
        {
            string reasons = string.Empty;
            for (int i = 0; dependency != null && dependency.Reasons != null && i < dependency.Reasons.Count; i++)
                reasons = reasons.Length == 0 ? dependency.Reasons[i].ToString() : reasons + ", " + dependency.Reasons[i].ToString();
            return reasons.Length == 0 ? dependency.Version : reasons;
        }

        private static string Count<T>(List<T> values)
        {
            return values != null ? values.Count.ToString() : "0";
        }

        private static bool HasModGatingFlags(ScenarioModCompatibilityReport report)
        {
            return report != null
                && ((report.MissingRequiredMods != null && report.MissingRequiredMods.Count > 0)
                    || (report.VersionMismatches != null && report.VersionMismatches.Count > 0)
                    || (report.UnknownReferences != null && report.UnknownReferences.Count > 0));
        }
    }
}
