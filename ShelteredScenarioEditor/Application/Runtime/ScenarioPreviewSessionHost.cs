using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using UnityEngine;

namespace ShelteredScenarioEditor.Application.Runtime
{
    /// <summary>
    /// Editor-owned lifetime boundary for the single API preview session. Editor
    /// features depend on this port instead of exposing their workflows in ShelteredAPI.
    /// </summary>
    internal sealed class ScenarioPreviewSessionHost
    {
        private IScenarioPreviewSession _current;
        private string _scenarioFilePath;

        public bool IsActive
        {
            get { return _current != null && _current.StartResult != null && _current.StartResult.Started; }
        }

        public ScenarioPreviewResult StartOrRefresh(ScenarioDefinition definition, string scenarioFilePath)
        {
            if (_current != null
                && string.Equals(_scenarioFilePath, scenarioFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return _current.Refresh(definition, ScenarioPreviewRefreshScope.World);
            }

            Close();
            _current = ShelteredScenarioRuntime.BeginPreview(definition, scenarioFilePath);
            _scenarioFilePath = scenarioFilePath;
            ScenarioPreviewResult result = _current != null
                ? _current.StartResult
                : ScenarioPreviewResultForMissingSession();
            if (result == null || !result.Started)
                Close();
            return result;
        }

        public ScenarioPreviewResult Refresh(ScenarioDefinition definition, ScenarioPreviewRefreshScope scope)
        {
            return _current != null
                ? _current.Refresh(definition, scope)
                : ScenarioPreviewResultForMissingSession();
        }

        public bool RestartWorld(ScenarioWorldLaunchRequest request, out string error)
        {
            if (_current != null)
                return _current.RestartWorld(request, out error);

            error = "No scenario preview session is active.";
            return false;
        }

        public ScenarioRuntimeSnapshot CaptureRuntimeState()
        {
            return _current != null ? _current.CaptureRuntimeState() : null;
        }

        public void SetExecutionLogging(bool enabled)
        {
            if (_current != null)
                _current.SetExecutionLogging(enabled);
        }

        public ScenarioRuntimeExecutionEntrySnapshot[] CaptureExecutionLog(int maximumEntries)
        {
            return _current != null
                ? _current.CaptureExecutionLog(maximumEntries)
                : new ScenarioRuntimeExecutionEntrySnapshot[0];
        }

        public bool TryFireRuntimeElement(string elementId, out string message)
        {
            if (_current != null)
                return _current.TryFireRuntimeElement(elementId, out message);

            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryGetMinutesUntilNextAuthoredEvent(int maximumMinutes, out int minutes)
        {
            if (_current != null)
                return _current.TryGetMinutesUntilNextAuthoredEvent(maximumMinutes, out minutes);

            minutes = 0;
            return false;
        }

        public void NotifyGameTimeChanged()
        {
            if (_current != null)
                _current.NotifyGameTimeChanged();
        }

        public bool TryPreviewRuntimeSpriteFrame(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite)
        {
            return _current != null
                && _current.TryPreviewRuntimeSpriteFrame(targetPath, targetKind, sprite);
        }

        public bool TryPlayRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite[] frames,
            float[] durations,
            float speed)
        {
            return _current != null
                && _current.TryPlayRuntimeSpriteAnimation(targetPath, targetKind, frames, durations, speed);
        }

        public void StopRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind)
        {
            if (_current != null)
                _current.StopRuntimeSpriteAnimation(targetPath, targetKind);
        }

        public void CaptureRuntimeObjectState(Obj_Base source, ObjectPlacement destination)
        {
            if (_current != null)
                _current.CaptureRuntimeObjectState(source, destination);
        }

        public bool IsStationObject(Obj_Base obj)
        {
            return _current != null && _current.IsStationObject(obj);
        }

        public void CaptureStationUpgradeState(Obj_Base source, ObjectPlacement destination)
        {
            if (_current != null)
                _current.CaptureStationUpgradeState(source, destination);
        }

        public ScenarioStationUpgradeSnapshot GetStationUpgradeSnapshot(
            Obj_Base obj,
            ObjectPlacement placement)
        {
            return _current != null ? _current.GetStationUpgradeSnapshot(obj, placement) : null;
        }

        public bool TryChangeStationObjectLevel(
            Obj_Base obj,
            ObjectPlacement placement,
            int delta,
            out string message)
        {
            if (_current != null)
                return _current.TryChangeStationObjectLevel(obj, placement, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryChangeStationUpgradeLevel(
            Obj_Base obj,
            ObjectPlacement placement,
            string pathName,
            int delta,
            out string message)
        {
            if (_current != null)
                return _current.TryChangeStationUpgradeLevel(obj, placement, pathName, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryChangeStationStat(
            Obj_Base obj,
            ObjectPlacement placement,
            string statName,
            float delta,
            out string message)
        {
            if (_current != null)
                return _current.TryChangeStationStat(obj, placement, statName, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryClearStationStat(
            Obj_Base obj,
            ObjectPlacement placement,
            string statName,
            out string message)
        {
            if (_current != null)
                return _current.TryClearStationStat(obj, placement, statName, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public void Close()
        {
            IScenarioPreviewSession current = _current;
            _current = null;
            _scenarioFilePath = null;
            if (current != null)
                current.Dispose();
        }

        private static ScenarioPreviewResult ScenarioPreviewResultForMissingSession()
        {
            // The API intentionally owns result construction. A null result is handled
            // by ScenarioEditorPlaytestResult as a failed preview without a facade shim.
            return null;
        }
    }
}
