# API troubleshooting

Use this page for common ModAPI and ShelteredAPI runtime failures. Log text is case-sensitive unless a step says otherwise.

## A setting is missing from the UI

Check these conditions:

- The setting's `Mode` is visible in the current Simple or Advanced view.
- Search and category filters are clear.
- The plugin exposes an attributed `Settings` or `Config` holder that the loader can discover.
- `SpineSettingsHelper.Scan(...)` ran on the object that contains `[ModSetting]` fields.

Search the log for:

```text
Scanning <TypeName> for settings...
Scan complete for <TypeName>. Found <N> definitions.
OnChanged method '<Name>' not found on type <Type>
VisibilityMethod '<Name>' not found on <Type>
ValidateMethod '<Name>' not found on <Type>
```

## A recipe was not injected

Confirm that the result item and every ingredient are registered before injection. Confirm that the recipe uses `Workbench`, `Laboratory`, or `AmmoPress`.

Search the log for:

```text
Recipe '<id>' skipped - result item '<itemId>' not found
Warning: Recipe '<id>' ingredient '<itemId>' could not be resolved. Skipping.
Recipe '<id>' has no valid ingredients. Skipping.
ERROR: Recipe '<id>' has invalid station '<station>'. Valid stations: Workbench, Laboratory, AmmoPress
Recipe injection complete: <added> added, <failed> failed
```

The final summary is a debug log entry.

## An item has no icon

Check that `IconPath` is relative to the mod root and that the file exists. Search the log for:

```text
Failed to load icon at '<path>'
Asset not found at '<fullPath>'
WARNING: Custom item <type> has no icon.
```

## A recipe is hidden at a station

Confirm the station and required level. The UI can also hide a valid recipe when its availability rules fail.

Search the debug log for:

```text
Added recipe '<id>' producing <type> @ <location> Lv<level> ...
```

## Settings do not reload

Check whether the settings object's fields changed, whether the setting uses global or per-save scope, and whether an active save path exists.

Search the log for:

```text
Failed to apply setting <key>: <message>
<prefix> session-started settings reload failed: <message>
NG+ Merge failed for <key>: <message>
```

## A time trigger does not fire

Register the trigger in `Start(...)` with a unique ID and the intended `TimeTriggerCadence`. Then call `ShelteredEvents.GetTimeTriggerPriorityList(...)` and confirm that the trigger appears.

Search the log for:

```text
Trigger callback threw for '<triggerId>': <message>
```

`ShelteredEvents.RegisterTimeTrigger` and `ShelteredEvents.SixHourTick` are API members, not log messages.

## Check plugin startup first

Before deeper debugging:

1. Confirm that the plugin reached `Start(...)`.
2. Confirm that registration code runs in `Start(...)`, not in the constructor.
3. Search the log for the exact messages above.
4. Compare registered IDs without assuming case-sensitive matching.
5. Check for namespace collisions between similarly named API and vanilla types.
