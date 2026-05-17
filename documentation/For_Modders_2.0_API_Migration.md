# For Modders: 2.0 API Migration

SMM 2.0 splits the API surface into two assemblies:

- `ModAPI.dll` is the host-neutral plugin framework: lifecycle, settings, persistence, event bus, Harmony helpers, runtime compatibility helpers, logging, and common utilities.
- `ShelteredAPI.dll` owns Sheltered-specific content, saves, UI, input, events, actors, characters, scenarios, game state, and vanilla integration facades.

## Required Manifest Metadata

Update packaged `About/About.json` files for 2.0 packages:

```json
{
  "requiredModApiVersion": "2.0.0.0",
  "requiredShelteredApiVersion": "2.0.0.0"
}
```

Use both fields when the mod references both assemblies. Do not leave old 1.3 metadata in a package advertised as SMM 2.0 compatible.

## Migration Pass

1. Rebuild against SMM 2.0 `ModAPI.dll` and `ShelteredAPI.dll`.
2. Move Sheltered-specific namespaces to the supported `ShelteredAPI.*` facades.
3. Keep neutral framework usage on `ModAPI.*`.
4. Verify `entryType` still points to an `IModPlugin` implementation, or remove it if the mod relies on plugin scanning.
5. Update `About.json`, README requirements, and Nexus description.
6. Run the mod alone, then with the full public mod set.
7. Check save/load warnings before advertising old-save compatibility.

## Player-Facing Compatibility Text

Use plain language on Nexus pages:

`Requires Sheltered Mod Manager 2.0 Beta or newer. Older SMM 1.2.2/1.3 versions are not supported by this package. Back up saves before switching major SMM versions.`
