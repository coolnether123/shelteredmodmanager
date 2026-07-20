using System;
using System.Collections.Generic;

namespace ShelteredModManager.ContentPacks
{
    /// <summary>
    /// Versioned, host-neutral content-pack document shared by the manager and runtime.
    /// Public fields keep the contract compatible with JavaScriptSerializer on .NET 3.5.
    /// </summary>
    [Serializable]
    public sealed class ContentPackDocument
    {
        public int schemaVersion = 1;
        public string modId;
        public List<ContentPackItem> items = new List<ContentPackItem>();
        public List<ContentPackRecipe> recipes = new List<ContentPackRecipe>();
    }

    [Serializable]
    public sealed class ContentPackItem
    {
        public string id;
        public string displayName;
        public string description;
        public string iconPath;
        public string category = "Normal";
        public int stackSize = 64;
        public int tradeValue = 20;
        public float burnValue;
        public float scrapValue;
        public float baseCraftTime = 10f;
        public int craftStackSize = 1;
        public ContentPackFabrication fabrication = new ContentPackFabrication();
        public ContentPackRation ration = new ContentPackRation();
        public int loadCarrySlots;
        public ContentPackRawFood rawFood = new ContentPackRawFood();
        public List<ContentPackIngredient> recycling = new List<ContentPackIngredient>();
    }

    [Serializable]
    public sealed class ContentPackFabrication
    {
        public float cost;
        public float timeSeconds;
    }

    [Serializable]
    public sealed class ContentPackRation
    {
        public int value;
        public float contamination;
    }

    [Serializable]
    public sealed class ContentPackRawFood
    {
        public bool enabled;
        public float cookedHungerMultiplier = 1.1f;
    }

    [Serializable]
    public sealed class ContentPackRecipe
    {
        public string id;
        public string resultItemId;
        public string station = "Workbench";
        public int level = 1;
        public float craftTimeSeconds = 1f;
        public bool unique;
        public bool locked;
        public string unlockFlag;
        public List<ContentPackIngredient> ingredients = new List<ContentPackIngredient>();
    }

    [Serializable]
    public sealed class ContentPackIngredient
    {
        public string itemId;
        public int count = 1;
    }

    /// <summary>Supplies host/package facts that are intentionally absent from the document.</summary>
    public sealed class ContentPackValidationContext
    {
        public string ExpectedModId;
        public string ModRootPath;
        public bool ValidateAssetFiles;
        public long MaximumIconBytes = 4L * 1024L * 1024L;
        public int MaximumIconDimension = 2048;
    }
}
