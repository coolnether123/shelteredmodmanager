# SMM 2.0 Migration

Sheltered Mod Manager 2.0 is a breaking mod-system update. Mods built for SMM 1.2.2 or the old 1.3 beta line may not work until rebuilt for ModAPI/ShelteredAPI 2.0.

Mod authors rebuilding packages should use [For Modders: 2.0 API Migration](For_Modders_2.0_API_Migration.md) and the canonical [ModAPI/ShelteredAPI assembly boundary](README.md#assembly-boundary-canonical). This page is the player upgrade checklist.

## For Players

- Back up saves before switching to SMM 2.0.
- Use 2.0 versions of mods when they are available.
- Do not assume a 1.2.2 or 1.3 mod is safe just because it appears in the manager.
- Read the compatibility status in the mod details panel before enabling a mod.
- Read the save verification dialog before loading old saves.

If a save used older API mods, update those mods first. If no 2.0 version exists yet, treat the save as at risk and test on a copied save.

## Upgrade Checklist

1. Install SMM 2.0 into a clean Sheltered test install.
2. Launch once with no mods enabled.
3. Add updated 2.0 public mods one at a time.
4. Launch after each mod and check `SMM\mod_manager.log`.
5. Enable the full public mod set and test a new survival save.
6. Open old saves and confirm warnings are clear before loading.
7. Keep the old SMM/mod package available until important saves have been verified.

## Expected Compatibility Warning

When SMM 2.0 sees a mod built against an older API line, it should warn:

`This mod was built for the older SMM API line. SMM 2.0 moved Sheltered APIs into ShelteredAPI.dll; use a 2.0 version of the mod before loading important saves.`

That warning means the manager found a likely 1.x mod. It does not prove the mod is broken, but it does mean the mod needs a rebuild or focused testing before normal play.
