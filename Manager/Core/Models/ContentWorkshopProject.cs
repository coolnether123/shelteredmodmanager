using System;
using ShelteredModManager.ContentPacks;

namespace Manager.Core.Models
{
    [Serializable]
    public sealed class ContentWorkshopAbout
    {
        public string id;
        public string name;
        public string version = "1.0.0";
        public string description;
        public string entryType = "ContentPack";
        public string[] authors = new string[0];
        public string[] tags = new string[] { "Content" };
        public string website;
        public string requiredShelteredApiVersion;
    }

    /// <summary>
    /// Editable workshop aggregate. RootPath is editor state and is never serialized
    /// into the distributable content-pack document.
    /// </summary>
    public sealed class ContentWorkshopProject
    {
        public string RootPath { get; set; }
        public ContentWorkshopAbout About { get; set; }
        public ContentPackDocument Content { get; set; }
        public bool IsDirty { get; set; }

        public ContentWorkshopProject()
        {
            About = new ContentWorkshopAbout();
            Content = new ContentPackDocument();
        }

        public string ModId
        {
            get { return About == null ? string.Empty : (About.id ?? string.Empty); }
        }
    }

    public sealed class ContentWorkshopOperationResult
    {
        public bool Success { get; set; }
        public string Path { get; set; }
        public string ErrorMessage { get; set; }

        public static ContentWorkshopOperationResult Succeeded(string path)
        {
            return new ContentWorkshopOperationResult { Success = true, Path = path };
        }

        public static ContentWorkshopOperationResult Failed(string error)
        {
            return new ContentWorkshopOperationResult
            {
                Success = false,
                ErrorMessage = error ?? "The operation failed."
            };
        }
    }
}
