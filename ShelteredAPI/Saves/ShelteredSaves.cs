namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Stable mod-facing save facade for standard expanded saves and scenario-scoped saves.
    /// </summary>
    public static class ShelteredSaves
    {
        /// <summary>Returns the scenario-scoped save currently bound to the running world, if any.</summary>
        public static SaveEntry GetActiveScenarioSave()
        {
            return Runtime.SaveRuntimeState.ActiveCustomSave;
        }

        /// <summary>Returns whether a matching scenario new-game target is queued for a transport slot.</summary>
        public static bool IsScenarioNewGameQueued(
            SaveManager.SaveType saveType,
            string scenarioId,
            string saveId)
        {
            Runtime.SaveRuntimeState.Target target;
            return Runtime.SaveRuntimeState.TryGetPendingSave(saveType, out target)
                && target != null
                && string.Equals(target.ScenarioId, scenarioId, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.SaveId, saveId, System.StringComparison.Ordinal);
        }

        /// <summary>Clears the process-local active scenario save binding.</summary>
        public static void ClearActiveScenarioSession()
        {
            Runtime.SaveRuntimeState.ClearActiveCustomSession();
        }

        public static SaveEntry[] ListStandard()
        {
            return ExpandedVanillaSaves.List();
        }

        public static SaveEntry[] ListStandard(int page, int pageSize)
        {
            return ExpandedVanillaSaves.List(page, pageSize);
        }

        public static int CountStandard()
        {
            return ExpandedVanillaSaves.Count();
        }

        public static int GetMaxStandardSlot()
        {
            return ExpandedVanillaSaves.GetMaxSlot();
        }

        public static SaveEntry GetStandard(string saveId)
        {
            return ExpandedVanillaSaves.Get(saveId);
        }

        public static SaveEntry GetStandardSlot(int absoluteSlot)
        {
            return ExpandedVanillaSaves.GetBySlot(absoluteSlot);
        }

        public static SaveEntry CreateStandard(SaveCreateOptions options)
        {
            return ExpandedVanillaSaves.Create(options);
        }

        public static SaveEntry OverwriteStandard(string saveId, SaveOverwriteOptions options, byte[] xmlBytes)
        {
            return ExpandedVanillaSaves.Overwrite(saveId, options, xmlBytes);
        }

        public static bool DeleteStandard(string saveId)
        {
            return ExpandedVanillaSaves.Delete(saveId);
        }

        public static bool DeleteStandardSlot(int absoluteSlot)
        {
            return ExpandedVanillaSaves.DeleteBySlot(absoluteSlot);
        }

        public static SaveEntry[] ListScenario(string scenarioId, int page, int pageSize)
        {
            return ScenarioSaves.List(scenarioId, page, pageSize);
        }

        public static SaveEntry GetScenario(string scenarioId, string saveId)
        {
            return ScenarioSaves.Get(scenarioId, saveId);
        }

        public static SaveEntry CreateScenario(string scenarioId, SaveCreateOptions options)
        {
            return ScenarioSaves.Create(scenarioId, options);
        }

        public static SaveEntry CreateNextScenario(string scenarioId, SaveCreateOptions options)
        {
            return ScenarioSaves.CreateNext(scenarioId, options);
        }

        public static int GetNextScenarioSlot(string scenarioId)
        {
            return ScenarioSaves.GetNextAvailableSlot(scenarioId);
        }

        public static SaveEntry OverwriteScenario(string scenarioId, string saveId, SaveOverwriteOptions options, byte[] xmlBytes)
        {
            return ScenarioSaves.Overwrite(scenarioId, saveId, options, xmlBytes);
        }

        public static bool DeleteScenario(string scenarioId, string saveId)
        {
            return ScenarioSaves.Delete(scenarioId, saveId);
        }

        /// <summary>Clears a queued scenario new-game target for the specified transport slot.</summary>
        public static bool ClearQueuedScenarioNewGame(SaveManager.SaveType saveType)
        {
            return ShelteredAPI.Scenarios.Composition.ScenarioRuntimeCompositionRoot
                .Resolve<ShelteredAPI.Scenarios.Application.Selection.IScenarioSaveLibrary>()
                .ClearQueuedNewGameSave(saveType);
        }

        /// <summary>Clears a queued scenario new-game target only when its identity matches.</summary>
        public static bool ClearQueuedScenarioNewGame(
            SaveManager.SaveType saveType,
            string scenarioId,
            string saveId)
        {
            return ShelteredAPI.Scenarios.Composition.ScenarioRuntimeCompositionRoot
                .Resolve<ShelteredAPI.Scenarios.Application.Selection.IScenarioSaveLibrary>()
                .ClearQueuedNewGameSaveIfMatches(saveType, scenarioId, saveId);
        }

        /// <summary>Clears a queued scenario load target for the specified transport slot.</summary>
        public static bool ClearQueuedScenarioLoad(SaveManager.SaveType saveType)
        {
            return ShelteredAPI.Scenarios.Composition.ScenarioRuntimeCompositionRoot
                .Resolve<ShelteredAPI.Scenarios.Application.Selection.IScenarioSaveLibrary>()
                .ClearQueuedLoad(saveType);
        }

        /// <summary>Clears a queued scenario load target only when its identity matches.</summary>
        public static bool ClearQueuedScenarioLoad(
            SaveManager.SaveType saveType,
            string scenarioId,
            string saveId)
        {
            return ShelteredAPI.Scenarios.Composition.ScenarioRuntimeCompositionRoot
                .Resolve<ShelteredAPI.Scenarios.Application.Selection.IScenarioSaveLibrary>()
                .ClearQueuedLoadIfMatches(saveType, scenarioId, saveId);
        }
    }
}
