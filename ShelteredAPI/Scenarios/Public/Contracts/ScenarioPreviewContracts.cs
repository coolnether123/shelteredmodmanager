using System;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Owns one process-local runtime preview. Dispose the session to release every
    /// runtime resource created for the preview.
    /// </summary>
    public interface IScenarioPreviewSession : IDisposable
    {
        ScenarioPreviewResult StartResult { get; }
        ScenarioPreviewResult Refresh(ScenarioDefinition definition, ScenarioPreviewRefreshScope scope);
        bool RestartWorld(ScenarioWorldLaunchRequest request, out string error);
        ScenarioRuntimeSnapshot CaptureRuntimeState();
        void SetExecutionLogging(bool enabled);
        ScenarioRuntimeExecutionEntrySnapshot[] CaptureExecutionLog(int maximumEntries);
        bool TryFireRuntimeElement(string elementId, out string message);
        bool TryGetMinutesUntilNextAuthoredEvent(int maximumMinutes, out int minutes);
        void NotifyGameTimeChanged();
        bool TryPreviewRuntimeSpriteFrame(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite);
        bool TryPlayRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite[] frames,
            float[] durations,
            float speed);
        void StopRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind);
        void CaptureRuntimeObjectState(Obj_Base source, ObjectPlacement destination);
        bool IsStationObject(Obj_Base obj);
        void CaptureStationUpgradeState(Obj_Base source, ObjectPlacement destination);
        ScenarioStationUpgradeSnapshot GetStationUpgradeSnapshot(Obj_Base obj, ObjectPlacement placement);
        bool TryChangeStationObjectLevel(Obj_Base obj, ObjectPlacement placement, int delta, out string message);
        bool TryChangeStationUpgradeLevel(Obj_Base obj, ObjectPlacement placement, string pathName, int delta, out string message);
        bool TryChangeStationStat(Obj_Base obj, ObjectPlacement placement, string statName, float delta, out string message);
        bool TryClearStationStat(Obj_Base obj, ObjectPlacement placement, string statName, out string message);
    }

    [Flags]
    public enum ScenarioPreviewRefreshScope
    {
        None = 0,
        World = 1,
        SpriteSwaps = 2,
        ScenePlacements = 4,
        MapProjection = 8,
        SceneAssets = SpriteSwaps | ScenePlacements,
        All = World | SceneAssets | MapProjection
    }

    /// <summary>Value-only result returned by the runtime preview facade.</summary>
    public sealed class ScenarioPreviewResult
    {
        private readonly System.Collections.Generic.List<string> _messages =
            new System.Collections.Generic.List<string>();

        public bool Started { get; private set; }
        public int RuntimeRevision { get; private set; }
        public int FamilyChanges { get; private set; }
        public int InventoryChanges { get; private set; }
        public int BunkerChanges { get; private set; }
        public int TriggerChanges { get; private set; }
        public int ConditionChanges { get; private set; }
        public int SpriteSwapChanges { get; private set; }
        public int MapChanges { get; private set; }
        public int ScenePlacementChanges { get; internal set; }
        public string[] Messages { get { return _messages.ToArray(); } }

        internal static ScenarioPreviewResult FromApplyResult(ScenarioApplyResult source, int runtimeRevision)
        {
            ScenarioPreviewResult result = new ScenarioPreviewResult
            {
                Started = source != null,
                RuntimeRevision = runtimeRevision
            };
            if (source == null)
            {
                result.AddMessage("Preview apply returned no result.");
                return result;
            }

            result.FamilyChanges = source.FamilyChanges;
            result.InventoryChanges = source.InventoryChanges;
            result.BunkerChanges = source.BunkerChanges;
            result.TriggerChanges = source.TriggerChanges;
            result.ConditionChanges = source.ConditionChanges;
            result.SpriteSwapChanges = source.SpriteSwapChanges;
            result.MapChanges = source.MapChanges;
            string[] messages = source.Messages;
            for (int i = 0; messages != null && i < messages.Length; i++)
                result.AddMessage(messages[i]);
            return result;
        }

        internal static ScenarioPreviewResult Failed(string message)
        {
            ScenarioPreviewResult result = new ScenarioPreviewResult();
            result.AddMessage(message);
            return result;
        }

        internal static ScenarioPreviewResult Succeeded(int runtimeRevision)
        {
            return new ScenarioPreviewResult { Started = true, RuntimeRevision = runtimeRevision };
        }

        internal void AddMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _messages.Add(message);
        }
    }
}
