using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioRecordFactory
    {
        public ScenarioRecord CreateRecord(CustomScenarioRegistration registration)
        {
            CustomScenarioInfo info = new CustomScenarioInfo(
                registration.Id,
                registration.DisplayName,
                registration.Description,
                registration.Version,
                registration.Order,
                registration.OwnerModId,
                ScenarioDependencyManifest.CloneRequiredMods(registration.RequiredMods),
                registration.Definition != null,
                registration.DefinitionFactory != null);

            return new ScenarioRecord
            {
                Registration = registration,
                Info = info,
                IsDefinitionBacked = false
            };
        }
    }
}
