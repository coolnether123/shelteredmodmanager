# Shared mod hooks

Use `ShelteredAPI.dll` and subscribe through `ShelteredEvents`. Existing mods keep their patches until they migrate. These hooks run on the game thread. Unsubscribe named handlers when your plugin stops. Each handler runs in subscription order; a failure is attributed to its mod, warned once, and does not skip later handlers. PatchRegistry owns activation, domain controls and conflict reports.

## Death and fatal damage

```csharp
ShelteredEvents.CharacterDied += death => {
    RecordKill(death.EncounterKiller, death.EncounterVictim, death.Context);
};
ShelteredEvents.CharacterFatalDamageStarting += member => CaptureActiveJob(member);
ShelteredEvents.CharacterFatalDamage += (member, allowed) => FinishQueueCleanup(member);
```

`CharacterDied` is a confirmed live-to-dead transition. `Victim` and `Killer` are `BaseCharacter` references when available. Encounter-only NPCs, animals and pets can have null shelter references; use `EncounterVictim` and `EncounterKiller`. Killer is null when vanilla has no credited attacker. `Cause`, `EncounterDamageType`, and `Details` retain native damage information. Context is `Shelter`, `Breach`, or `ExpeditionEncounter`. Fatal damage can instead produce unconsciousness, so its two notifications are separate from death. The after notification's bool is vanilla's allow-death result.

Trace: `Character/BaseCharacter.cs:Damage` calls fatal handling, defers death if `CanDieNow` rejects it, and finally commits health. `Kill` delegates to Damage. Pending death in `Update` retries Damage. `Character/FamilyMember.cs:Damage` delegates to base after its guard; `OnFatalDamageTaken` handles unconsciousness and queues. `NpcVisitor` and `Pet` use the base damage path. Hunger, thirst, disease, radiation, bleeding, traps and scripted kills converge on Damage or Kill. Encounter weapon, thrown-item and bleeding damage converge on `Encounters/EncounterCharacter.cs:TakeDamage` and `Health`; the setter reports death after the attacker fields are assigned. Knockout is excluded. Later damage to the family representation is deduplicated. Healing clears pending deduplication. Loading an already dead character does not publish a new death. Attribution outside encounters remains unknown when the game supplies no attacker.

## Walk speed

```csharp
var attributes = ShelteredCharacters.FromFamilyMember(member).Attributes;
var penalty = attributes.Apply("WalkSpeed", 0.75f, 0f, "example.mod");
penalty.IsMultiplicative = true;
penalty.MinimumResult = 0.05f;
// When the condition ends:
attributes.Remove(penalty);
// On mod cleanup: attributes.RemoveAllFromMod("example.mod");
```

`WalkSpeed` is currently the only attribute name applied to vanilla gameplay. Names are case insensitive. The GetWalkSpeed postfix computes `(vanilla + sum of additions) * product of factors`, then applies the greatest contributed minimum, at least zero. No contributions preserve the vanilla result exactly. Nonfinite and expired contributions are ignored. Duration zero is permanent. Factor and minimum survive save/load; old saves default to additive. `GetModifier` continues returning only the additive sum. Set the kind immediately after Apply. Refresh a changing pregnancy/age contribution by removing its handle and applying another. Do not also keep the old speed patch after migration or the penalty applies twice.

## Body and appearance

```csharp
CharacterMesh body;
if (ShelteredCharacters.TryGetBody(member, out body))
    body.transform.localScale = desiredScale;
string message;
if (!ShelteredCharacters.TrySetBody(member, "man", out message))
    Log(message);
```

The existing scenario appearance service owns mesh access and replacement. Resolve the body again after replacement. Callers can adjust depth, colors and textures using the returned native body. Replacement preserves local scale, position and colors; it selects textures for the new body, aligns saved/original texture identities and updates the avatar. A mesh id must exist in CharacterMeshOptions. No patch runs for body access.

## Trading panel

```csharp
ShelteredEvents.TradeUpdated += panel => RefreshAmountDisplay(panel);
ShelteredEvents.TradeSelectionLabelsUpdated += panel => RefreshSelectionDisplay(panel);
// After changing suggested stock:
ShelteredEvents.RefreshTradingPanel(panel);
```

Notifications run after the matching vanilla methods, including early returns. RefreshTradingPanel calls both private refresh methods with a reentrancy guard. Handlers must avoid directly invoking those methods recursively. Existing NpcVisitor interaction or selection gesture patches can remain if they implement behavior outside these notifications.

## Expedition party panel

```csharp
ShelteredEvents.ExpeditionPanelShown += panel => InitializeExtraSlots(panel);
ShelteredEvents.ExpeditionPanelUpdated += panel => RefreshPartyEligibility(panel);
```

These run after OnShow and Update. They support slot presentation, filtering and button/readiness changes. Party size and navigation policies remain mod responsibilities.

## Timed object wear

```csharp
ShelteredEvents.ObjectDegrading += args => {
    if (IsTrackedRecyclingBatch(args.Object)) args.WearAmount = 0;
    args.Interval *= DurabilityFactor(args.Object);
};
```

One shared args instance carries `Object`, `OriginalWearAmount`, settable `WearAmount`, `OriginalInterval`, and settable `Interval`. Later handlers see earlier adjustments. Negative wear clamps to zero; invalid intervals are ignored. A narrow transpiler replaces the single timed Degrade call inside Obj_Integrity.Update. Fire and direct damage remain separate. The adjusted interval schedules the next tick, then a finalizer restores the base interval even after an exception. Shape mismatch leaves the method unchanged and warns once. This supports recycling timed-wear suppression and durability without suppressing vanilla Update. Recycling batch tracking, persistence and final per-item loss remain mod logic.

## Expedition tick

```csharp
ShelteredEvents.ExpeditionTick += manager => CleanupRadioRequests(manager);
```

Runs after ExplorationManager.UpdateManager, including early returns. A callback cannot prevent a party state from executing earlier in the frame. Keep state-specific prefixes when suppression is required.

## Map size policy

```csharp
ShelteredMap.RegisterMapSizePolicy(new MapSizePolicy {
    SourceId = "example.mod", PolicyId = "world-size", Priority = 100,
    Width = 80, Height = 32, TerrainMultiplier = 2f, ContentMultiplier = 2f,
    GenerationFinished = (map, error) => { if (error == null) BalanceProximity(map); }
});
// On cleanup:
ShelteredMap.ClearPolicies("example.mod");
```

Uses the existing ExpeditionMapGenerationPolicy registry and its source/id replacement and priority ordering. Later resolved non-null dimensions win; count factors multiply. Dimensions are restricted to 1..4096. Arrays and grid scales adapt before the existing CreateMap hook. Terrain counts round; content counts ceil with a minimum of one for positive originals. Location and town density policies now also affect generation counts. Counts restore after success or failure; generated dimensions and arrays remain. GenerationFinished runs before count restoration and receives the original generation exception. Policy setup failure restores its partial changes and falls back to vanilla with a warning. Empty registration does nothing. This is a new-map policy, not a resize-existing-save operation. Difficulty UI and proximity balancing remain mod logic. See [migration notes](Shared_Mod_Hooks_Migration.md).
