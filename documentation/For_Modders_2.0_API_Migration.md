# For Modders: 2.0 API Migration

SMM 2.0 introduces the ModAPI/ShelteredAPI split. Use the canonical [assembly boundary](README.md#assembly-boundary-canonical) when choosing references; this page covers migration actions for an existing package.

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

### Manager runtime option API

Use `ModAPI.Core.ManagerBooleanOptions.RegisterBooleanOption(...)`, `GetBool(...)`, and `SetBool(...)`.
`ManagerBooleanOptionDefinition` remains public from `ModAPI.dll`; `Manager.exe` no longer emits a duplicate
definition. The JSON container, record, and desktop/runtime policy-input types are internal in the 2.0 line.
Code that referenced `ManagerBooleanOptionsFile` or `ManagerBooleanOptionRecord` must move to the supported
facade instead of editing `manager_options.json` directly.

### Custom scenario API and editor split

Reference `ShelteredAPI.dll` for scenario definitions, XML authoring, registration/catalog access, runtime operations, browsing, and saves. Do not reference `ShelteredScenarioEditor.dll`; it is an optional downstream application and may be physically absent from a player's installation.

`ShelteredScenarios` is now the only Sheltered-specific registration/catalog facade. Code written against the unreleased `ShelteredScenarioRegistration` wrapper must move directly to `ShelteredScenarios`; there is no alias or forwarding shim. `ShelteredScenarioAuthoring` remains the XML/file facade, and `ShelteredScenarioRuntime` remains the active-runtime facade.

Editor workflow metadata such as the author test checklist is not part of `ScenarioDefinition`. The editor keeps it in adjacent `scenario.editor.xml` files and excludes those files from exported packages, so mods must not read, write, or package that sidecar. The editor's runtime playtest uses a disposable `IScenarioPreviewSession`; owners close it with `Dispose`, not a legacy `EndPreview` call.

## Player-Facing Compatibility Text

Use plain language on Nexus pages:

`Requires Sheltered Mod Manager 2.0 or newer. Older SMM 1.2.2/1.3 versions are not supported by this package. Back up saves before switching major SMM versions.`
