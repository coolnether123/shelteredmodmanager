namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Stable mod-facing save facade for standard expanded saves and scenario-scoped saves.
    /// </summary>
    public static class ShelteredSaves
    {
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
    }
}
