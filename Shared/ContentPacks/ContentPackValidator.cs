using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ShelteredModManager.ContentPacks
{
    public static class ContentPackValidator
    {
        public const int SupportedSchemaVersion = 1;

        private static readonly Regex IdentifierPattern =
            new Regex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> Categories = NewSet(new string[]
        {
            "Normal", "Medicine", "Entertainment", "Object", "Tool", "Food", "Water",
            "Weapon", "Ammo", "Armour", "LoadCarrying", "Equipment", "Schematic",
            "Shelter", "ShelterPaint", "Meat", "Embryo", "GasMask"
        });

        private static readonly HashSet<string> Stations = NewSet(new string[]
        {
            "Workbench", "Laboratory", "AmmoPress"
        });

        private static readonly HashSet<string> BehaviorDependentCategories = NewSet(new string[]
        {
            "Object", "Weapon", "Ammo", "Armour", "Equipment", "Schematic",
            "Shelter", "ShelterPaint", "Embryo", "GasMask"
        });

        public static ContentPackValidationResult Validate(
            ContentPackDocument document,
            ContentPackValidationContext context)
        {
            ContentPackValidationResult result = new ContentPackValidationResult();
            if (document == null)
            {
                result.AddError("document.required", "$", "Content-pack document is required.");
                return result;
            }

            ContentPackValidationContext facts = context ?? new ContentPackValidationContext();
            ValidateHeader(document, facts, result);

            List<ContentPackItem> items = document.items ?? new List<ContentPackItem>();
            List<ContentPackRecipe> recipes = document.recipes ?? new List<ContentPackRecipe>();
            HashSet<string> itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> recipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < items.Count; i++)
            {
                ContentPackItem item = items[i];
                if (item != null)
                {
                    ValidateOwnedId(
                        item.id,
                        document.modId,
                        "items[" + i + "].id",
                        "item",
                        itemIds,
                        result);
                }
            }
            for (int i = 0; i < recipes.Count; i++)
            {
                ContentPackRecipe recipe = recipes[i];
                if (recipe != null)
                {
                    ValidateOwnedId(
                        recipe.id,
                        document.modId,
                        "recipes[" + i + "].id",
                        "recipe",
                        recipeIds,
                        result);
                }
            }

            for (int i = 0; i < items.Count; i++)
                ValidateItem(items[i], i, document.modId, facts, itemIds, result);
            for (int i = 0; i < recipes.Count; i++)
                ValidateRecipe(recipes[i], i, document.modId, itemIds, result);

            return result;
        }

        private static void ValidateHeader(
            ContentPackDocument document,
            ContentPackValidationContext context,
            ContentPackValidationResult result)
        {
            if (document.schemaVersion != SupportedSchemaVersion)
            {
                result.AddError(
                    "schema.unsupported",
                    "schemaVersion",
                    "Only content-pack schema version " + SupportedSchemaVersion + " is supported.");
            }

            if (!IsIdentifier(document.modId))
                result.AddError("mod_id.invalid", "modId", "modId must be a lowercase dotted identifier.");

            string expected = (context.ExpectedModId ?? string.Empty).Trim();
            if (expected.Length > 0 &&
                !string.Equals(expected, document.modId, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError(
                    "mod_id.mismatch",
                    "modId",
                    "Content-pack modId must match About/About.json id '" + expected + "'.");
            }
        }

        private static void ValidateItem(
            ContentPackItem item,
            int index,
            string modId,
            ContentPackValidationContext context,
            HashSet<string> itemIds,
            ContentPackValidationResult result)
        {
            string path = "items[" + index + "]";
            if (item == null)
            {
                result.AddError("item.required", path, "Item entry cannot be null.");
                return;
            }

            if (string.IsNullOrEmpty((item.displayName ?? string.Empty).Trim()))
                result.AddError("item.display_name.required", path + ".displayName", "Item displayName is required.");
            if (!Categories.Contains(item.category ?? string.Empty))
                result.AddError("item.category.invalid", path + ".category", "Item category is not supported.");
            else if (BehaviorDependentCategories.Contains(item.category))
            {
                result.AddWarning(
                    "item.category.behavior_required",
                    path + ".category",
                    "This category may require a code plugin for complete game behavior.");
            }

            ValidateRange(item.stackSize, 1, 9999, path + ".stackSize", "item.stack_size", result);
            ValidateRange(item.craftStackSize, 1, 9999, path + ".craftStackSize", "item.craft_stack_size", result);
            ValidateNonnegative(item.tradeValue, path + ".tradeValue", "item.trade_value", result);
            ValidateNonnegative(item.loadCarrySlots, path + ".loadCarrySlots", "item.load_carry_slots", result);
            ValidateNonnegative(item.burnValue, path + ".burnValue", "item.burn_value", result);
            ValidateNonnegative(item.scrapValue, path + ".scrapValue", "item.scrap_value", result);
            ValidateNonnegative(item.baseCraftTime, path + ".baseCraftTime", "item.base_craft_time", result);

            if (item.fabrication != null)
            {
                ValidateNonnegative(item.fabrication.cost, path + ".fabrication.cost", "item.fabrication.cost", result);
                ValidateNonnegative(item.fabrication.timeSeconds, path + ".fabrication.timeSeconds", "item.fabrication.time", result);
            }
            if (item.ration != null)
            {
                ValidateNonnegative(item.ration.value, path + ".ration.value", "item.ration.value", result);
                if (!IsFinite(item.ration.contamination) ||
                    item.ration.contamination < 0f ||
                    item.ration.contamination > 1f)
                {
                    result.AddError(
                        "item.ration.contamination",
                        path + ".ration.contamination",
                        "Value must be between 0 and 1.");
                }
            }
            if (item.rawFood != null && item.rawFood.enabled)
            {
                if (!IsFinite(item.rawFood.cookedHungerMultiplier) ||
                    item.rawFood.cookedHungerMultiplier <= 0f)
                {
                    result.AddError(
                        "item.raw_food.multiplier",
                        path + ".rawFood.cookedHungerMultiplier",
                        "Cooked hunger multiplier must be greater than zero.");
                }
            }

            if (!string.IsNullOrEmpty(item.iconPath))
            {
                ContentPackAssetValidationResult asset = ContentPackPathPolicy.ValidateIcon(
                    context.ModRootPath,
                    item.iconPath,
                    context.ValidateAssetFiles,
                    context.MaximumIconBytes,
                    context.MaximumIconDimension);
                if (!asset.Success)
                    result.AddError("item.icon.invalid", path + ".iconPath", asset.ErrorMessage);
            }

            List<ContentPackIngredient> recycling =
                item.recycling ?? new List<ContentPackIngredient>();
            for (int i = 0; i < recycling.Count; i++)
            {
                ValidateIngredient(
                    recycling[i],
                    path + ".recycling[" + i + "]",
                    modId,
                    itemIds,
                    result);
            }
        }

        private static void ValidateRecipe(
            ContentPackRecipe recipe,
            int index,
            string modId,
            HashSet<string> itemIds,
            ContentPackValidationResult result)
        {
            string path = "recipes[" + index + "]";
            if (recipe == null)
            {
                result.AddError("recipe.required", path, "Recipe entry cannot be null.");
                return;
            }

            ValidateReference(recipe.resultItemId, path + ".resultItemId", modId, itemIds, result);

            if (!Stations.Contains(recipe.station ?? string.Empty))
                result.AddError("recipe.station.invalid", path + ".station", "Recipe station is not supported.");
            ValidateRange(recipe.level, 1, 5, path + ".level", "recipe.level", result);
            if (!IsFinite(recipe.craftTimeSeconds) || recipe.craftTimeSeconds <= 0f)
            {
                result.AddError(
                    "recipe.craft_time",
                    path + ".craftTimeSeconds",
                    "Recipe craft time must be greater than zero.");
            }
            if (recipe.locked && string.IsNullOrEmpty((recipe.unlockFlag ?? string.Empty).Trim()))
            {
                result.AddWarning(
                    "recipe.unlock_flag.missing",
                    path + ".unlockFlag",
                    "Locked recipe has no unlock flag and may remain unavailable.");
            }

            List<ContentPackIngredient> ingredients =
                recipe.ingredients ?? new List<ContentPackIngredient>();
            if (ingredients.Count == 0)
                result.AddError("recipe.ingredients.required", path + ".ingredients", "Recipe needs at least one ingredient.");
            for (int i = 0; i < ingredients.Count; i++)
            {
                ValidateIngredient(
                    ingredients[i],
                    path + ".ingredients[" + i + "]",
                    modId,
                    itemIds,
                    result);
            }
        }

        private static void ValidateIngredient(
            ContentPackIngredient ingredient,
            string path,
            string modId,
            HashSet<string> localItemIds,
            ContentPackValidationResult result)
        {
            if (ingredient == null)
            {
                result.AddError("ingredient.required", path, "Ingredient entry cannot be null.");
                return;
            }

            ValidateReference(ingredient.itemId, path + ".itemId", modId, localItemIds, result);
            if (ingredient.count <= 0)
                result.AddError("ingredient.count", path + ".count", "Ingredient count must be greater than zero.");
        }

        private static void ValidateReference(
            string itemId,
            string path,
            string modId,
            HashSet<string> localItemIds,
            ContentPackValidationResult result)
        {
            string value = (itemId ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                result.AddError("reference.required", path, "Item reference is required.");
                return;
            }
            if (!IsReferenceIdentifier(value))
            {
                result.AddError("reference.invalid", path, "Item reference contains unsupported characters.");
                return;
            }
            if (localItemIds != null && localItemIds.Contains(value))
                return;

            string ownedPrefix = (modId ?? string.Empty) + ".";
            if (modId != null && value.StartsWith(ownedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("reference.local_missing", path, "Local item reference does not match a declared item.");
                return;
            }

            result.AddWarning(
                "reference.external",
                path,
                "External item reference will be resolved against vanilla items or enabled dependencies at runtime.");
        }

        private static void ValidateOwnedId(
            string id,
            string modId,
            string path,
            string kind,
            HashSet<string> ids,
            ContentPackValidationResult result)
        {
            string value = (id ?? string.Empty).Trim();
            if (!IsIdentifier(value))
            {
                result.AddError(kind + ".id.invalid", path, "ID must be a lowercase dotted identifier.");
                return;
            }
            if (string.IsNullOrEmpty(modId) ||
                !value.StartsWith(modId + ".", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError(kind + ".id.namespace", path, "ID must begin with modId followed by a dot.");
            }
            if (!ids.Add(value))
                result.AddError(kind + ".id.duplicate", path, "ID is duplicated in this content pack.");
        }

        private static bool IsIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf('.') > 0 &&
                IdentifierPattern.IsMatch(value);
        }

        private static bool IsReferenceIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-';
                if (!valid)
                    return false;
            }
            return true;
        }

        private static void ValidateRange(
            int value,
            int minimum,
            int maximum,
            string path,
            string code,
            ContentPackValidationResult result)
        {
            if (value < minimum || value > maximum)
                result.AddError(code, path, "Value must be between " + minimum + " and " + maximum + ".");
        }

        private static void ValidateNonnegative(
            int value,
            string path,
            string code,
            ContentPackValidationResult result)
        {
            if (value < 0)
                result.AddError(code, path, "Value cannot be negative.");
        }

        private static void ValidateNonnegative(
            float value,
            string path,
            string code,
            ContentPackValidationResult result)
        {
            if (!IsFinite(value) || value < 0f)
                result.AddError(code, path, "Value must be finite and cannot be negative.");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static HashSet<string> NewSet(string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }
    }
}
