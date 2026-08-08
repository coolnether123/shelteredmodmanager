using System.Collections.Generic;

namespace ShelteredScenarioEditor.Application.Authoring{
    // Single place that turns the ScenarioAuthoringShortcutCatalog registry into
    // the Keyboard Shortcuts overlay view model. Both the help content builder
    // (UI) and ScenarioAuthoringShortcutCatalogVerification (contract test)
    // consume this builder, so the overlay can never drift from the registry.
    internal static class ScenarioAuthoringShortcutOverlayBuilder
    {
        public static ScenarioAuthoringShortcutOverlayViewModel Build(ScenarioAuthoringShortcutContext activeContext)
        {
            IList<ScenarioAuthoringShortcutContext> order = ScenarioAuthoringShortcutCatalog.ContextsInDisplayOrder;
            List<ScenarioAuthoringShortcutGroupViewModel> groups = new List<ScenarioAuthoringShortcutGroupViewModel>();

            for (int i = 0; order != null && i < order.Count; i++)
            {
                ScenarioAuthoringShortcutContext context = order[i];
                List<ScenarioAuthoringShortcutRowViewModel> rows = new List<ScenarioAuthoringShortcutRowViewModel>();

                IList<ScenarioAuthoringShortcutDescriptor> descriptors = ScenarioAuthoringShortcutCatalog.All;
                for (int d = 0; descriptors != null && d < descriptors.Count; d++)
                {
                    ScenarioAuthoringShortcutDescriptor descriptor = descriptors[d];
                    if (descriptor == null || descriptor.Context != context)
                        continue;

                    rows.Add(new ScenarioAuthoringShortcutRowViewModel
                    {
                        KeyChord = descriptor.KeyChord,
                        Description = descriptor.Description
                    });
                }

                if (rows.Count == 0)
                    continue;

                groups.Add(new ScenarioAuthoringShortcutGroupViewModel
                {
                    Title = ScenarioAuthoringShortcutCatalog.GetContextTitle(context),
                    IsActiveContext = context == activeContext,
                    Rows = rows.ToArray()
                });
            }

            return new ScenarioAuthoringShortcutOverlayViewModel
            {
                ActiveContextTitle = ScenarioAuthoringShortcutCatalog.GetContextTitle(activeContext),
                Groups = groups.ToArray()
            };
        }
    }
}
