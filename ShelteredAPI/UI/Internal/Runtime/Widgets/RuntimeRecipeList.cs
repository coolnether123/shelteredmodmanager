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
            Build(parent, recipes, isAvailable, onCraft, depth, null);
        }

        public static void Build(GameObject parent, IList<CraftingUiRecipe> recipes, Func<CraftingUiRecipe, bool> isAvailable, Action<CraftingUiRecipe> onCraft, int depth, RuntimeRecipeListOptions options)
        {
            if (parent == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(parent);
            RuntimePanelChromeLayout layout = options != null && options.Layout != null ? options.Layout : RuntimePanelChromeLayout.Default;
            RuntimePanelStyle style = options != null ? options.Style : null;
            if (recipes == null || recipes.Count == 0)
            {
                string emptyText = options != null && !string.IsNullOrEmpty(options.EmptyText) ? options.EmptyText : "No recipes";
                RuntimeWidgetUtil.CreateLabel(parent, emptyText, layout.ContentWidth, 32, 20, new Vector3(0f, layout.ContentTopY - 52f, 0f), NGUIText.Alignment.Center, depth, style != null ? style.TextColor : null);
                return;
            }

            int rowWidth = Math.Max(360, layout.ContentWidth);
            List<GameObject> rows = new List<GameObject>();
            for (int i = 0; i < recipes.Count; i++)
            {
                CraftingUiRecipe recipe = recipes[i];
                bool available = isAvailable == null || isAvailable(recipe);
                GameObject row = RuntimeWidgetUtil.CreateChild(parent, "RecipeRow_" + i, new Vector3(0f, layout.ContentTopY - i * 66f, 0f));
                RuntimeWidgetUtil.CreateBox(row, "Background", rowWidth, 58, available ? new Color(0.12f, 0.14f, 0.13f, 0.94f) : new Color(0.1f, 0.1f, 0.1f, 0.7f), Vector3.zero, depth + i * 4);
                if (recipe != null && recipe.Icon != null)
                    RuntimeWidgetUtil.CreateSprite(row, "Icon", recipe.Icon, 34, 34, new Vector3(-rowWidth / 2f + 28f, 7f, 0f), depth + i * 4 + 1);

                int textWidth = Math.Max(180, rowWidth - 210);
                float textX = -rowWidth / 2f + (recipe != null && recipe.Icon != null ? 56f : 20f) + (textWidth / 2f);
                RuntimeWidgetUtil.CreateLabel(row, recipe != null ? recipe.DisplayName : string.Empty, textWidth, 20, 18, new Vector3(textX, 14f, 0f), NGUIText.Alignment.Left, depth + i * 4 + 1, style != null ? style.TextColor : null);
                string detail = BuildDetail(recipe, available, options);
                RuntimeWidgetUtil.CreateLabel(row, detail, textWidth, 16, 14, new Vector3(textX, -6f, 0f), NGUIText.Alignment.Left, depth + i * 4 + 1, style != null ? style.TextColor : null);
                string output = BuildOutput(recipe);
                RuntimeWidgetUtil.CreateLabel(row, output, 72, 24, 16, new Vector3(rowWidth / 2f - 126f, 5f, 0f), NGUIText.Alignment.Right, depth + i * 4 + 1, style != null ? style.TextColor : null);
                string craftText = ResolveCraftText(recipe, options);
                CraftingUiRecipe captured = recipe;
                bool capturedAvailable = available;
                RuntimeButton.Create(row, "Craft", craftText, 82, 28, new Vector3(rowWidth / 2f - 48f, 1f, 0f), depth + i * 4 + 2, available && onCraft != null, delegate
                {
                    if (capturedAvailable && onCraft != null)
                        onCraft(captured);
                }, style);
                RuntimeWidgetUtil.EnsureCollider(row, rowWidth, 58);
                rows.Add(row);
            }

            RuntimeScrollView.Attach(parent, rows, layout.ContentTopY, 66f, layout.Bottom + 110f, layout.ContentTopY, layout.Left + 20f, layout.Right - 20f);
        }

        private static string ResolveCraftText(CraftingUiRecipe recipe, RuntimeRecipeListOptions options)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.CraftButtonText))
                return recipe.CraftButtonText;
            if (options != null && !string.IsNullOrEmpty(options.CraftButtonText))
                return options.CraftButtonText;
            return "Craft";
        }

        private static string BuildOutput(CraftingUiRecipe recipe)
        {
            if (recipe == null || recipe.OutputCount <= 0)
                return string.Empty;
            if (!string.IsNullOrEmpty(recipe.OutputCountText))
                return recipe.OutputCountText;
            return "x" + recipe.OutputCount;
        }

        private static string BuildDetail(CraftingUiRecipe recipe, bool available, RuntimeRecipeListOptions options)
        {
            if (!available)
            {
                if (recipe != null && !string.IsNullOrEmpty(recipe.UnavailableText))
                    return recipe.UnavailableText;
                if (options != null && options.GetUnavailableReason != null)
                {
                    string reason = options.GetUnavailableReason(recipe);
                    if (!string.IsNullOrEmpty(reason))
                        return reason;
                }
            }

            if (recipe != null && !string.IsNullOrEmpty(recipe.Subtitle))
                return recipe.Subtitle;

            if (recipe == null || recipe.Ingredients == null || recipe.Ingredients.Count == 0)
                return string.Empty;

            List<string> parts = new List<string>();
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftingUiIngredient ingredient = recipe.Ingredients[i];
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId))
                    continue;
                parts.Add(ingredient.ItemId + " x" + Math.Max(1, ingredient.Count));
            }

            return string.Join(", ", parts.ToArray());
        }
    }

    internal sealed class RuntimeRecipeListOptions
    {
        public string EmptyText;
        public string CraftButtonText;
        public Func<CraftingUiRecipe, string> GetUnavailableReason;
        public RuntimePanelChromeLayout Layout;
        public RuntimePanelStyle Style;
    }
}
