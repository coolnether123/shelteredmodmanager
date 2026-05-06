# ShelteredAPI Dialogue Migration Notes

## Lifespan

Lifespan can replace its local `DialogueScheduler` and `DialogueHelper` with `ShelteredAPI.Dialogue` while keeping all Lifespan-owned narrative pools in the mod.

Recommended mapping:

- Replace `DialogueScheduler.Enqueue(member, text, false, priority, validation)` with a `DialogueRequest` on `DialogueChannel.AmbientSurvivorSpeech`.
- Replace `DialogueScheduler.EnqueueConversation(turns, priority, minDelay, maxDelay)` with `DialogueSequence`.
- Replace `DialogueHelper.PickLine(contextKey, options, speaker)` with `ShelteredDialogue.TrySelectLine(...)`, passing Lifespan-owned `DialogueLineOption` values.
- Set `OwnerId = "Lifespan"` and use stable `ContextKey` values such as `Birthday_123`, `Skill_Strength_Response`, or `AwayObs_42`.
- Use `UseDailyBudget`, `MaxPerDay`, and `MaxPerSpeakerPerDay` for the current daily speech throttling behavior.

Example:

```csharp
DialogueRequest request = new DialogueRequest();
request.OwnerId = "Lifespan";
request.ContextKey = "Birthday_" + member.GetId();
request.Channel = DialogueChannel.AmbientSurvivorSpeech;
request.Speaker = ShelteredDialogue.ForFamilyMember(member);
request.Text = line;
request.Priority = DialoguePriority.Routine;
request.Validation = delegate { return !member.isDead && ageTracker.GetAgeWeeks(member) / 52 == age; };
request.UseDailyBudget = true;
request.MaxPerDay = 1;

ShelteredDialogue.Queue(request);
```

## Faction Overhaul

Faction Overhaul should keep its rule, template, memory, facade, contradiction, composure, and question systems mod-local. ShelteredAPI should not absorb faction-specific encounter narrative logic.

Recommended usage:

- Continue using `FactionEncounterDialogueService`, `EncounterDialogueLineSelector`, and facade systems to build encounter text.
- Use `ShelteredAPI.Dialogue` only as a shared delivery/selection utility where it helps.
- For ambient or world speech, enqueue `DialogueRequest` or `DialogueSequence` through `ShelteredDialogue.Service`.
- For encounter panels, wait for a later concrete `EncounterDialogueChannelAdapter` before routing panel text through ShelteredAPI. The current 1.4 pass intentionally does not replace vanilla `BaseDialogueStage` flow.
- If Faction Overhaul adopts the generic selector/history store, scope with `OwnerId = "FactionOverhaul"` and context keys that include faction/speaker/activity.
