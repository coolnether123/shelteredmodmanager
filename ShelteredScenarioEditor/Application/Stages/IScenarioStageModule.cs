using ModAPI.Scenarios;

using ShelteredScenarioEditor.Domain.Stages;
namespace ShelteredScenarioEditor.Application.Stages{
    internal interface IScenarioStageModule
    {
        ScenarioStageKind StageKind { get; }
        void OnEnter(ScenarioStageContext context);
        void OnExit(ScenarioStageContext context);
        void Update(ScenarioStageContext context);
    }
}
