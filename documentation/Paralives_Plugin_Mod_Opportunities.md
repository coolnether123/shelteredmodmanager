# Paralives Plugin Mod Opportunities

Date reviewed: 2026-05-27

Scope: local repository and decompiled Paralives code under `Decompiled/Paralives.dll`, plus the current SMM/ModAPI/ParalivesAPI integration. This is a reverse-engineered code review, not an in-game QA pass.

## Short Take

Relationships are not missing from the game code. The core pieces exist: relationship save data, relationship labels, UI, social group integration, together-card outcomes, context requirements, memories, and registered update systems.

The likely problem is that relationships may feel "not working" because progression content and glue are thin, hidden, or fragile. A mod does not need to build relationships from scratch. It needs to stabilize creation/editing, expose diagnostics, and add relationship-changing interactions/together cards.

Estimated difficulty:

| Goal | Difficulty | Notes |
| --- | --- | --- |
| Make sure relationship pairs are created and visible | Low to medium | Existing `RelationshipManager.CreateNewRelationship` and UI can be used. Needs null guards and save-dirty checks. |
| Add basic relationship growth from social actions | Medium | Existing outcome types can unlock/level labels. The work is content wiring and balancing. |
| Add new relationship labels/cards/interactions cleanly | Medium to high | Generated setting setters exist, but runtime content injection needs careful dirty/refresh behavior. |
| Build a complete relationship overhaul | High | Requires content, UI, autonomy, memories, balance, localization, and save migration. |

## Evidence From Code

- `SystemManager` registers `UpdateSocialGroups`, `UpdateCharacterRelationships`, `UpdateTogetherEnergy`, `UpdateGroupRelationships`, `UpdateCharacterWants`, `UpdateCharacterOccupations`, and `UpdateCharacterNeeds`.
- `RelationshipManager` supports pair lookup, creation, default labels, unlocking labels, incrementing label levels, symmetrical labels, household refresh, and lifestage cleanup.
- `AssetCharacterRelationshipData` persists `With`, `RelationshipLabelData`, and `TimestampOfLastInteracted`.
- `UIRelationships`, `UIRelationshipsPicker`, and `UIRelationshipsLabel` render and edit relationships.
- `TogetherManager.ProcessOutcomes` processes together-card outcome logic, and `TogetherManager.GetLabelAffectedByCard` explicitly looks for relationship label outcome types.
- `OutcomeType` includes `GainRelationshipLabelLevel`, `UnlockRelationshipLabel`, `UnlockOrLevelRelationshipLabel`, `IncreaseRelationshipLabelWithAll`, and group-pair outcome processors.
- `ContextRequirementType` includes relationship checks, group-pair relationship checks, together-card checks, and `TimeSpentWithCharacterSince`.
- Generated setters exist for `RelationshipLabels` and `Together` arrays, including label arrays, together cards, referenced requirements, and referenced outcomes.

## Relationship System Review

The current relationship pipeline looks like this:

1. Characters enter social groups through interactions.
2. `UpdateGroupRelationships` creates relationship records for pairs inside newly created social groups.
3. `UpdateCharacterRelationships` creates missing relationship records among current household members.
4. `UpdateSocialGroups` updates `TimestampOfLastInteracted` for characters in the same group.
5. Together cards call `TogetherManager.ProcessOutcomes`.
6. Outcome processors call `RelationshipManager.UnlockLabel` or `IncrementLabelLevel`.
7. Relationship UI reads `character.Data.Relationships` and shows visible labels.

That is enough foundation for a relationship mod. The highest-value first pass should not replace the system. It should make the existing pipeline visible and reliable.

Potential weak spots:

- `UIRelationshipsPicker.Refresh()` assumes `RelationshipManager.GetLabelsBetweenCharacters(...)` is non-null before iterating it. If the picker opens for two characters without an existing relationship, that can fail.
- `RelationshipManager.CreateNewRelationship`, `AddLabelData`, and `RemoveRelationshipLabel` mutate character data but do not visibly set `AssetCharacter.IsSaveDirty`; `IncrementLabelLevel` does. This may be harmless if saves serialize all characters, but it is suspicious and worth testing.
- `IncrementLabelLevel` writes a relationship-level memory before it confirms the relationship and label exist. Invalid calls may create misleading memories.
- UI lists relationships already present on the selected character. If a parafolk is known but has no relationship record yet, they may not appear.
- Progression depends on content. If few together cards or interactions emit relationship label outcomes, relationships can exist technically while feeling inactive to players.

## Highest-Value Plugin Mods

