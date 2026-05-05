using System.Collections.Generic;
using HarmonyLib;
using ShelteredAPI.Scenarios.Application.Selection;
using UnityEngine;

namespace ShelteredAPI.Saves.Paging
{
    internal static class SlotPagingScopeResolver
    {
        private static readonly SlotPagingScope StandardScope =
            new SlotPagingScope("Standard", SaveManager.SaveType.Slot1, 4, null);
        private static readonly Dictionary<SlotSelectionPanel, SlotPagingScope> RememberedScenarioScopes =
            new Dictionary<SlotSelectionPanel, SlotPagingScope>();

        public static SlotPagingScope Resolve(SlotSelectionPanel panel)
        {
            if (panel == null)
                return StandardScope;

            ScenarioSelectionPanel scenarioPanel = FindActiveScenarioPanel(panel);
            if (scenarioPanel == null)
                return ResolveRememberedOrStandard(panel);

            int selectedScenario = ReadSelectedScenario(scenarioPanel);
            SlotPagingScope selectedScope = CreateScenarioScope(selectedScenario);
            if (selectedScope != null)
            {
                RememberedScenarioScopes[panel] = selectedScope;
                return selectedScope;
            }

            return ResolveRememberedOrStandard(panel);
        }

        private static SlotPagingScope ResolveRememberedOrStandard(SlotSelectionPanel panel)
        {
            SlotPagingScope remembered;
            return panel != null && RememberedScenarioScopes.TryGetValue(panel, out remembered) && remembered != null
                ? remembered
                : StandardScope;
        }

        public static void RememberScenarioSelection(SlotSelectionPanel panel, int selectedScenario)
        {
            if (panel == null)
                return;

            SlotPagingScope scope = CreateScenarioScope(selectedScenario);
            if (scope != null)
                RememberedScenarioScopes[panel] = scope;
        }

        private static ScenarioSelectionPanel FindActiveScenarioPanel(SlotSelectionPanel panel)
        {
            ScenarioSelectionPanel[] panels = UnityEngine.Object.FindObjectsOfType<ScenarioSelectionPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                ScenarioSelectionPanel candidate = panels[i];
                if (candidate == null || candidate.selectionPanel != panel)
                    continue;

                GameObject go = candidate.gameObject;
                if (go != null && go.activeInHierarchy)
                    return candidate;
            }

            return null;
        }

        private static int ReadSelectedScenario(ScenarioSelectionPanel panel)
        {
            try { return Traverse.Create(panel).Field("m_selectedScenario").GetValue<int>(); }
            catch { return -1; }
        }

        private static SlotPagingScope CreateScenarioScope(int selectedScenario)
        {
            switch (selectedScenario)
            {
                case 0:
                    return new SlotPagingScope(
                        ScenarioSelectionIds.VanillaSurroundedStorageScenarioId,
                        SaveManager.SaveType.SlotSurrounded,
                        1,
                        "ShelterScene_Surrounded");
                case 1:
                    return new SlotPagingScope(
                        ScenarioSelectionIds.VanillaStasisStorageScenarioId,
                        SaveManager.SaveType.SlotStasis,
                        1,
                        "ShelterScene_Stasis");
                default:
                    return null;
            }
        }
    }
}
