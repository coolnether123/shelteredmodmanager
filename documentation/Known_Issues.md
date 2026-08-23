# Known issues

SMM 2.0 is still in prerelease review. The repository does not yet have a final `v2.0.0` tag.

## Nexus sign-in and direct install

Nexus has not issued the public OAuth client ID. Metadata browsing works without sign-in, but direct install, update, and reinstall remain unavailable until registration is complete. The manager reports that Nexus sign-in is not available.

The implemented installer accepts ZIP archives only. It rejects missing `About/About.json`, duplicate mod IDs, reserved folder names, unsafe archive paths, and unwritable mod folders.

## Mod compatibility

Mods built for SMM 1.2.2 or the old 1.3 beta may need a 2.0 rebuild. Do not treat a mod as save-safe because the manager can discover it. Follow the [modder migration guide](For_Modders_2.0_API_Migration.md) and test with a disposable save.

## Scenario editor

`ShelteredScenarioEditor.dll` is an optional preview component. The `ShelteredScenarioEditor.Enabled` manager option defaults off. Installed custom scenario browsing and playback remain available when the editor is disabled or absent.

## Report a problem

Include:

- the storefront and game executable path;
- the SMM version;
- the mod list and load order;
- the save type and slot;
- whether `ShelteredScenarioEditor.dll` is present and enabled;
- reproduction steps;
- `SMM\mod_manager.log`.
