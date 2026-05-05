using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeRecipeList
    {
        public static void Build(GameObject parent, IList<CraftingUiRecipe> recipes, Func<CraftingUiRecipe, bool> isAvailable, Action<CraftingUiRecipe> onCraft, int depth)
        {
            if (parent == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(parent);
            if (recipes == null || recipes.Count == 0)
            {
                RuntimeWidgetUtil.CreateLabel(parent, "No recipes", 540, 32, 20, new Vector3(0f, 76f, 0f), NGUIText.Alignment.Center, depth);
                return;
            }

            for (int i = 0; i < recipes.Count; i++)
            {
                CraftingUiRecipe recipe = recipes[i];
                bool available = isAvailable == null || isAvailable(recipe);
                GameObject row = RuntimeWidgetUtil.CreateChild(parent, "RecipeRow_" + i, new Vector3(0f, 128f - i * 42f, 0f));
                RuntimeWidgetUtil.CreateBox(row, "Background", 560, 36, available ? new Color(0.12f, 0.14f, 0.13f, 0.94f) : new Color(0.1f, 0.1f, 0.1f, 0.7f), Vector3.zero, depth + i * 3);
                RuntimeWidgetUtil.CreateLabel(row, recipe != null ? recipe.DisplayName : string.Empty, 360, 28, 18, new Vector3(-92f, -1f, 0f), NGUIText.Alignment.Left, depth + i * 3 + 1);
                RuntimeButton.Create(row, "Craft", "Craft", 82, 26, new Vector3(228f, 0f, 0f), depth + i * 3 + 2, available && onCraft != null, delegate
                {
                    if (available && onCraft != null)
                        onCraft(recipe);
                });
            }
        }
    }
}
