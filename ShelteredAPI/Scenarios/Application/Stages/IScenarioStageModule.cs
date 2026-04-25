using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioStageModule
    {
        ScenarioStageKind StageKind { get; }
        void OnEnter(ScenarioStageContext context);
        void OnExit(ScenarioStageContext context);
        void Update(ScenarioStageContext context);
    }
}
