using ShelteredAPI.Scenarios.Domain.Stages;

namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
    internal sealed class TutorialStep
    {
        public TutorialStep(
            int index,
            string id,
            string title,
            string body,
            string pendingCallout,
            string targetId,
            string targetWindowId,
            ScenarioStageKind targetStage,
            string targetActionId)
        {
            Index = index;
            Id = id;
            Title = title;
            Body = body;
            PendingCallout = pendingCallout;
            TargetId = targetId;
            TargetWindowId = targetWindowId;
            TargetStage = targetStage;
            TargetActionId = targetActionId;
        }

        public int Index { get; private set; }
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string PendingCallout { get; private set; }
        public string TargetId { get; private set; }
        public string TargetWindowId { get; private set; }
        public ScenarioStageKind TargetStage { get; private set; }
        public string TargetActionId { get; private set; }
    }
}
