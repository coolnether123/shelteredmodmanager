using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Built-in effect types that scenario actions can execute.
    /// Custom effect dispatchers can extend behavior without changing the neutral DTO.
    /// </summary>
    public enum ScenarioEffectKind
    {
        UnlockBunkerExpansion = 0,
        ActivateObject = 1,
        DeactivateObject = 2,
        AddInventory = 3,
        RemoveInventory = 4,
        SpawnFutureSurvivor = 5,
        StartQuest = 6,
        SetWeather = 7,
        SetScenarioFlag = 8,
        RestoreWeather = 9,
        FireTrigger = 10
    }
}
