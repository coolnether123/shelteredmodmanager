using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using ShelteredAPI.Storage;
using ShelteredAPI.UI.Runtime;

namespace ShelteredAPI.Workstations
{
    /// <summary>
    /// High-level API for mod-owned cooking/crafting workflows that use public stores and runtime UI.
    /// </summary>
    public static class ShelteredCooking
    {
        public static RuntimeUiHandle Open(CookingStationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            IItemStore ingredientStore = request.IngredientStore ?? ShelteredStores.ForInventory();
            IItemStore outputStore = request.OutputStore ?? ShelteredStores.ForInventory();

            CraftingUiRequest uiRequest = new CraftingUiRequest
            {
                PanelId = request.PanelId,
                Title = string.IsNullOrEmpty(request.Title) ? "Cooking" : request.Title,
                OwnerId = request.OwnerId,
                PanelOptions = request.PanelOptions,
                EmptyText = "No recipes",
                CraftButtonText = "Cook",
                RefreshEveryFrame = request.RefreshEveryFrame,
                RecipeSource = delegate { return BuildUiRecipes(ResolveRecipes(request)); },
                IsAvailable = recipe => IsAvailable(recipe, ingredientStore, outputStore, request),
                GetUnavailableReason = recipe => ResolveUnavailableReason(recipe, ingredientStore, outputStore, request),
                OnCraftRequested = craft => ExecuteCraft(craft, request, ingredientStore, outputStore),
                OnClosed = request.OnClosed
            };

            return ShelteredRuntimeUI.OpenCrafting(uiRequest);
        }

        public static IDisposable RegisterStation(CookingStationRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException("registration");

            return ShelteredRuntimeUI.RegisterObjectPanel(new ObjectPanelRegistration
            {
                ObjectId = registration.ObjectId,
                ObjectType = registration.ObjectType,
                InteractionId = registration.InteractionId,
                InteractionText = registration.InteractionText,
                Priority = registration.Priority,
                CanOpen = context =>
                {
                    CookingStationContext stationContext = ToStationContext(registration, context);
                    return registration.CanOpen == null || registration.CanOpen(stationContext);
                },
                Open = context =>
                {
                    CookingStationContext stationContext = ToStationContext(registration, context);
                    Func<IList<CookingStationRecipe>> recipeSource = null;
                    if (registration.RecipeSource != null)
                        recipeSource = delegate { return registration.RecipeSource(stationContext); };

                    return Open(new CookingStationRequest
                    {
                        OwnerId = registration.OwnerId,
                        PanelId = registration.PanelId,
                        Title = registration.Title,
                        PanelOptions = registration.PanelOptions,
                        IngredientStore = registration.IngredientStore != null ? registration.IngredientStore(stationContext) : ShelteredStores.ForInventory(),
                        OutputStore = registration.OutputStore != null ? registration.OutputStore(stationContext) : ShelteredStores.ForInventory(),
                        Worker = ResolveWorker(registration, stationContext),
                        WorkstationObject = registration.WorkstationObject != null ? registration.WorkstationObject(stationContext) : stationContext.TargetObject,
                        JobOptions = registration.JobOptions,
                        Recipes = registration.Recipes,
                        RecipeSource = recipeSource,
                        ConsumeIngredients = registration.ConsumeIngredients,
                        RefreshEveryFrame = registration.RefreshEveryFrame,
                        GetUnavailableReason = registration.GetUnavailableReason,
                        OnCraftQueued = registration.OnCraftQueued,
                        OnCrafted = registration.OnCrafted,
                        OnCraftFailed = registration.OnCraftFailed,
                        OnClosed = registration.OnClosed
                    });
                }
            });
        }

        private static CookingStationContext ToStationContext(CookingStationRegistration registration, ObjectPanelContext context)
        {
            return new CookingStationContext(
                ResolveObjectId(registration, context),
                context != null ? context.TargetObject : null,
                context != null ? context.SelectedMember : null);
        }

        private static string ResolveObjectId(CookingStationRegistration registration, ObjectPanelContext context)
        {
            if (context != null && !string.IsNullOrEmpty(context.ObjectId))
                return context.ObjectId;

            return registration != null ? registration.ObjectId : null;
        }

