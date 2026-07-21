using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredModManager.ContentPacks;

namespace ShelteredAPI.Content.Packs
{
    internal sealed class ContentPackMappedBatch
    {
        public readonly List<ItemDefinition> Items = new List<ItemDefinition>();
        public readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>();
    }

    internal sealed class ContentPackMapper
    {
        public ContentPackMappedBatch Map(ModEntry owner, ContentPackDocument document)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            if (document == null)
                throw new ArgumentNullException("document");

            ContentPackMappedBatch batch = new ContentPackMappedBatch();
            List<ContentPackItem> items = document.items ?? new List<ContentPackItem>();
            for (int i = 0; i < items.Count; i++)
                batch.Items.Add(MapItem(owner, items[i]));

            List<ContentPackRecipe> recipes = document.recipes ?? new List<ContentPackRecipe>();
            for (int i = 0; i < recipes.Count; i++)
                batch.Recipes.Add(MapRecipe(recipes[i]));

            return batch;
        }

        private static ItemDefinition MapItem(ModEntry owner, ContentPackItem source)
        {
            ItemDefinition item = new ItemDefinition
            {
                Id = source.id,
                DisplayNameText = source.displayName,
                DescriptionText = source.description,
                DisplayName = source.displayName,
                Description = source.description,
                IconPath = source.iconPath,
                Category = (ItemCategory)Enum.Parse(typeof(ItemCategory), source.category, true),
                StackSize = source.stackSize,
                TradeValue = source.tradeValue,
                BurnValue = source.burnValue,
                ScrapValue = source.scrapValue,
                BaseCraftTime = source.baseCraftTime,
                CraftStackSize = source.craftStackSize,
                LoadCarrySlots = source.loadCarrySlots
            };

            if (source.fabrication != null)
            {
                item.FabricationCost = source.fabrication.cost;
                item.BaseFabricationTime = source.fabrication.timeSeconds;
            }
            if (source.ration != null)
            {
                item.RationValue = source.ration.value;
                item.Contamination = source.ration.contamination;
            }
            if (source.rawFood != null)
            {
                item.IsRawFood = source.rawFood.enabled;
                item.CookedHungerMultiplier = source.rawFood.cookedHungerMultiplier;
            }

            List<ContentPackIngredient> recycling =
                source.recycling ?? new List<ContentPackIngredient>();
            for (int i = 0; i < recycling.Count; i++)
            {
                item.RecyclingIngredients.Add(new RecipeIngredient
                {
                    ItemId = recycling[i].itemId,
                    Count = recycling[i].count
                });
            }

            if (!string.IsNullOrEmpty(source.iconPath))
                item.Icon = AssetLoader.LoadSprite(owner.RootPath, source.iconPath);

            return item;
        }

        private static RecipeDefinition MapRecipe(ContentPackRecipe source)
        {
            RecipeDefinition recipe = new RecipeDefinition
            {
                Id = source.id,
                ResultItemId = source.resultItemId,
                Station = (CraftStation)Enum.Parse(typeof(CraftStation), source.station, true),
                Level = source.level,
                CraftTimeSeconds = source.craftTimeSeconds,
                Unique = source.unique,
                Locked = source.locked,
                UnlockFlag = source.unlockFlag
            };

            List<ContentPackIngredient> ingredients =
                source.ingredients ?? new List<ContentPackIngredient>();
            for (int i = 0; i < ingredients.Count; i++)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    ItemId = ingredients[i].itemId,
                    Count = ingredients[i].count
                });
            }

            return recipe;
        }
    }
}
