# Known Issues

This list is for SMM 2.0 release tracking.

## Release Scope

- SMM 2.0 is the stable release line for the split ModAPI/ShelteredAPI contract.
- Custom scenario browser/editor features are experimental.
- Nexus publish tools are experimental and hidden unless explicitly enabled.
- Older 1.2.2 or 1.3 mods may require a 2.0 rebuild before they are safe for saves.

## Mods Needing Extra Verification

- Family Expansion and Deep Expansion need rebuilt 2.0 packages and smoke tests before they should be advertised as compatible.
- Faction Overhaul should not be advertised as stable with SMM 2.0 unless its runtime checklist passes on real saves.
- Expanded Map Sizes needs real save/runtime testing before it should be advertised as fully compatible.

## Nexus Install Notes

- Direct Nexus install currently supports ZIP archives.
- Direct Nexus install requires a Nexus API key and can still be denied by Nexus account, file, or app policy.
- Install/update replaces only one direct mod folder under the configured `mods` folder.
- Packages with missing `About/About.json`, duplicate mod IDs, reserved folder names, or unsafe archive paths are rejected.
- For the current GOG staging install, verify the manager can write to `C:\Program Files (x86)\GOG Galaxy\Games\Sheltered\mods` before running download/update smoke tests.

## Report Format

Bug reports should include:

- Storefront and game executable path.
- SMM version.
- Mod list and load order.
- Save type and slot.
- Whether the custom scenario editor was enabled.
- Reproduction steps.
- `SMM\mod_manager.log`.