        private static FamilyMember ResolveWorker(CookingStationRegistration registration, CookingStationContext context)
        {
            FamilyMember worker = registration != null && registration.Worker != null ? registration.Worker(context) : null;
            if (worker != null)
                return worker;

            if (context != null && context.SelectedMember != null && context.SelectedMember.IsIdle())
                return context.SelectedMember;

            return FindIdleWorker();
        }

        public static FamilyMember FindIdleWorker()
        {
            if (FamilyManager.Instance == null)
                return null;

            List<FamilyMember> members = FamilyManager.Instance.GetAllFamilyMembers();
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member != null && member.IsIdle())
                    return member;
            }

            return null;
        }

        private static IList<CookingStationRecipe> ResolveRecipes(CookingStationRequest request)
        {
            if (request == null)
                return new List<CookingStationRecipe>();
            if (request.RecipeSource != null)
            {
                IList<CookingStationRecipe> recipes = request.RecipeSource();
                return recipes ?? new List<CookingStationRecipe>();
            }
            return request.Recipes ?? new List<CookingStationRecipe>();
        }

        private static IList<CraftingUiRecipe> BuildUiRecipes(IList<CookingStationRecipe> recipes)
        {
            List<CraftingUiRecipe> uiRecipes = new List<CraftingUiRecipe>();
            for (int i = 0; recipes != null && i < recipes.Count; i++)
            {
                CookingStationRecipe recipe = recipes[i];
                if (recipe == null)
                    continue;

                List<CraftingUiIngredient> ingredients = new List<CraftingUiIngredient>();
                for (int j = 0; recipe.Ingredients != null && j < recipe.Ingredients.Count; j++)
                {
                    RecipeIngredient ingredient = recipe.Ingredients[j];
                    if (ingredient == null)
                        continue;
                    ingredients.Add(new CraftingUiIngredient { ItemId = ingredient.ItemId, Count = ingredient.Count });
                }

                uiRecipes.Add(new CraftingUiRecipe
                {
                    RecipeId = recipe.RecipeId,
                    DisplayName = recipe.DisplayName,
                    Subtitle = recipe.Subtitle,
                    Ingredients = ingredients,
                    OutputItemId = recipe.OutputItemId,
                    OutputCount = recipe.OutputCount,
                    OutputCountText = recipe.OutputCountText,
                    Icon = recipe.Icon,
                    Tag = recipe
                });
            }
            return uiRecipes;
        }

        private static bool IsAvailable(CraftingUiRecipe uiRecipe, IItemStore ingredientStore, IItemStore outputStore, CookingStationRequest request)
        {
            CookingStationRecipe recipe = uiRecipe != null ? uiRecipe.Tag as CookingStationRecipe : null;
            if (recipe == null)
                return false;

            if (!HasIngredients(recipe, ingredientStore))
                return false;

            if (!string.IsNullOrEmpty(recipe.OutputItemId) && outputStore != null)
                return outputStore.CanAdd(recipe.OutputItemId, Math.Max(1, recipe.OutputCount));

            return true;
        }

        private static string ResolveUnavailableReason(CraftingUiRecipe uiRecipe, IItemStore ingredientStore, IItemStore outputStore, CookingStationRequest request)
        {
            CookingStationRecipe recipe = uiRecipe != null ? uiRecipe.Tag as CookingStationRecipe : null;
            if (recipe == null)
                return "Recipe is unavailable";

            if (request != null && request.GetUnavailableReason != null)
            {
                string custom = request.GetUnavailableReason(recipe);
                if (!string.IsNullOrEmpty(custom))
                    return custom;
            }

            if (!HasIngredients(recipe, ingredientStore))
                return "Missing ingredients";

            if (!string.IsNullOrEmpty(recipe.OutputItemId) && outputStore != null && !outputStore.CanAdd(recipe.OutputItemId, Math.Max(1, recipe.OutputCount)))
                return "Output store is full";

            return string.Empty;
        }

        private static bool HasIngredients(CookingStationRecipe recipe, IItemStore store)
        {
            if (recipe == null || store == null)
                return false;

            for (int i = 0; recipe.Ingredients != null && i < recipe.Ingredients.Count; i++)
            {
                RecipeIngredient ingredient = recipe.Ingredients[i];
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId))
                    continue;
                if (store.GetCount(ingredient.ItemId) < Math.Max(1, ingredient.Count))
                    return false;
            }
            return true;
        }

        private static void ExecuteCraft(CraftingUiCraftContext craft, CookingStationRequest request, IItemStore ingredientStore, IItemStore outputStore)
        {
            CookingStationRecipe recipe = craft != null && craft.Recipe != null ? craft.Recipe.Tag as CookingStationRecipe : null;
            CookingCraftContext context = new CookingCraftContext(recipe, ingredientStore, outputStore, craft != null ? craft.Panel : null);
            if (recipe == null)
            {
                context.Result = ItemTransferResult.Failed(null, 0, "Recipe is unavailable");
                NotifyFailed(request, context);
                return;
            }

            if (ShouldQueueJob(recipe, request))
            {
                QueueCraftJob(recipe, request, ingredientStore, outputStore, context);
                return;
            }

            ItemTransferResult result = ApplyRecipe(recipe, request, ingredientStore, outputStore);
            context.Result = result;
            if (result.Success)
            {
                if (request != null && request.OnCrafted != null)
                    request.OnCrafted(context);
                if (context.Panel != null)
                    context.Panel.Refresh();
            }
            else
            {
                NotifyFailed(request, context);
            }
        }

        private static bool ShouldQueueJob(CookingStationRecipe recipe, CookingStationRequest request)
        {
            return recipe != null
                && request != null
                && request.JobOptions != null
                && request.JobOptions.Enabled;
        }

        private static void QueueCraftJob(CookingStationRecipe recipe, CookingStationRequest request, IItemStore ingredientStore, IItemStore outputStore, CookingCraftContext context)
        {
            FamilyMember worker = request != null ? request.Worker : null;
            Obj_Base workstationObject = request != null ? request.WorkstationObject : null;
            CookingStationJobOptions options = request != null ? request.JobOptions : null;

            context.Worker = worker;
            context.WorkstationObject = workstationObject;

            if (worker == null)
            {
                context.Result = ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "No idle worker is available");
                NotifyFailed(request, context);
                return;
            }
            if (workstationObject == null)
            {
                context.Result = ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Workstation object is required");
                NotifyFailed(request, context);
                return;
            }
            if (!HasIngredients(recipe, ingredientStore))
            {
                context.Result = ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Missing ingredients");
                NotifyFailed(request, context);
                return;
            }

            int outputCount = Math.Max(1, recipe.OutputCount);
            if (!string.IsNullOrEmpty(recipe.OutputItemId) && outputStore != null && !outputStore.CanAdd(recipe.OutputItemId, outputCount))
            {
                context.Result = ItemTransferResult.Failed(recipe.OutputItemId, outputCount, "Output store cannot accept the result");
                NotifyFailed(request, context);
                return;
            }

            RuntimeTimedWorkJob job = new RuntimeTimedWorkJob(
                ResolveJobType(options),
                workstationObject,
                worker,
                ResolveDuration(recipe, options),
                options != null ? options.AnimationTrigger : null,
                options != null ? options.CompleteAnimationTrigger : null,
                delegate
                {
                    CompleteQueuedCraft(recipe, request, ingredientStore, outputStore, context);
                },
                delegate
                {
                    context.Result = ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Cooking job was cancelled");
                    NotifyFailed(request, context);
                    if (context.Panel != null)
                        context.Panel.Refresh();
                });

            bool queued = options != null && options.QueueAsPlayerJob ? worker.AddPlayerJob(job) : worker.AddAIJob(job);
            if (!queued)
            {
                context.Result = ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Worker job queue rejected the cooking job");
                NotifyFailed(request, context);
                return;
            }

            context.Queued = true;
            context.Result = ItemTransferResult.Ok(recipe.OutputItemId, outputCount, 0);
            if (request != null && request.OnCraftQueued != null)
                request.OnCraftQueued(context);
            if (context.Panel != null)
            {
                if (options != null && options.ClosePanelOnQueue)
                    context.Panel.Close();
                else
                    context.Panel.Refresh();
            }
        }

        private static void CompleteQueuedCraft(CookingStationRecipe recipe, CookingStationRequest request, IItemStore ingredientStore, IItemStore outputStore, CookingCraftContext context)
        {
            ApplyTargetIntegrityCost(request);

            ItemTransferResult result = ApplyRecipe(recipe, request, ingredientStore, outputStore);
            context.Result = result;
            if (result.Success)
            {
                if (request != null && request.OnCrafted != null)
                    request.OnCrafted(context);
            }
            else
            {
                NotifyFailed(request, context);
            }

            if (context.Panel != null)
                context.Panel.Refresh();
        }

        private static string ResolveJobType(CookingStationJobOptions options)
        {
            return options != null && !string.IsNullOrEmpty(options.JobType) ? options.JobType : "cook_food";
        }

        private static float ResolveDuration(CookingStationRecipe recipe, CookingStationJobOptions options)
        {
            if (recipe != null && recipe.DurationSeconds > 0f)
                return recipe.DurationSeconds;
            if (options != null && options.DurationSeconds > 0f)
                return options.DurationSeconds;
            return 2f;
        }

        private static void ApplyTargetIntegrityCost(CookingStationRequest request)
        {
            if (request == null || request.JobOptions == null || request.JobOptions.TargetIntegrityCost <= 0)
                return;

            Obj_Integrity integrity = request.WorkstationObject as Obj_Integrity;
            if (integrity != null)
                integrity.Degrade(request.JobOptions.TargetIntegrityCost);
        }

        private static ItemTransferResult ApplyRecipe(CookingStationRecipe recipe, CookingStationRequest request, IItemStore ingredientStore, IItemStore outputStore)
        {
            if (recipe == null)
                return ItemTransferResult.Failed(null, 0, "Recipe is required");
            if (ingredientStore == null)
                return ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Ingredient store is required");
            if (outputStore == null)
                return ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Output store is required");
            if (!HasIngredients(recipe, ingredientStore))
                return ItemTransferResult.Failed(recipe.OutputItemId, recipe.OutputCount, "Missing ingredients");

            int outputCount = Math.Max(1, recipe.OutputCount);
            if (!string.IsNullOrEmpty(recipe.OutputItemId) && !outputStore.CanAdd(recipe.OutputItemId, outputCount))
                return ItemTransferResult.Failed(recipe.OutputItemId, outputCount, "Output store cannot accept the result");

            List<RecipeIngredient> consumed = new List<RecipeIngredient>();
            if (request == null || request.ConsumeIngredients)
            {
                for (int i = 0; recipe.Ingredients != null && i < recipe.Ingredients.Count; i++)
                {
                    RecipeIngredient ingredient = recipe.Ingredients[i];
                    if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId))
                        continue;

                    int count = Math.Max(1, ingredient.Count);
                    ItemTransferResult removed = ingredientStore.Remove(ingredient.ItemId, count);
                    if (!removed.Success)
                    {
                        RollbackIngredients(ingredientStore, consumed);
                        return removed;
                    }
                    consumed.Add(new RecipeIngredient { ItemId = ingredient.ItemId, Count = removed.Moved });
                }
            }

            if (string.IsNullOrEmpty(recipe.OutputItemId))
                return ItemTransferResult.Ok(null, 0, 0);

            ItemTransferResult added = outputStore.Add(recipe.OutputItemId, outputCount);
            if (!added.Success)
            {
                RollbackIngredients(ingredientStore, consumed);
                return added;
            }

            return added;
        }

        private static void RollbackIngredients(IItemStore store, List<RecipeIngredient> consumed)
        {
            if (store == null || consumed == null)
                return;

            for (int i = 0; i < consumed.Count; i++)
            {
                RecipeIngredient ingredient = consumed[i];
                if (ingredient != null && !string.IsNullOrEmpty(ingredient.ItemId) && ingredient.Count > 0)
                    store.Add(ingredient.ItemId, ingredient.Count);
            }
        }

        private static void NotifyFailed(CookingStationRequest request, CookingCraftContext context)
        {
            if (request != null && request.OnCraftFailed != null)
                request.OnCraftFailed(context);
        }
    }
}
