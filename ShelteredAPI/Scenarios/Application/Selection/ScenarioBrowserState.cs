using System.Collections.Generic;
namespace ShelteredAPI.Scenarios.Application.Selection{
    /// <summary>
    /// The three top-level buckets the browser can show. Vanilla saves are
    /// scenario-keyed (Survival / Surrounded / Stasis each carry their own
    /// per-scenario save list). Modded covers published custom scenarios from
    /// loaded mods. Draft covers scenario-authoring drafts in progress, so
    /// new in-flight scenarios stay separated from already-published ones.
    /// </summary>
    internal enum ScenarioBrowserSource
    {
        Vanilla = 0,
        Modded = 1,
        Draft = 2
    }

    internal sealed class ScenarioBrowserState
    {
        public int PanelInstanceId;
        public ScenarioBrowserSource Source = ScenarioBrowserSource.Vanilla;
        public int Page;
        public string SelectedScenarioId;
        public string SelectedSaveId;
        public int SelectedAbsoluteSlot;
        public bool LaunchInFlight;

        public bool IsModded
        {
            get { return Source == ScenarioBrowserSource.Modded; }
        }

        public void Reset()
        {
            Source = ScenarioBrowserSource.Vanilla;
            Page = 0;
            SelectedScenarioId = null;
            SelectedSaveId = null;
            SelectedAbsoluteSlot = 0;
            LaunchInFlight = false;
        }
    }

    internal sealed class ScenarioBrowserStateStore
    {
        private static readonly ScenarioBrowserStateStore _instance = new ScenarioBrowserStateStore();
        private readonly Dictionary<int, ScenarioBrowserState> _states = new Dictionary<int, ScenarioBrowserState>();

        public static ScenarioBrowserStateStore Instance
        {
            get { return _instance; }
        }

        private ScenarioBrowserStateStore()
        {
        }

        public ScenarioBrowserState GetOrCreate(int panelInstanceId)
        {
            ScenarioBrowserState state;
            if (!_states.TryGetValue(panelInstanceId, out state) || state == null)
            {
                state = new ScenarioBrowserState();
                state.PanelInstanceId = panelInstanceId;
                _states[panelInstanceId] = state;
            }

            return state;
        }

        public ScenarioBrowserState Find(int panelInstanceId)
        {
            ScenarioBrowserState state;
            _states.TryGetValue(panelInstanceId, out state);
            return state;
        }

        public bool Remove(int panelInstanceId)
        {
            return _states.Remove(panelInstanceId);
        }
    }
}
