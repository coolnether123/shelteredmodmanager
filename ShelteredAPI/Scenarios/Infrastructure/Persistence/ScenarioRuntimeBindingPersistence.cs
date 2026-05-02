namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioRuntimeBindingPersistence : IScenarioRuntimeBindingPersistence
    {
        private const string SaveGroupName = "CustomScenarioBinding";
        private const string HasLastEditorTickName = "HasLastEditorSaveTick";
        private const string HasScenarioQuestInstanceIdName = "HasScenarioQuestInstanceId";

        public ScenarioRuntimeBinding Load(SaveData data)
        {
            data.GroupStart(SaveGroupName);
            ScenarioRuntimeBinding binding = new ScenarioRuntimeBinding();
            string scenarioId = string.Empty;
            string versionApplied = string.Empty;
            bool isActive = false;
            bool isConverted = false;
            int dayCreated = 0;
            int lastEditorTick = 0;
            bool hasLastEditorTick = false;
            int scenarioQuestInstanceId = -1;
            bool hasScenarioQuestInstanceId = false;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("VersionApplied", ref versionApplied);
            data.SaveLoad("IsActive", ref isActive);
            data.SaveLoad("IsConverted", ref isConverted);
            data.SaveLoad("DayCreated", ref dayCreated);
            data.SaveLoad(HasLastEditorTickName, ref hasLastEditorTick);
            data.SaveLoad("LastEditorSaveTick", ref lastEditorTick);
            data.SaveLoad(HasScenarioQuestInstanceIdName, ref hasScenarioQuestInstanceId);
            data.SaveLoad("ScenarioQuestInstanceId", ref scenarioQuestInstanceId);
            data.GroupEnd();

            if (string.IsNullOrEmpty(scenarioId))
                return null;

            binding.ScenarioId = scenarioId;
            binding.VersionApplied = versionApplied;
            binding.IsActive = isActive;
            binding.IsConvertedToNormalSave = isConverted;
            binding.DayCreated = dayCreated;
            binding.LastEditorSaveTick = hasLastEditorTick ? new int?(lastEditorTick) : null;
            binding.ScenarioQuestInstanceId = hasScenarioQuestInstanceId ? new int?(scenarioQuestInstanceId) : null;
            return binding;
        }

        public void Save(SaveData data, ScenarioRuntimeBinding binding)
        {
            data.GroupStart(SaveGroupName);
            string scenarioId = binding.ScenarioId ?? string.Empty;
            string versionApplied = binding.VersionApplied ?? string.Empty;
            bool isActive = binding.IsActive;
            bool isConverted = binding.IsConvertedToNormalSave;
            int dayCreated = binding.DayCreated;
            bool hasLastEditorTick = binding.LastEditorSaveTick.HasValue;
            int lastEditorTick = binding.LastEditorSaveTick.HasValue ? binding.LastEditorSaveTick.Value : 0;
            bool hasScenarioQuestInstanceId = binding.ScenarioQuestInstanceId.HasValue;
            int scenarioQuestInstanceId = binding.ScenarioQuestInstanceId.HasValue ? binding.ScenarioQuestInstanceId.Value : -1;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("VersionApplied", ref versionApplied);
            data.SaveLoad("IsActive", ref isActive);
            data.SaveLoad("IsConverted", ref isConverted);
            data.SaveLoad("DayCreated", ref dayCreated);
            data.SaveLoad(HasLastEditorTickName, ref hasLastEditorTick);
            data.SaveLoad("LastEditorSaveTick", ref lastEditorTick);
            data.SaveLoad(HasScenarioQuestInstanceIdName, ref hasScenarioQuestInstanceId);
            data.SaveLoad("ScenarioQuestInstanceId", ref scenarioQuestInstanceId);
            data.GroupEnd();
        }
    }
}
