using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    public enum NexusUploadStage
    {
        Details,
        Files,
        Verify,
        Publish
    }

    public enum NexusOwnershipVerificationKind
    {
        None,
        UploaderId,
        UploaderName,
        AuthorName
    }

    public class NexusUploadDraft
    {
        public string LocalModId { get; set; }
        public string LocalModPath { get; set; }
        public string GameDomain { get; set; }
        public int NexusModId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string AuthorsText { get; set; }
        public string TagsText { get; set; }
        public string PackagePath { get; set; }
        public string FileCategory { get; set; }
        public string UpdateGroupId { get; set; }
        public bool PrimaryModManagerDownload { get; set; }
        public bool AllowModManagerDownload { get; set; }
        public bool ShowRequirementsPopup { get; set; }
        public bool ArchiveExistingFile { get; set; }
        public DateTime SavedAtUtc { get; set; }
        public NexusUploadStage Stage { get; set; }

        public NexusUploadDraft()
        {
            LocalModId = string.Empty;
            LocalModPath = string.Empty;
            GameDomain = string.Empty;
            Name = string.Empty;
            Version = string.Empty;
            Summary = string.Empty;
            Description = string.Empty;
            AuthorsText = string.Empty;
            TagsText = string.Empty;
            PackagePath = string.Empty;
            FileCategory = "main";
            UpdateGroupId = string.Empty;
            AllowModManagerDownload = true;
            SavedAtUtc = DateTime.MinValue;
            Stage = NexusUploadStage.Details;
        }
    }

    public class NexusOwnershipVerification
    {
        public bool IsVerified { get; set; }
        public NexusOwnershipVerificationKind Kind { get; set; }
        public NexusRemoteMod RemoteMod { get; set; }
        public string Summary { get; set; }

        public NexusOwnershipVerification()
        {
            Kind = NexusOwnershipVerificationKind.None;
            Summary = string.Empty;
        }
    }

    public class NexusUploadValidationReport
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public IList<string> Errors { get { return _errors; } }
        public IList<string> Warnings { get { return _warnings; } }
        public bool HasErrors { get { return _errors.Count > 0; } }
        public bool CanPackage { get { return !HasErrors; } }
        public bool CanPublish { get { return !HasErrors; } }

        public void AddError(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _warnings.Add(message);
        }
    }

    public class NexusUploadPackageResult
    {
        public string PackagePath { get; set; }
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }
    }

    public class NexusUploadPublishResult
    {
        public string UploadId { get; set; }
        public string ModFileId { get; set; }
        public string ModFileGameScopedId { get; set; }
        public string State { get; set; }
        public string Summary { get; set; }
    }

    public class NexusModFileUpdateGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastFileUploadedAtUtc { get; set; }
        public int VersionsCount { get; set; }
    }
}
