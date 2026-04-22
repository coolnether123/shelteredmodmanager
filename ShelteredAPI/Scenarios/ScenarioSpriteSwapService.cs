using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteSwapService : IScenarioSpriteSwapEngine
    {
        private readonly object _sync = new object();
        private readonly ScenarioSpriteSwapPlanner _planner;
        private readonly ScenarioSpriteSwapRenderer _renderer;
        private ScenarioDefinition _definition;
        private string _scenarioFilePath;
        private string _scenarioId;
        private int _lastAppliedDay = int.MinValue;
        private int _lastPlannedRuleCount;
        private int _lastAppliedRuleCount;
        private int _lastRetryFrame = -1;

        public static ScenarioSpriteSwapService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSpriteSwapService>(); }
        }

        internal ScenarioSpriteSwapService(ScenarioSpriteSwapPlanner planner, ScenarioSpriteSwapRenderer renderer)
        {
            _planner = planner ?? new ScenarioSpriteSwapPlanner(new ScenarioSpriteAssetResolver());
            _renderer = renderer ?? new ScenarioSpriteSwapRenderer();
        }

        public void Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            lock (_sync)
            {
                _definition = definition;
                _scenarioFilePath = scenarioFilePath;
                _scenarioId = definition != null ? definition.Id : null;
                _lastAppliedDay = int.MinValue;
            }

            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SpriteSwaps == null
                || definition.AssetReferences.SpriteSwaps.Count == 0)
            {
                Clear("Scenario has no sprite swap rules.");
                if (result != null)
                    result.SpriteSwapChanges = 0;
                return;
            }

            ApplyForCurrentDay("Scenario definition activated.", result);
        }

        public void Update()
        {
            ScenarioDefinition definition;
            lock (_sync)
            {
                definition = _definition;
            }

            if (definition == null || !ScenarioWorldReady.IsShelterSceneActive())
                return;

            int day = GetCurrentDay();
            if (day != _lastAppliedDay)
            {
                ApplyForCurrentDay("Scenario day changed to " + day + ".", null);
                return;
            }

            if (_lastAppliedRuleCount < _lastPlannedRuleCount && (_lastRetryFrame < 0 || Time.frameCount - _lastRetryFrame >= 30))
            {
                _lastRetryFrame = Time.frameCount;
                ApplyForCurrentDay("Retrying incomplete sprite swap application.", null);
            }
        }

        public void Clear(string reason)
        {
            bool hadState;
            lock (_sync)
            {
                hadState = _definition != null || _lastAppliedDay != int.MinValue;
                _definition = null;
                _scenarioFilePath = null;
                _scenarioId = null;
                _lastAppliedDay = int.MinValue;
                _lastPlannedRuleCount = 0;
                _lastAppliedRuleCount = 0;
                _lastRetryFrame = -1;
            }

            if (!hadState)
                return;

            _renderer.Clear(reason);
            MMLog.WriteInfo("[ScenarioSpriteSwapService] Cleared sprite swap state. Reason=" + (reason ?? "unspecified") + ".");
        }

        private void ApplyForCurrentDay(string reason, ScenarioApplyResult result)
        {
            ScenarioDefinition definition;
            string scenarioFilePath;
            string scenarioId;
            lock (_sync)
            {
                definition = _definition;
                scenarioFilePath = _scenarioFilePath;
                scenarioId = _scenarioId;
            }

            if (definition == null)
                return;

            int currentDay = GetCurrentDay();
            var plan = _planner.BuildPlan(definition, scenarioFilePath, currentDay);
            int appliedCount = _renderer.Apply(plan, reason);
            _lastAppliedDay = currentDay;
            _lastPlannedRuleCount = plan.Count;
            _lastAppliedRuleCount = appliedCount;
            if (result != null)
                result.SpriteSwapChanges = appliedCount;

            MMLog.WriteInfo("[ScenarioSpriteSwapService] Applied sprite swap plan. scenario=" + (scenarioId ?? "<unknown>")
                + " day=" + currentDay + " rules=" + plan.Count + " applied=" + appliedCount
                + " reason=" + (reason ?? "unspecified") + ".");
        }

        private static int GetCurrentDay()
        {
            try
            {
                return GameTime.Day > 0 ? GameTime.Day : 1;
            }
            catch
            {
                return 1;
            }
        }
    }
}
