using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Selection
{
    internal static class ScenarioLaunchSetupPolicy
    {
        public static ScenarioLaunchSetupMode GetMode(ScenarioDefinition definition)
        {
            return definition != null && definition.LaunchSetup != null
                ? definition.LaunchSetup.Mode
                : ScenarioLaunchSetupMode.FullSetup;
        }

        public static int GetValue(ScenarioDefinition definition, string categoryId, int fallback)
        {
            ScenarioDifficultyCategoryDefinition category = Find(definition, categoryId);
            if (category == null)
                return fallback;
            int maximum = categoryId == ScenarioDifficultyCategoryIds.MapSize ? 2
                : categoryId == ScenarioDifficultyCategoryIds.Fog ? 1 : 3;
            return Math.Max(0, Math.Min(maximum, category.AuthoredValue));
        }

        public static bool IsPlayerSelectable(ScenarioDefinition definition, string categoryId)
        {
            ScenarioDifficultyCategoryDefinition category = Find(definition, categoryId);
            return category == null || category.PlayerSelectable;
        }

        public static void ApplyDifficulty(ScenarioDefinition definition)
        {
            DifficultyManager.StoreMenuDifficultySettings(
                GetValue(definition, ScenarioDifficultyCategoryIds.Rain, 1),
                GetValue(definition, ScenarioDifficultyCategoryIds.Resources, 1),
                GetValue(definition, ScenarioDifficultyCategoryIds.Breach, 1),
                GetValue(definition, ScenarioDifficultyCategoryIds.Faction, 1),
                GetValue(definition, ScenarioDifficultyCategoryIds.Mood, 1),
                GetValue(definition, ScenarioDifficultyCategoryIds.MapSize, 0),
                GetValue(definition, ScenarioDifficultyCategoryIds.Fog, 0) != 0);
        }

        public static bool TryGetPendingGuidedDefinition(out ScenarioDefinition definition)
        {
            definition = null;
            try
            {
                ICustomScenarioLifecycleService lifecycle = ScenarioCompositionRoot.Resolve<ICustomScenarioLifecycleService>();
                CustomScenarioState state = lifecycle != null ? lifecycle.CurrentState : null;
                if (state == null || state.LifecycleState != CustomScenarioLifecycleState.Pending || string.IsNullOrEmpty(state.ScenarioId))
                    return false;

                string path;
                ScenarioValidationResult validation;
                IScenarioDefinitionCatalogService catalog = ScenarioCompositionRoot.Resolve<IScenarioDefinitionCatalogService>();
                return catalog != null
                    && catalog.TryLoadDefinition(state.ScenarioId, out definition, out path, out validation)
                    && GetMode(definition) == ScenarioLaunchSetupMode.Guided;
            }
            catch
            {
                definition = null;
                return false;
            }
        }

        private static ScenarioDifficultyCategoryDefinition Find(ScenarioDefinition definition, string categoryId)
        {
            if (definition == null || definition.LaunchSetup == null || definition.LaunchSetup.Categories == null)
                return null;
            for (int i = 0; i < definition.LaunchSetup.Categories.Count; i++)
            {
                ScenarioDifficultyCategoryDefinition category = definition.LaunchSetup.Categories[i];
                if (category != null && string.Equals(category.Id, categoryId, StringComparison.OrdinalIgnoreCase))
                    return category;
            }
            return null;
        }
    }
}
