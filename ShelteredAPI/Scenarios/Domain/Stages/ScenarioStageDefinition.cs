using System;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Metadata for one scenario editor stage.
    /// Used by authoring UI to group sections and by validation to report where issues belong.
    /// </summary>
    public sealed class ScenarioStageDefinition
    {
        public ScenarioStageDefinition(
            ScenarioStageKind kind,
            string displayName,
            int order,
            ScenarioStageKind parentKind,
            bool isTopLevel,
            bool isAuthoringStage,
            string description)
        {
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException("displayName");

            Kind = kind;
            DisplayName = displayName;
            Order = order;
            ParentKind = parentKind;
            IsTopLevel = isTopLevel;
            IsAuthoringStage = isAuthoringStage;
            Description = description ?? string.Empty;
        }

        public ScenarioStageKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public int Order { get; private set; }
        public ScenarioStageKind ParentKind { get; private set; }
        public bool IsTopLevel { get; private set; }
        public bool IsAuthoringStage { get; private set; }
        public string Description { get; private set; }

        public bool HasParent
        {
            get { return ParentKind != ScenarioStageKind.None; }
        }
    }
}
