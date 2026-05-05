using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Stages;
namespace ShelteredAPI.Scenarios.Application.Stages{
    internal interface IScenarioStageModule
    {
        ScenarioStageKind StageKind { get; }
        void OnEnter(ScenarioStageContext context);
        void OnExit(ScenarioStageContext context);
        void Update(ScenarioStageContext context);
    }
}
