using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using ShelteredAPI.Storage;
using ShelteredAPI.UI.Runtime;
using UnityEngine;

namespace ShelteredAPI.Workstations
{
    /// <summary>
    /// Recipe used by ShelteredAPI mod-owned cooking stations.
    /// </summary>
    public sealed class CookingStationRecipe
    {
        public CookingStationRecipe()
        {
            Ingredients = new List<RecipeIngredient>();
            OutputCount = 1;
        }

        public string RecipeId { get; set; }
        public string DisplayName { get; set; }
        public string Subtitle { get; set; }
        public IList<RecipeIngredient> Ingredients { get; set; }
        public string OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public string OutputCountText { get; set; }
        public float DurationSeconds { get; set; }
        public Sprite Icon { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// Direct request for a mod-owned cooking/crafting panel backed by item stores.
    /// </summary>
    public sealed class CookingStationRequest
    {
        public CookingStationRequest()
        {
            ConsumeIngredients = true;
            RefreshEveryFrame = false;
        }

        public string OwnerId { get; set; }
        public string PanelId { get; set; }
        public string Title { get; set; }
        public RuntimePanelOptions PanelOptions { get; set; }
        public IItemStore IngredientStore { get; set; }
        public IItemStore OutputStore { get; set; }
        public FamilyMember Worker { get; set; }
        public Obj_Base WorkstationObject { get; set; }
        public CookingStationJobOptions JobOptions { get; set; }
        public IList<CookingStationRecipe> Recipes { get; set; }
        public Func<IList<CookingStationRecipe>> RecipeSource { get; set; }
        public bool ConsumeIngredients { get; set; }
        public bool RefreshEveryFrame { get; set; }
        public Func<CookingStationRecipe, string> GetUnavailableReason { get; set; }
        public Action<CookingCraftContext> OnCraftQueued { get; set; }
        public Action<CookingCraftContext> OnCrafted { get; set; }
        public Action<CookingCraftContext> OnCraftFailed { get; set; }
        public Action OnClosed { get; set; }
    }

    /// <summary>
    /// Timed character job options for cooking station crafts.
    /// </summary>
    public sealed class CookingStationJobOptions
    {
        public CookingStationJobOptions()
        {
            Enabled = true;
            DurationSeconds = 2f;
            JobType = "cook_food";
            AnimationTrigger = "Rummage";
            CompleteAnimationTrigger = "Idle";
            ClosePanelOnQueue = true;
        }

        public bool Enabled { get; set; }
        public float DurationSeconds { get; set; }
        public string JobType { get; set; }
        public string AnimationTrigger { get; set; }
        public string CompleteAnimationTrigger { get; set; }
        public bool QueueAsPlayerJob { get; set; }
        public bool ClosePanelOnQueue { get; set; }
        public int TargetIntegrityCost { get; set; }
    }

    /// <summary>
    /// Object-panel registration that opens a cooking station UI from a world object.
    /// </summary>
    public sealed class CookingStationRegistration
    {
        public CookingStationRegistration()
        {
            ObjectType = ObjectManager.ObjectType.Undefined;
            InteractionText = "Cook";
            ConsumeIngredients = true;
        }

        public string OwnerId { get; set; }
        public string ObjectId { get; set; }
        public ObjectManager.ObjectType ObjectType { get; set; }
        public string InteractionId { get; set; }
        public string InteractionText { get; set; }
        public int Priority { get; set; }
        public string PanelId { get; set; }
        public string Title { get; set; }
        public RuntimePanelOptions PanelOptions { get; set; }
        public bool ConsumeIngredients { get; set; }
        public bool RefreshEveryFrame { get; set; }
        public Func<CookingStationContext, bool> CanOpen { get; set; }
        public Func<CookingStationContext, IItemStore> IngredientStore { get; set; }
        public Func<CookingStationContext, IItemStore> OutputStore { get; set; }
        public Func<CookingStationContext, FamilyMember> Worker { get; set; }
        public Func<CookingStationContext, Obj_Base> WorkstationObject { get; set; }
        public CookingStationJobOptions JobOptions { get; set; }
        public IList<CookingStationRecipe> Recipes { get; set; }
        public Func<CookingStationContext, IList<CookingStationRecipe>> RecipeSource { get; set; }
        public Func<CookingStationRecipe, string> GetUnavailableReason { get; set; }
        public Action<CookingCraftContext> OnCraftQueued { get; set; }
        public Action<CookingCraftContext> OnCrafted { get; set; }
        public Action<CookingCraftContext> OnCraftFailed { get; set; }
        public Action OnClosed { get; set; }
    }

    /// <summary>
    /// Runtime context passed when a registered cooking station opens.
    /// </summary>
    public sealed class CookingStationContext
    {
        internal CookingStationContext(string objectId, Obj_Base targetObject, FamilyMember selectedMember)
        {
            ObjectId = objectId;
            TargetObject = targetObject;
            SelectedMember = selectedMember;
            ObjectType = ResolveObjectType(targetObject);
        }

        public string ObjectId { get; private set; }
        public ObjectManager.ObjectType ObjectType { get; private set; }
        public Obj_Base TargetObject { get; private set; }
        public FamilyMember SelectedMember { get; private set; }

        private static ObjectManager.ObjectType ResolveObjectType(Obj_Base targetObject)
        {
            if (targetObject == null)
                return ObjectManager.ObjectType.Undefined;

            try
            {
                return targetObject.GetObjectType();
            }
            catch
            {
                return ObjectManager.ObjectType.Undefined;
            }
        }
    }

    /// <summary>
    /// Result context for a cooking craft request.
    /// </summary>
    public sealed class CookingCraftContext
    {
        internal CookingCraftContext(CookingStationRecipe recipe, IItemStore ingredientStore, IItemStore outputStore, RuntimeUiHandle panel)
        {
            Recipe = recipe;
            IngredientStore = ingredientStore;
            OutputStore = outputStore;
            Panel = panel;
        }

        public CookingStationRecipe Recipe { get; private set; }
        public IItemStore IngredientStore { get; private set; }
        public IItemStore OutputStore { get; private set; }
        public RuntimeUiHandle Panel { get; private set; }
        public FamilyMember Worker { get; internal set; }
        public Obj_Base WorkstationObject { get; internal set; }
        public bool Queued { get; internal set; }
        public ItemTransferResult Result { get; internal set; }
    }
}
