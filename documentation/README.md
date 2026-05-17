# Sheltered Mod Manager Documentation

Use this index to pick the shortest useful guide. For exact callable APIs, use [API Signatures Reference](API_Signatures_Reference.md); task guides intentionally show only the signatures needed for that workflow.

## Start Here

| Need | Document |
|------|----------|
| First mod, folder layout, lifecycle | [How to Develop a Plugin](how%20to%20develop%20a%20plugin.md) |
| Current ModAPI/ShelteredAPI overview | [ModAPI Developer Guide](ModAPI_Developer_Guide.md) |
| Project/runtime architecture | [ModAPI Project Map](ModAPI_Documentation.md) |
| Loader and boundary details | [ModAPI Architecture Guide](ModAPI_Architecture_guide.md) |
| Exact API signatures | [API Signatures Reference](API_Signatures_Reference.md) |
| Common failures and log patterns | [API Troubleshooting](API_Troubleshooting.md) |
| Build and verify Dev/2.0 locally | [README build section](../readme.md#building-from-source) |

## Task Guides

| Task | Document |
|------|----------|
| Add settings or per-save state | [Settings and Persistence](SETTINGS.md) |
| Build Spine settings UI | [Spine Settings Guide](Spine_Settings_Guide.md) |
| Register items, recipes, loot, or assets | [ShelteredAPI Content Guide](ShelteredAPI_Content_Guide.md) |
| Build runtime UI, stores, or cooking stations | [Runtime UI, Stores, and Cooking Stations](ShelteredAPI_Runtime_UI_Stores_Guide.md) |
| Work with actors and characters | [ShelteredAPI Actors Guide](ShelteredAPI_Characters_Guide.md) |
| Subscribe to events or scheduled ticks | [Events Guide](Events_Guide.md) |
| Add rebindable controls | [Input Keybindings Guide](Input_Keybindings_Guide.md) |
| Author custom scenarios | [Custom Scenarios Guide](Custom_Scenarios_Guide.md) |
| Patch game code with Harmony | [Harmony Patch Guide](how%20to%20develop%20a%20patch%20with%20harmony.md) |
| Debug transpilers | [Transpiler and Debugging Guide](Transpiler_and_Debugging_Guide.md) |

## Reference And Release Docs

| Topic | Document |
|-------|----------|
| Patch metadata and cooperative patching rules | [Patch Governance](Patch_Governance.md) |
| Transpiler safety flags | [Transpiler Safety Settings](Transpiler_Safety_Settings.md) |
| Public API boundary decisions | [ModAPI Sheltered Boundary Refactor](ModAPI_Sheltered_Boundary_Refactor.md) |
| Developer comment style | [Developer Commenting Standard](Developer_Commenting_Standard.md) |
| Beta.1 release scope | [Beta.1 Release Notes](Release_Beta.1.md) |
| Player upgrade guidance | [SMM 2.0 Beta Migration](SMM_2.0_Beta_Migration.md) |
| Current known issues | [Known Issues](Known_Issues.md) |
| Modder migration checklist | [For Modders: 2.0 API Migration](For_Modders_2.0_API_Migration.md) |
| Player announcement draft | [Player Announcement: SMM 2.0 Beta](Player_Announcement_2.0_Beta.md) |
| Nexus app registration readiness | [Nexus Official Registration Readiness](Nexus_Official_Readiness.md) |
| Local build prerequisites | [README build section](../readme.md#building-from-source) |

## Assembly Rule

- Reference `ModAPI.dll` for neutral plugin lifecycle, settings, persistence, input action, actor contract, event-bus, and Harmony helper APIs.
- Add `ShelteredAPI.dll` when you use Sheltered content, saves, UI, input, events, actors, characters, scenarios, or game-state facades.
- Add `Assembly-CSharp.dll` only when your mod code directly names vanilla Sheltered game types.
