using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    /// <summary>
    /// Represents a discovered mod with all its metadata.
    /// </summary>
    public class ModItem
    {
        private string _id;
        private string _displayName;
        private string _rootPath;
        private string _version;
        private string[] _authors;
        private string _description;
        private string[] _tags;
        private string[] _dependsOn;
        private string[] _loadAfter;
        private string[] _loadBefore;
        private string _website;
        private string _previewPath;
        private bool _hasValidAbout;

        public string Id { get { return _id; } }
        public string DisplayName { get { return _displayName; } }
        public string RootPath { get { return _rootPath; } }
        public string Version { get { return _version; } set { _version = value; } }
        public string[] Authors { get { return _authors; } set { _authors = value; } }
        public string Description { get { return _description; } set { _description = value; } }
        public string[] Tags { get { return _tags; } set { _tags = value; } }
        public string[] DependsOn { get { return _dependsOn; } set { _dependsOn = value; } }
        public string[] LoadAfter { get { return _loadAfter; } set { _loadAfter = value; } }
        public string[] LoadBefore { get { return _loadBefore; } set { _loadBefore = value; } }
        public string Website { get { return _website; } set { _website = value; } }
        public string PreviewPath { get { return _previewPath; } set { _previewPath = value; } }
        public bool HasValidAbout { get { return _hasValidAbout; } set { _hasValidAbout = value; } }
        
        // Runtime state
        public bool IsEnabled { get; set; }
        public ModStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public string RequiredModApiVersion { get; set; }
        public bool IsModApiCompatible { get; set; }

        public ModItem(string id, string displayName, string rootPath)
        {
            if (id == null) throw new ArgumentNullException("id");
            _id = id;
            _displayName = displayName ?? id;
            _rootPath = rootPath ?? string.Empty;
            _version = "Unknown";
            _authors = new string[0];
            _description = string.Empty;
            _tags = new string[0];
            _dependsOn = new string[0];
            _loadAfter = new string[0];
            _loadBefore = new string[0];
            Status = ModStatus.Ok;
            IsModApiCompatible = true;
        }

        /// <summary>
        /// Creates a ModItem from About.json data
        /// </summary>
        public static ModItem FromAbout(ModTypes.ModAboutInfo about, string rootPath, string previewPath)
        {
            string id;
            if (about != null && !string.IsNullOrEmpty(about.id))
            {
                id = about.id.Trim().ToLowerInvariant();
            }
            else
            {
                string folderName = System.IO.Path.GetFileName(rootPath);
                id = (folderName != null) ? folderName.ToLowerInvariant() : "unknown";
            }
            
            string displayName;
            if (about != null && !string.IsNullOrEmpty(about.name))
            {
                displayName = about.name;
            }
            else
            {
                displayName = System.IO.Path.GetFileName(rootPath) ?? "Unknown Mod";
            }

            var item = new ModItem(id, displayName, rootPath);
            
            if (about != null)
            {
                item.Version = about.version ?? "Unknown";
                item.Authors = about.authors ?? new string[0];
                item.Description = about.description ?? string.Empty;
                item.Tags = about.tags ?? new string[0];
                item.DependsOn = about.dependsOn ?? new string[0];
                item.LoadAfter = about.loadAfter ?? new string[0];
                item.LoadBefore = about.loadBefore ?? new string[0];
                item.Website = about.website ?? string.Empty;
                item.HasValidAbout = !string.IsNullOrEmpty(about.id);
            }
            
            item.PreviewPath = previewPath;

            return item;
        }

        public override string ToString()
        {
            return DisplayName;
        }
        
        public override bool Equals(object obj)
        {
            ModItem other = obj as ModItem;
            if (other != null)
                return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty);
        }
    }

    /// <summary>
    /// Status indicators for mod health
    /// </summary>
    public enum ModStatus
    {
        Ok,                     // All good
        Warning,                // Soft issues (load order)
        Error,                  // Hard issues (missing dependencies)
        MissingDependency,      // Required mod not found
        VersionMismatch,        // ModAPI version incompatible
        LoadOrderConflict       // Should load before/after something
    }
}
