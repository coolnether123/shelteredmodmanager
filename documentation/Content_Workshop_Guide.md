# Content Workshop Guide

Content Workshop is the desktop manager's data-driven authoring workspace for custom Sheltered items, recipes, and item icons. It creates normal mod packages and does not require a custom scenario or a compiled plugin for the supported static fields.

Use a code plugin when an item needs custom runtime behavior. Content Workshop schema version 1 defines static item data, recipes, costs, and assets; categories such as weapons, armour, equipment, schematics, shelter objects, embryos, and gas masks may still need code to behave completely.

## Authoring workflow

1. Open **Content Workshop** in `SMM\Manager.exe`.
2. Select **New** and choose an empty project folder.
3. Set a lowercase dotted mod ID, such as `com.example.survivalitems`.
4. Add items and recipes.
5. Import a PNG or create/edit a 32x32 icon in the pixel editor.
6. Select **Validate** and resolve all errors.
7. Export a mod folder or ZIP, or install it into the manager's configured mods folder.

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

`Assemblies/` is optional. A content-only package has no DLL. A hybrid mod can add an ordinary plugin assembly for behavior that the data schema does not describe.

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