| Mod idea | Why it would do well | Main hooks | Difficulty |
| --- | --- | --- | --- |
| Relationship Fix and Diagnostics | Players immediately notice if relationships do not appear or save. This would validate the base system. | `RelationshipManager`, `UIRelationshipsPicker`, `UpdateGroupRelationships`, `TogetherManager.GetLabelAffectedByCard` | Medium |
| Basic Relationship Progression | Makes relationships feel alive without a full overhaul. | Together card outcomes, social group timestamps, relationship label level processors | Medium |
| Relationship UI Plus | Current UI appears label-centric and may hide unknown/missing pairs. Filters/history/debug tools would help players and modders. | `UIRelationships`, `UIRelationshipsLabel`, `MemoryLogSaveData` | Medium |
| Expanded Relationship Labels | Adds best friend, crush, rival, enemy, awkward, mentor, neighbor, coworker, etc. | `RelationshipLabelsSetter`, `RelationshipLabel`, default labels, equivalences | Medium-high |
| Together Card Expansion | The together-card system is already designed for social outcomes, success chances, target picking, and replies. | `Together2Setter`, `TogetherCard`, referenced outcomes/requirements | Medium-high |
| NPC Social Life | Let non-household parafolk gain/lose relationship labels from town autonomy and social groups. | `UpdateTownAutonomy`, `SocialGroupManager`, relationship outcomes | High |
| Get-To-Know Plus | Relationship info already supports nickname, age, romance, family, occupation, wants, skills, and secrets. | `RelationshipManager.GetStringOfKnownInformation`, `GetToKnowManager`, known info data | Medium |
| Wants and Goals Expansion | Wants are active, persisted, progress-tracked, and connected to emotions/skills/social targets. | `WantsManager`, `UpdateCharacterWants`, `GoalsManager`, `BrainLogic` | Medium |
| Emotion and Status Effect Rebalance | Status effects modify needs, skills, emotions, together bar speed, want capacity, etc. | `StatusEffectManager`, `UpdateCharacterStatusEffects`, status effect settings | Medium |
| Social Autonomy Tuning | Autonomy can select social targets and inject interactions. Better social behavior would make the world feel less idle. | `UpdateCharacterAutonomy`, `AutonomyManager`, `InteractionManager.InjectInteraction` | High |
| Family/Kinship Repair | Relationship labels include family arrays and family tree UI exists. Save repair and missing inverse labels would be valuable. | `RelationshipLabels`, `UIFamilyTreeCharacter`, `RelationshipManager.UnlockLabel` | Medium |
| Education/Homework Overhaul | The school/occupation/wants pipeline is rich and already researched in this repo. | `UpdateCharacterSchoolEnrollment`, `OccupationsManager`, `WantsManager` | Medium-high |
| Career/Odd Job Pack | Occupations, schedules, offers, unlockables, performance, strikes, and tasks are already modeled. | `Occupations`, `OccupationsManager`, occupation outcomes | Medium-high |
| Needs/Autonomy Balance | Needs, autonomy, and interaction usability are connected and likely easy to feel in gameplay. | `NeedManager`, `UpdateCharacterNeeds`, `UpdateCharacterAutonomy` | Medium |
| Save Repair and Mod Diagnostics | Modded saves will need repair tools: missing inverse relationships, duplicate labels, dead GUIDs, orphaned social groups. | `AssetManager`, `SavedGameManager`, relationship/social data | Medium |
| Paralives Gameplay API Facade | Current `ParalivesAPI` mostly registers runtime info and SMM mod screen integration. Gameplay mods still need direct decompiled types/Harmony. | New `ParalivesAPI.Relationships`, `ParalivesAPI.Together`, `ParalivesAPI.Characters` wrappers | Medium-high |

## Other Systems Worth Modding

Relationships are only one good target. The decompiled code shows several gameplay systems that are already functional enough to extend.

### Needs

The needs loop is active and centralized. `UpdateCharacterNeeds` checks `Needs.EnableNeedsFeature`, decays household needs, force-refills NPC needs, and applies relief from `MemoryLogType.IsRelievingNeed`. `NeedManager` exposes direct operations for changing, setting, decaying, relieving, force-refilling, capping, and masking needs.

Good mod ideas:

- Needs rebalance presets: slower decay, harsher critical thresholds, separate child/adult tuning.
- New hidden or temporary needs: stress, boredom, loneliness, comfort, illness recovery.
- Status-effect-driven needs: weather, traits, objects, school/work, and relationships changing decay or caps.
- Better NPC needs simulation: current NPC logic force-relieves needs to full, so NPCs can feel less grounded than household characters.

Difficulty: medium. The manager API is straightforward, but adding a brand-new need cleanly means touching settings, UI, status effects, interactions, and localization.

### Wants, Goals, And Requests

