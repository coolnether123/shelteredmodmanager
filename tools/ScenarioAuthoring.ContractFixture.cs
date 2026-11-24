public class ScenarioModDependencyDefinition { }
public class ScenarioNpcDefinition { }
public class ScenarioFlowDefinition { }
public class FamilySetupDefinition { }
public class ScenarioLaunchSetupDefinition { public static ScenarioLaunchSetupDefinition CreateDefault() { return new ScenarioLaunchSetupDefinition(); } }
public class StartingInventoryDefinition { }
public class BunkerEditsDefinition { }
public class TriggersAndEventsDefinition { }
public class QuestAuthoringDefinition { }
public class MapAuthoringDefinition { }
public class WinLossConditionsDefinition { }
public class ScenarioScoringDefinition { }
public class AssetReferencesDefinition { }
public class ScenarioBunkerGridDefinition { }
public class ScenarioBackendWorldsDefinition { }
public class ScenarioGateDefinition { }
public class ScenarioScheduledActionDefinition { }
public class JournalDefinition { }
public class ScenarioConversationAuthoringDefinition { }
public class ScenarioVanillaSuppressionDefinition { }
public static partial class ScenarioAuthoringContractFixture {
    public static int Count;
    public static void Verify() {
        foreach (ScenarioBaseGameMode mode in Enum.GetValues(typeof(ScenarioBaseGameMode))) {
            ScenarioDefinition definition = CreateDefinition(mode);
            Count++;
            if (definition.BaseGameMode != mode
                || definition.SelectionRules.Availability.Survival != (mode == ScenarioBaseGameMode.Survival)
                || definition.SelectionRules.Availability.Surrounded != (mode == ScenarioBaseGameMode.Surrounded)
                || definition.SelectionRules.Availability.Stasis != (mode == ScenarioBaseGameMode.Stasis))
                throw new Exception("New scenario availability must match the requested base mode: " + mode);
        }
    }
}
