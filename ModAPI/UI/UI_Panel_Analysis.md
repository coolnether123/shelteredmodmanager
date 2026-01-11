# UI Panel Analysis for Custom Item Support

## Summary
This document analyzes all UI panels in Sheltered that display items to determine which ones need patches to support custom modded items (IDs >= 10000).

## Why Patches Are Needed
The game's original code uses `Enum.GetValues(typeof(ItemManager.ItemType))` to iterate through all item types. This returns only the hardcoded enum values from the compiled game code, which means custom items with high IDs (10000+) are skipped entirely.

## Patched Panels ✅

### 1. StoragePanel
**File:** `UI/StoragePanel.cs`  
**Method:** `OnShow()` (line 50)  
**Issue:** Uses `Enum.GetValues` to populate storage bin UI  
**Fix:** `UIPatches.Postfix_StoragePanel_OnShow` - Appends custom items from `ContentInjector.RegisteredTypes`

### 2. RecyclingPanel  
**File:** `UI/RecyclingPanel.cs`  
**Method:** `OnShow()` (line 121)  
**Issue:** Uses `Enum.GetValues` to find recyclable items  
**Fix:** `UIPatches.Postfix_RecyclingPanel_OnShow` - Adds custom recyclable items

### 3. ItemFabricationPanel
**File:** `UI/ItemFabricationPanel.cs`  
**Method:** `OnShow()` (lines 175, 197)  
**Issue:** Uses `Enum.GetValues` twice (for salvage items and fabrication selection)  
**Fix:** `UIPatches.Postfix_ItemFabricationPanel_OnShow` - Adds custom items to both grids

## Panels That Don't Need Patches ✅

### 4. TradingPanel
**File:** `UI/TradingPanel.cs`  
**Method:** `AddPlayerItems(List<ItemStack> items, int maxCapacity)` (line 147)  
**Why Safe:** Receives pre-populated `List<ItemStack>` from external code. Custom items are already in this list as ItemStack objects.

### 5. ExpeditionLoadout
**File:** `Expeditions/ExpeditionLoadout.cs`  
**Method:** `AddStorageItems()` (line 301)  
**Why Safe:** Gets items from `InventoryManager.Instance.GetItems()` which returns a List<ItemStack> that already includes custom items.

### 6. ItemTransferPanel
**File:** `UI/ItemTransferPanel.cs`  
**Method:** `GetShelterItems(int inventoryIndex)` (line 839)  
**Why Safe:** Returns `InventoryManager.Instance.GetItems()` which includes custom items.

### 7. UI_DebugInventory
**File:** `UI/UI_DebugInventory.cs`  
**Method:** `Start()` (line 23)  
**Why Safe:** Debug-only panel, not used in normal gameplay. Can be ignored.

## Technical Details

### The Safe Pattern
Panels that use these methods are safe:
- `InventoryManager.Instance.GetItems()` - Returns List<ItemStack> with all items
- Pre-populated `List<ItemStack>` parameters - Already includes custom items
- Direct item type parameters - Caller already knows the specific type

### The Unsafe Pattern  
Panels using this pattern need patches:
```csharp
Array values = Enum.GetValues(typeof(ItemManager.ItemType));
for (int index = 0; index < values.Length; ++index)
{
    ItemManager.ItemType type = (ItemManager.ItemType)values.GetValue(index);
    // ... use type ...
}
```

### The Fix Pattern
For each unsafe panel, we use Harmony Postfix patches:
```csharp
[HarmonyPatch(typeof(PanelName), "OnShow")]
[HarmonyPostfix]
static void Postfix_PanelName_OnShow(PanelName __instance)
{
    foreach (var type in ContentInjector.RegisteredTypes)
    {
        // Check if item should be displayed
        // Add to appropriate grid
    }
}
```

## Conclusion
✅ **All critical UI panels are now covered**

The three main storage/crafting panels (Storage, Recycling, ItemFabrication) now have patches. All other item-displaying panels use safe APIs that already support custom items.

Custom items will now:
- Appear in the storage bin
- Be available for recycling (if configured with ItemBaseParts)
- Be available for fabrication (if configured with FabricationCost)
- Work correctly in trading, expeditions, and item transfers
