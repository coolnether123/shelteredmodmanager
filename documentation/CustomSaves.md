Custom Save Slots (ModAPI)

Overview
- Virtual save slots per scenario are stored under `Mods/ModAPI/Saves/<scenarioId>/` with a `manifest.json` and entry files `<saveId>.xml`, plus optional `previews/<saveId>.png`.
- Reserved base slots (1–3) can be marked for a scenario; when reserved, the UI shows virtual entries for that slot, paged in groups of 3 using Left/Right arrow keys.
- Loading and saving integrate with the game’s normal pipeline via a PlatformSave proxy so core ISaveable flows remain intact.

API (namespace `ModAPI.Saves`)
- ScenarioRegistry.RegisterScenario(ScenarioDescriptor)
- SlotReservationManager.SetSlotReservation(int physicalSlot, SaveSlotUsage usage, string scenarioId)
- CustomSaveRegistry.ListSaves(string scenarioId, int page, int pageSize)
- CustomSaveRegistry.GetSave(string scenarioId, string saveId)
- CustomSaveRegistry.CreateSave(string scenarioId, SaveCreateOptions)
- CustomSaveRegistry.OverwriteSave(string scenarioId, string saveId, SaveOverwriteOptions, byte[] xmlBytes)
- CustomSaveRegistry.RenameSave(string scenarioId, string saveId, string newName)
- CustomSaveRegistry.DeleteSave(string scenarioId, string saveId)
- PreviewCapture.CapturePNG(string scenarioId, string saveId, Texture2D frame)

Notes
- Left/Right arrow keys paginate. UI buttons can be added later via NGUI cloning.
- Compatibility mode: If no reserved slots and you stay on page 0, behavior is vanilla.
- Data safety: writes go to `*.tmp` then replace; manifest updated last.

Quick start (in your plugin)
- Register scenario: `ScenarioRegistry.RegisterScenario(new ScenarioDescriptor{ id="MyScenario", displayName="My Scenario", version="1.0" });`
- Reserve slot 1: `SlotReservationManager.SetSlotReservation(1, SaveSlotUsage.CustomScenario, "MyScenario");`
- Start the game and open the save/load screen. Use Left/Right to page virtual entries.