Wants and goals are a strong mod target. `UpdateCharacterWants` tracks active wants, progress, failure, completion, household-shared progress, and goal objective progress. `WantsManager` handles capacity, cooldowns, occupation/school tasks, reactions, completion rewards, and notifications. Daily town requests are generated by `GenerateRequests`, then displayed by `UpdateLoopRequests` with thought bubbles and request boards.

Good mod ideas:

- More wants tied to social life, skills, lot ownership, collections, careers, and family.
- Request board expansion with daily errands, favors, deliveries, social requests, and skill commissions.
- Goal packs for long-term life arcs: artist, athlete, caregiver, introvert, socialite, collector.
- Better want controls: pinning, rerolling, blocking categories, or increasing capacity by trait/status.

Difficulty: medium. The data structures are there. The hard part is authoring enough balanced content and making sure requirements report progress correctly.

### School, Careers, And Homeschool

School is implemented as an occupation. `UpdateCharacterSchoolEnrollment` auto-enrolls mandatory lifestages into default school and removes inappropriate school occupations after lifestage changes. `UpdateCharacterOccupations` handles scheduled attendance, ending workdays, vacation/skipped days, job performance, active occupation memories, and task expiry.

Good mod ideas:

- Homeschool mode that keeps the vanilla school occupation active but blocks physical attendance.
- More school tracks: public school, private school, online school, apprenticeship, arts academy.
- Career pack: new careers, part-time work, odd jobs, self-employment, gig requests.
- Work/school difficulty rebalance: performance, strikes, vacation, grade decay, task frequency.
- Better schedule UI and notifications.

Difficulty: medium-high. The occupation system is rich, but it is easy to fight the mandatory enrollment loop if the mod removes/replaces vanilla school state too aggressively.

### Autonomy And Town Life

Autonomy is one of the biggest opportunities. `UpdateCharacterAutonomy` handles forced autonomy and idle autonomy for loaded characters. `AutonomyManager` scores rules, finds items, chooses targets, and injects interactions. `UpdateTownAutonomy` scores lots and town rules for NPC movement, while `UpdateSpawnTownies` auto-refills town population from premades.

Good mod ideas:

- Smarter social autonomy: NPCs initiate hangouts, arguments, flirting, errands, or visits.
- Town routines: morning coffee, school commute, work lunch, nightlife, park visits, seasonal events.
- Population manager: cap by lifestage, family composition, occupation, traits, or lot availability.
- Autonomy debugging overlay: show why a rule was or was not picked.
- Less idle household behavior: context-aware chores, hobbies, social actions, and need relief.

Difficulty: high. The hooks exist, but autonomy changes can create queue spam, bad pathfinding, or interactions that interrupt the player.

### Fire, Disasters, And Maintenance

Fire is already modeled. `UpdateItemsOnFire` spreads fire, burns items over time, resets states on burn, spawns fire objects, and respects configured spread limits. `FireManager` can set, extinguish, and spawn fire on items.

Good mod ideas:

- Fire rebalance: slower/faster spread, item categories that burn differently, safer fireplaces.
- New disasters: power outage, plumbing leak, mold, pest infestation, heatwave, storm damage.
- Maintenance gameplay: repair contracts, inspections, insurance, emergency services.
- Lot challenge modes: old wiring, cursed kitchen, dry season, messy tenants.

Difficulty: medium. Fire hooks are concrete. New disaster types are harder because they need UI, effects, item states, and cleanup paths.

### Mail, Newspapers, Calendar, And World Events

`GenerateMail` and `GenerateNewspaper` are small but valuable extension points. Requests, mail, newspapers, calendar events, tickets, town autonomy, and notifications can combine into world events.

Good mod ideas:

- Weekly newspaper stories based on player actions, fires, careers, births, deaths, and town events.
- Mail expansion: bills, invites, gifts, warnings, school reports, job offers.
- Event system: festivals, school events, sales, community projects, town drama.
- Narrative hooks: secrets, rumors, relationships, requests, and news articles feeding each other.

Difficulty: medium. This is mostly content and integration, but it needs careful cooldowns so the player is not spammed.

### Build/Buy And Items

The item/build side has many registered systems: dirty meshes/materials/texts, item states, slots, placement, fire, dirtiness, brokenness, lights, clocks, doors, mirrors, inventories, catalog tags, item finder rules, and interaction groups.

Good mod ideas:

- Item state expansion: dirty, broken, upgraded, haunted, rented, locked, reserved.
- New global interactions injected by tags: clean, inspect, repair, admire, prank, upgrade.
- Better catalog filters and build/buy search.
- Object pack compatibility helpers: validate locators, slots, tags, interactions, and item states.
- Lot tools: auto-lighting, cleanup, repair all, replace burnt items, inventory all collectibles.

