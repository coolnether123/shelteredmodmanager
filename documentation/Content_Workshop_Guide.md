# Content Workshop guide

Content Workshop is the desktop manager's data-driven authoring workspace for custom Sheltered items, recipes, and item icons. It creates normal mod packages and does not require a custom scenario or a compiled plugin for the supported static fields.

Use a code plugin when an item needs custom runtime behavior. Content Workshop schema version 1 defines static item data, recipes, costs, and assets; categories such as weapons, armour, equipment, schematics, shelter objects, embryos, and gas masks may still need code to behave completely.

## Authoring workflow

1. Open **Content workshop** in `SMM\Manager.exe`.
2. Select **New pack**. Packs are kept in `Documents\Sheltered Mod Manager\Workshop`, and the start screen lists them for one-click opening. The pack's folder follows its name until the first save.
3. Give the pack a name and add yourself under **Authors**. The pack ID (for example `yourname.survivalitems`) and the item and recipe IDs fill themselves in from those names. They stop changing once the pack is first installed or exported, because saves and other mods refer to them.
4. Add items and recipes. Choose what a recipe makes, and its materials, from lists of your own items and base-game items, or type the ID of an item from another mod.
5. Import a PNG or draw a 32x32 icon in **Icon Studio**.
6. On **Install & share**, select **Run check** and fix anything it lists. Double-click a result to jump to the field.
7. Select **Install to my game** to copy the pack into the mods folder and turn it on, or **Save as ZIP** to share it.

**Developer mode** (Settings) adds the tools mod makers need: New pack asks for an empty folder; the pack, item and recipe ID fields are shown; items gain burn and scrap value, carry slots, crafting and fabrication, contamination and raw-food fields; recipes gain unique-result and unlock settings; and **Export folder** is available.

The editor warns before discarding an unsaved project. Local install never overwrites an existing mod; remove or back up the installed copy explicitly before installing a replacement.

## Package layout

Content Workshop exports only its owned package roots:

```text
com.example.survivalitems/
  About/
    About.json
  Content/
    content-pack.json
  Assets/
    Icons/
      field_ration.png
  README.md
```

Content Workshop does not export or install an `Assemblies/` folder. If the mod also needs code, export the content pack and add the plugin assembly through the normal mod packaging workflow. Do not reopen that combined package in Content Workshop and expect it to preserve the assembly.

## Content-pack contract

The runtime reads exactly `Content/content-pack.json`. Schema version 1 has this top-level shape:

```json
{
  "schemaVersion": 1,
  "modId": "com.example.survivalitems",
  "items": [],
  "recipes": []
}
```

Item and recipe IDs must:

- use lowercase letters, numbers, dots, underscores, or hyphens;
- begin with the package `modId` followed by a dot;
- be unique within their respective collection.

An item can define:

- display name, description, category, and icon path;
- stack size, trade value, burn value, scrap value, and base craft time;
- craft stack size and fabrication cost/time;
- ration value and contamination;
- load-carry slots and raw-food cooking multiplier;
- recycling outputs.

A recipe can define:

- result item ID;
- workbench, laboratory, or ammo-press station;
- level, craft time, unique/locked state, and unlock flag;
- ingredient item IDs and quantities.

References to items declared in the same pack are checked immediately. External references are allowed with a warning and are resolved against vanilla content or enabled dependencies at runtime.

## Icons and pixel editing

Icons must be PNG files contained inside the project, no larger than 4 MiB or 2048 pixels on either axis. Content Workshop normalizes imported images into `Assets/Icons`.

The icon editor supports paint, erase, color pick, undo, redo, zoom, PNG import, and saving to the selected item. Its document, clipboard, selection, and history engine is the same host-neutral pixel core used by in-game scenario sprite authoring; WinForms and Unity provide only their respective image adapters and save destinations.

## Validation and runtime loading

Export and local install require a valid document and valid assets. At game startup, ShelteredAPI:

1. processes content packs in normal dependency/load order;
2. validates the document and assets before registration;
3. registers all items and recipes as one batch;
4. rolls the whole batch back if any registration fails;
5. logs and rejects a broken pack without preventing unrelated mods from activating.

This makes content-only and hybrid mods follow the same load-order and compatibility boundaries as compiled plugins.
