# Codex note - fridge-backed cooking docs

Timestamp: 2026-05-07

Updated documentation only:
- `documentation/ShelteredAPI_Runtime_UI_Stores_Guide.md`
- `documentation/ShelteredAPI_Content_Guide.md`
- `documentation/API_Signatures_Reference.md`

Scope:
- Added Dev/API-preview warnings for runtime stores and cooking stations.
- Documented the fridge-backed cooking flow using a mod-owned object store keyed from a freezer/fridge object.
- Clarified that vanilla `Obj_Freezer` remains a meat/desperate-meat adapter and should not be patched for arbitrary custom item types.
- Added snippets for nearest fridge lookup, `ShelteredStores.ForObject(...)`, container UI, `ShelteredCooking.RegisterStation(...)`, `Meat x1 -> Ration x1`, and timed `cook_food`/`Rummage` job options.