Difficulty: medium to high. Simple interaction/tag mods are medium. New objects with prefabs/assets are higher because asset pipeline and locators matter.

### Debugging, Save Repair, And Modder Tools

This repo already has runtime inspector/debugging support in `ModAPI`, and Paralives itself has lots of manager state. A modder tool pack would probably help every other mod.

Good mod ideas:

- Save repair: dead GUIDs, orphaned relationships, orphaned social groups, impossible occupations, duplicate wants.
- Gameplay inspector: selected character needs, wants, relationships, current interactions, autonomy rule, town autonomy state.
- Content validator: relationship labels, together cards, wants, occupations, item tags, localization keys.
- Patch compatibility report: which mods patch the same gameplay methods.

Difficulty: medium. It is mostly read-only at first and becomes safer if repair actions are explicit and reversible.

## Best Non-Relationship First Mods

If the goal is a useful plugin quickly, these are the best non-relationship starting points:

1. Request Board Expansion: daily requests already exist and are easy for players to notice.
2. Homeschool Lite: the school-as-occupation system gives a clear target and the repo already has research notes.
3. Needs Rebalance Presets: small hooks, immediate gameplay impact, low asset burden.
4. Autonomy Debug Overlay: helps build every later autonomy/social mod.
5. Save Repair Tool: useful for all experimental mods and likely easy to test in small pieces.

## Best First Relationship Mod

Build a "Relationship Fix and Diagnostics" plugin first. It gives quick proof about whether relationships are broken, incomplete, or just under-contented.

Suggested behavior:

1. On session start or next gameplay frame, scan current household relationships.
2. Ensure every household pair has a reciprocal relationship record.
3. Patch or guard relationship picker opening so it creates the pair before showing labels.
4. Log all together cards that contain relationship label outcomes.
5. Add a lightweight debug command or UI panel showing:
   - selected character
   - known relationship pairs
   - labels and levels
   - last interacted timestamp
   - missing reciprocal pairs
   - dead target GUIDs
6. Mark affected characters dirty after relationship creation, label add/remove, and label level changes.

Likely patch targets:

- `UIRelationships.OnEditRelationships()` or `UIRelationshipsPicker.Init(...)`: ensure pair exists before the picker refreshes.
- `RelationshipManager.CreateNewRelationship(...)`: postfix mark both characters dirty and optionally publish diagnostics.
- `RelationshipManager.AddLabelData(...)`: postfix mark source character dirty.
- `RelationshipManager.RemoveRelationshipLabel(...)`: postfix mark source character dirty.
- `RelationshipManager.IncrementLabelLevel(...)`: validate the label exists before memory logging, if this proves to be a real issue in-game.

This is a good first project because it is mostly additive and diagnostic. It should reveal whether the game already ships relationship-changing cards/interactions or whether the content layer is the missing piece.

## How Hard Is "Get Relationships Working?"

If "working" means relationships exist, show in the panel, and can be manually edited or repaired: low to medium. The manager and UI already exist.

If "working" means relationships progress naturally from social interactions: medium. The outcome processor path already exists, but the mod has to add or patch content so actual together cards/interactions call `UnlockOrLevelRelationshipLabel` or `GainRelationshipLabelLevel`.

If "working" means a polished Sims-like social system with romance, friendship, rivalry, decay, memories, autonomy, UI history, family constraints, lifestage safety, and NPC behavior: high. The engine hooks are there, but the content and balancing work is large.

My read: this is not an engine-hard problem. It is a gameplay/content integration problem with a few likely bugs around null handling, dirty-save marking, and visibility.

## Implementation Notes For Modders

- Use SMM plugin lifecycle: `IModPlugin.Initialize` for state/services and `Start` for Harmony patches.
- Use `IModSessionEvents` or delayed frame execution before touching `Settings`, `SavedGameManager`, `HouseholdManager`, or `CharacterManager`.
- Prefer small postfix/prefix patches before transpilers.
- Keep mod save data separate with `ctx.SaveSystem.RegisterModData(...)` for any custom relationship metadata.
- Direct references to `Paralives.dll` are currently practical because `ParalivesAPI` does not yet expose relationship/together facades.
- For content injection, generated setters can mutate settings, but test whether runtime setting compilation/dirty flags refresh dictionaries and translations correctly.

## Recommended Roadmap

1. Relationship diagnostics and pair repair.
2. Basic relationship progression from existing together-card use.
3. UI improvements for relationship visibility and history.
4. Add new labels and a small set of cards that use existing outcome processors.
5. Add autonomy rules so NPCs initiate relationship-relevant social interactions.
6. Extract stable helper wrappers into `ParalivesAPI` once the patch behavior is proven.
