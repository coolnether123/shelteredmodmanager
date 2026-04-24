using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioModCompatibilityViewModelBuilder
    {
        public List<ScenarioAuthoringInspectorItem> BuildItems(ScenarioModCompatibilityReport report)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Required Mods", Count(report != null ? report.RequiredMods : null)));
            items.Add(Property("Optional Mods", Count(report != null ? report.OptionalMods : null)));
            items.Add(Property("Missing Required", Count(report != null ? report.MissingRequiredMods : null)));
            items.Add(Property("Version Mismatches", Count(report != null ? report.VersionMismatches : null)));

            AddDependencies(items, "Missing", report != null ? report.MissingRequiredMods : null);
            AddDependencies(items, "Required", report != null ? report.RequiredMods : null);
            for (int i = 0; report != null && report.UnknownReferences != null && i < report.UnknownReferences.Count && i < 6; i++)
                items.Add(Property("Unknown Reference", report.UnknownReferences[i]));
            return items;
        }

        private static void AddDependencies(List<ScenarioAuthoringInspectorItem> items, string label, List<ScenarioModDependencyDefinition> dependencies)
        {
            for (int i = 0; dependencies != null && i < dependencies.Count && i < 8; i++)
            {
                ScenarioModDependencyDefinition dependency = dependencies[i];
                if (dependency != null)
                    items.Add(Property(label + " " + dependency.ModId, FormatReasons(dependency)));
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

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value
            };
        }
    }
}
