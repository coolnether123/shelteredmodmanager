using System;
using System.IO;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class NexusUploadDraftService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public NexusUploadDraft LoadOrCreate(ModItem mod, AppSettings settings)
        {
            NexusUploadDraft draft;
            if (TryLoad(mod, out draft))
            {
                MergeMissingValues(draft, mod, settings);
                return draft;
            }

            draft = new NexusUploadDraft();
            MergeMissingValues(draft, mod, settings);
            draft.SavedAtUtc = DateTime.UtcNow;
            return draft;
        }

        public bool Save(ModItem mod, NexusUploadDraft draft, out string errorMessage)
        {
            errorMessage = null;
            if (mod == null || string.IsNullOrEmpty(mod.RootPath))
            {
                errorMessage = "No local mod is selected.";
                return false;
            }

            if (draft == null)
            {
                errorMessage = "No upload draft is available.";
                return false;
            }

            try
            {
                string aboutDir = Path.Combine(mod.RootPath, "About");
                if (!Directory.Exists(aboutDir))
                    Directory.CreateDirectory(aboutDir);

                draft.LocalModId = mod.Id ?? string.Empty;
                draft.LocalModPath = mod.RootPath ?? string.Empty;
                draft.SavedAtUtc = DateTime.UtcNow;

                File.WriteAllText(GetDraftPath(mod), _serializer.Serialize(draft));
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Upload draft could not be saved: " + ex.Message;
                return false;
            }
        }

        public NexusUploadValidationReport Validate(ModItem mod, NexusUploadDraft draft, NexusOwnershipVerification ownership)
        {
            var report = new NexusUploadValidationReport();

            if (mod == null)
            {
                report.AddError("Select a local mod before publishing.");
                return report;
            }

            if (string.IsNullOrEmpty(mod.RootPath) || !Directory.Exists(mod.RootPath))
                report.AddError("The selected mod folder does not exist.");
            else
            {
                global::Manager.ModTypes.ModAboutInfo about;
                string normalizedId;
                string packageError;
                if (!ModPackageSafety.ValidateUploadRoot(mod.RootPath, out about, out normalizedId, out packageError))
                {
                    report.AddError(packageError);
                }
                else if (!string.Equals(normalizedId, ModPackageSafety.NormalizeModId(mod.Id), StringComparison.OrdinalIgnoreCase))
                {
                    report.AddError("About.json id does not match the selected local mod id.");
                }
            }

            if (draft == null)
            {
                report.AddError("The upload draft could not be loaded.");
                return report;
            }

            if (string.IsNullOrEmpty(draft.GameDomain))
                report.AddError("A Nexus game domain is required.");
            if (string.IsNullOrEmpty(draft.Name))
                report.AddError("A mod name is required.");
            if (string.IsNullOrEmpty(draft.Version))
                report.AddError("A version is required.");
            if (string.IsNullOrEmpty(draft.PackagePath) || !File.Exists(draft.PackagePath))
                report.AddError("Build a package before publishing through the Nexus API.");
            if (string.IsNullOrEmpty(draft.Summary))
                report.AddWarning("Add a short summary before posting to Nexus.");
            if (string.IsNullOrEmpty(draft.Description))
                report.AddWarning("Add a longer description before posting to Nexus.");
            if (!IsValidFileCategory(draft.FileCategory))
                report.AddError("File category must be main, optional, or miscellaneous.");
            if (draft.NexusModId <= 0)
                report.AddError("Nexus v3 API publishing requires an existing Nexus mod ID.");
            if (ownership == null || !ownership.IsVerified)
                report.AddWarning("Nexus v3 will verify ownership when the file publish request is submitted.");

            return report;
        }

        private bool TryLoad(ModItem mod, out NexusUploadDraft draft)
        {
            draft = null;
            if (mod == null || string.IsNullOrEmpty(mod.RootPath))
                return false;

            string path = GetDraftPath(mod);
            if (!File.Exists(path))
                return false;

            try
            {
                draft = _serializer.Deserialize<NexusUploadDraft>(File.ReadAllText(path));
                return draft != null;
            }
            catch
            {
                draft = null;
                return false;
            }
        }

        private static string GetDraftPath(ModItem mod)
        {
            return Path.Combine(Path.Combine(mod.RootPath, "About"), "NexusUploadDraft.json");
        }

        private static void MergeMissingValues(NexusUploadDraft draft, ModItem mod, AppSettings settings)
        {
            if (draft == null || mod == null)
                return;

            if (string.IsNullOrEmpty(draft.LocalModId)) draft.LocalModId = mod.Id ?? string.Empty;
            if (string.IsNullOrEmpty(draft.LocalModPath)) draft.LocalModPath = mod.RootPath ?? string.Empty;
            if (string.IsNullOrEmpty(draft.GameDomain))
                draft.GameDomain = !string.IsNullOrEmpty(mod.NexusGameDomain)
                    ? mod.NexusGameDomain
                    : (settings != null ? settings.NexusGameDomain : string.Empty);
            if (draft.NexusModId <= 0) draft.NexusModId = mod.NexusModId;
            if (string.IsNullOrEmpty(draft.Name)) draft.Name = mod.DisplayName ?? string.Empty;
            if (string.IsNullOrEmpty(draft.Version)) draft.Version = mod.Version ?? string.Empty;
            if (string.IsNullOrEmpty(draft.Description)) draft.Description = mod.Description ?? string.Empty;
            if (string.IsNullOrEmpty(draft.Summary)) draft.Summary = BuildSummary(mod.Description);
            if (string.IsNullOrEmpty(draft.AuthorsText)) draft.AuthorsText = Join(mod.Authors);
            if (string.IsNullOrEmpty(draft.TagsText)) draft.TagsText = Join(mod.Tags);
            if (string.IsNullOrEmpty(draft.FileCategory)) draft.FileCategory = "main";
        }

        private static string BuildSummary(string description)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty;

            string text = description.Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length <= 220)
                return text;

            return text.Substring(0, 217).TrimEnd() + "...";
        }

        private static string Join(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            return string.Join(", ", values);
        }

        private static bool IsValidFileCategory(string value)
        {
            return string.Equals(value, "main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "optional", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "miscellaneous", StringComparison.OrdinalIgnoreCase);
        }
    }
}
