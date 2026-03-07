# API Troubleshooting (ModAPI + ShelteredAPI)

Use this page for common runtime failures and the exact log signatures to check.

## Compatibility

Applies to current `ModAPI.dll` and `ShelteredAPI.dll`.

## 1. Setting Exists in Code but Not Visible in UI

Likely causes:
- `Mode = Advanced` and you are currently in Simple view.
- Active search/category filter is hiding the setting.
- Plugin is not exposing an `ISettingsProvider`.
- Scanner never ran on the object containing `[ModSetting]`.

Log lines to look for:
- `Scanning <TypeName> for settings...`
- `Scan complete for <TypeName>. Found <N> definitions.`
- `OnChanged method '<Name>' not found on type <Type>`
- `VisibilityMethod '<Name>' not found on <Type>`
- `ValidateMethod '<Name>' not found on <Type>`

## 2. Recipe Not Injected

Likely causes:
- Result item ID not registered or typoed.
- Ingredient item ID not resolvable.
- Invalid craft station enum for recipe.
- Registration done too early/outside normal plugin lifecycle.

Log lines to look for:
- `Recipe '<id>' skipped - result item '<itemId>' not found`
- `Recipe '<id>' skipped - ingredient '<itemId>' not found`
- `ERROR: Recipe '<id>' has invalid station '<station>'. Valid stations: Workbench, Laboratory, AmmoPress`
- `Recipe injection complete: <added> added, <failed> failed`

## 3. Item ID Resolves but UI Icon Is Missing

Likely causes:
- `IconPath` wrong or asset file missing.
- Sprite failed to load.
- Custom item has no resolved icon at runtime.

Log lines to look for:
- `Failed to load icon at '<path>'`
- `Asset not found at '<fullPath>'`
- `WARNING: Custom item <type> has no icon.`

## 4. Recipe Exists but Not Shown at Station/Level

Likely causes:
- Station mismatch (`Workbench`, `Laboratory`, `AmmoPress` only).
- Recipe level above current bench capability.
- Item appears in registry, but UI filtering/availability rules hide it.

Log lines to look for:
- `Added recipe '<id>' producing <type> @ <location> Lv<level> ...`
- `ERROR: Recipe '<id>' has invalid station '<station>'...`

## 5. Settings Saved but Not Reloading as Expected

Likely causes:
- Settings object changed shape or field names.
- Scope mismatch (`Global` vs `PerSave`).
- Save path/scope unavailable for current slot.

Log lines to look for:
- `Failed to apply setting <key>: <message>`
- `[ModManagerBase] Session-started settings re-load failed: <message>`
- `NG+ Merge failed for <key>: <message>`

## 6. Trigger Callbacks Not Firing (`GameTimeTriggerHelper`)

Likely causes:
- No listeners/callbacks registered.
- Game currently loading.
- Trigger not registered for expected cadence.
- Trigger ID was reused and overwritten by a later registration.

Log lines to look for:
- `GameTimeTriggerHelper.SubscribeLifecycle`
- `GameTimeTriggerHelper.Catchup`
- `Trigger callback threw for '<triggerId>': <message>`

Quick checks:
- Confirm `RegisterTrigger(...)` runs in `Start(...)`.
- Confirm `GetPriorityList(...)` contains your trigger for the expected cadence.

## 7. Quick Verification Checklist

1. Confirm plugin reached `Start(...)`.
2. Confirm registration methods are called in `Start(...)`, not constructor.
3. Search logs for the exact signatures above.
4. Validate IDs case-insensitively and consistently.
5. Recheck namespace collisions (`ItemDefinition` aliases).
