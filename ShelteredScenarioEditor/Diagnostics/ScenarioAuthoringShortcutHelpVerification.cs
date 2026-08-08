using ShelteredAPI.Scenarios.Diagnostics;
using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Diagnostics
{
    // Contract test for the SHORTCUTHELP slice: asserts that every registered
    // keyboard shortcut carries a chord + human description, and that the help
    // overlay model is generated entirely from the registry so it can never drift
    // from a separately maintained list.
    internal static class ScenarioAuthoringShortcutHelpVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            IList<ScenarioAuthoringShortcutDescriptor> descriptors = ScenarioAuthoringShortcutCatalog.All;
            Assert(descriptors != null && descriptors.Count > 0,
                "Shortcut registry is empty.", result);

            // 1. Every registered shortcut carries a chord and a description.
            for (int i = 0; descriptors != null && i < descriptors.Count; i++)
            {
                ScenarioAuthoringShortcutDescriptor descriptor = descriptors[i];
                Assert(descriptor != null
                    && !string.IsNullOrEmpty(descriptor.KeyChord)
                    && !string.IsNullOrEmpty(descriptor.Description),
                    "A registered shortcut is missing its key chord or description.", result);
            }

            // 2. The overlay is generated from the registry: every descriptor must
            //    appear in the overlay model, under a group for its context.
            ScenarioAuthoringShortcutOverlayViewModel overlay =
                ScenarioAuthoringShortcutOverlayBuilder.Build(ScenarioAuthoringShortcutContext.WorldEditing);
            Assert(overlay != null && overlay.Groups != null && overlay.Groups.Length > 0,
                "Shortcut overlay model has no groups.", result);

            for (int i = 0; descriptors != null && i < descriptors.Count; i++)
            {
                ScenarioAuthoringShortcutDescriptor descriptor = descriptors[i];
                if (descriptor == null)
                    continue;

                Assert(OverlayContains(overlay, descriptor),
                    "Registered shortcut '" + descriptor.KeyChord + "' does not appear in the overlay model.", result);
            }

            // 3. Every overlay row traces back to a registered shortcut (no hand-added rows).
            for (int g = 0; overlay != null && overlay.Groups != null && g < overlay.Groups.Length; g++)
            {
                ScenarioAuthoringShortcutGroupViewModel group = overlay.Groups[g];
                for (int r = 0; group != null && group.Rows != null && r < group.Rows.Length; r++)
                {
                    ScenarioAuthoringShortcutRowViewModel row = group.Rows[r];
                    Assert(row != null && RegistryContains(descriptors, row),
                        "Overlay row is not backed by a registered shortcut.", result);
                }
            }

            // 4. The active context is highlighted in exactly one group.
            int activeGroups = 0;
            for (int g = 0; overlay != null && overlay.Groups != null && g < overlay.Groups.Length; g++)
                if (overlay.Groups[g] != null && overlay.Groups[g].IsActiveContext) activeGroups++;
            Assert(activeGroups == 1,
                "Shortcut overlay must highlight exactly one active context group.", result);
        }

        private static bool OverlayContains(ScenarioAuthoringShortcutOverlayViewModel overlay, ScenarioAuthoringShortcutDescriptor descriptor)
        {
            string expectedTitle = ScenarioAuthoringShortcutCatalog.GetContextTitle(descriptor.Context);
            for (int g = 0; overlay != null && overlay.Groups != null && g < overlay.Groups.Length; g++)
            {
                ScenarioAuthoringShortcutGroupViewModel group = overlay.Groups[g];
                if (group == null || !string.Equals(group.Title, expectedTitle))
                    continue;

                for (int r = 0; group.Rows != null && r < group.Rows.Length; r++)
                {
                    ScenarioAuthoringShortcutRowViewModel row = group.Rows[r];
                    if (row != null
                        && string.Equals(row.KeyChord, descriptor.KeyChord)
                        && string.Equals(row.Description, descriptor.Description))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool RegistryContains(IList<ScenarioAuthoringShortcutDescriptor> descriptors, ScenarioAuthoringShortcutRowViewModel row)
        {
            for (int i = 0; descriptors != null && i < descriptors.Count; i++)
            {
                ScenarioAuthoringShortcutDescriptor descriptor = descriptors[i];
                if (descriptor != null
                    && string.Equals(descriptor.KeyChord, row.KeyChord)
                    && string.Equals(descriptor.Description, row.Description))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition)
                result.AddError("Shortcut help contract: " + message);
        }
    }
}
