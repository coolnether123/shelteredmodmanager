using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioEditorActorReferenceService
    {
        public int AssignMissingCastActorRefs(ScenarioDefinition definition)
        {
            return ShelteredScenarioAuthoring.AssignMissingActorReferences(definition);
        }

        public ScenarioActorRef EnsureStartingMemberRef(ScenarioDefinition definition, FamilyMemberConfig member, int memberIndex)
        {
            return ShelteredScenarioAuthoring.EnsureStartingMemberActorReference(definition, member, memberIndex);
        }

        public ScenarioActorRef EnsureFutureSurvivorRef(ScenarioDefinition definition, FutureSurvivorDefinition survivor, int survivorIndex)
        {
            return ShelteredScenarioAuthoring.EnsureFutureSurvivorActorReference(definition, survivor, survivorIndex);
        }

        public ScenarioActorRef CreateLiveFamilyMemberRef(FamilyMember member)
        {
            return ShelteredScenarioAuthoring.CreateLiveFamilyMemberActorReference(member);
        }
    }
}
