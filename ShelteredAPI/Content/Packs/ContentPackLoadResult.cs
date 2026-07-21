using System.Collections.Generic;
using ShelteredModManager.ContentPacks;

namespace ShelteredAPI.Content.Packs
{
    internal sealed class ContentPackLoadResult
    {
        public bool Found;
        public bool Success;
        public string ModId;
        public string PackPath;
        public string ErrorMessage;
        public int ItemCount;
        public int RecipeCount;
        public readonly List<ContentPackValidationIssue> Issues =
            new List<ContentPackValidationIssue>();
    }
}
