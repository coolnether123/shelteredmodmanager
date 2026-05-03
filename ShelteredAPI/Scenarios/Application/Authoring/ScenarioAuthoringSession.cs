using System;
using ModAPI.Scenarios;
using SaveType = SaveManager.SaveType;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSession
    {
        private ScenarioAuthoringSession()
        {
        }

        public string DraftId { get; private set; }
        public string ScenarioFilePath { get; private set; }
        public string DisplayName { get; private set; }
        public string Version { get; private set; }
        public string OwnerId { get; private set; }
        public ScenarioBaseGameMode BaseMode { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public string StorageScenarioId { get; private set; }
        public string StartupSaveId { get; private set; }
        public int StartupSaveSlot { get; private set; }
        public SaveType LaunchSaveType { get; private set; }

        public static ScenarioAuthoringSession Create(
            ScenarioInfo draftInfo,
            ScenarioBaseGameMode baseMode,
            string storageScenarioId,
            string startupSaveId,
            int startupSaveSlot,
            SaveType launchSaveType)
        {
            if (draftInfo == null)
                throw new ArgumentNullException("draftInfo");

            return new ScenarioAuthoringSession
            {
                DraftId = draftInfo.Id,
                ScenarioFilePath = draftInfo.FilePath,
                DisplayName = draftInfo.DisplayName,
                Version = string.IsNullOrEmpty(draftInfo.Version) ? "0.1.0" : draftInfo.Version,
                OwnerId = draftInfo.OwnerModId,
                BaseMode = baseMode,
                CreatedUtc = DateTime.UtcNow,
                StorageScenarioId = storageScenarioId,
                StartupSaveId = startupSaveId,
                StartupSaveSlot = startupSaveSlot,
                LaunchSaveType = launchSaveType
            };
        }
    }
}
