using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Nexus Mods v3 API operations used by Manager.
    /// </summary>
    public class NexusModsService
    {
        private const long SinglePartUploadLimitBytes = 100L * 1024L * 1024L;
        private readonly NexusV3RestClient _client;
        private readonly string _apiKey;

        public NexusModsService(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _client = new NexusV3RestClient(apiKey);
        }

        public Dictionary<string, NexusRemoteMod> GetModsByReferences(IEnumerable<NexusModReference> references, out string errorMessage)
        {
            errorMessage = null;
            var results = new Dictionary<string, NexusRemoteMod>(StringComparer.OrdinalIgnoreCase);
            var distinct = GetDistinctReferences(references);

            for (int i = 0; i < distinct.Count; i++)
            {
                NexusModReference reference = distinct[i];
                string error;
                NexusRemoteMod mod = GetModByDomainAndId(reference.GameDomain, reference.ModId, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    errorMessage = error;
                    continue;
                }

                if (mod != null)
                    results[reference.Key] = mod;
            }

            return results;
        }

        public List<NexusRemoteMod> GetLatestMods(string gameDomain, int count, out string errorMessage)
        {
            errorMessage = null;
            var list = new List<NexusRemoteMod>();
            if (string.IsNullOrEmpty(gameDomain))
            {
                errorMessage = "Nexus game domain is not configured.";
                return list;
            }

            NexusV3RestResult response = _client.Get("/games/" + Escape(gameDomain) + "/trending-mods");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return list;
            }

            object[] mods = AsArray(response.Data, "mods");
            if (mods == null)
                return list;

            int limit = count <= 0 ? mods.Length : Math.Min(count, mods.Length);
            for (int i = 0; i < limit; i++)
            {
                var node = mods[i] as Dictionary<string, object>;
                NexusRemoteMod mod = ParseTrendingMod(node, gameDomain);
                if (mod != null)
                    list.Add(mod);
            }

            return list;
        }

        public NexusRemoteMod GetModByDomainAndId(string gameDomain, int modId, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(gameDomain) || modId <= 0)
            {
                errorMessage = "Invalid Nexus mod reference.";
                return null;
            }

            NexusV3RestResult response = _client.Get("/games/" + Escape(gameDomain) + "/mods/" + modId);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return null;
            }

            NexusRemoteMod mod = ParseV3Mod(response.Data, gameDomain);
            if (mod == null)
                errorMessage = "Nexus v3 did not return mod details.";
            else
                PopulateLatestFileState(mod, ref errorMessage);

            return mod;
        }

        public List<NexusRemoteMod> FindModsByName(string gameDomain, string modName, int count, out string errorMessage)
        {
            string feedError;
            List<NexusRemoteMod> feed = GetLatestMods(gameDomain, count <= 0 ? 5 : count, out feedError);
            var matches = new List<NexusRemoteMod>();

            if (!string.IsNullOrEmpty(feedError))
            {
                errorMessage = feedError;
                return matches;
            }

            for (int i = 0; i < feed.Count; i++)
            {
                NexusRemoteMod mod = feed[i];
                if (mod != null && !string.IsNullOrEmpty(mod.Name) && !string.IsNullOrEmpty(modName) &&
                    mod.Name.IndexOf(modName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(mod);
                }
            }

            errorMessage = matches.Count == 0
                ? "Nexus v3 OpenAPI does not expose general mod search; no matching trending mod was found."
                : null;
            return matches;
        }

        public List<NexusRemoteMod> GetModsByUploader(string gameDomain, int uploaderId, int count, out string errorMessage)
        {
            errorMessage = "Nexus v3 OpenAPI does not expose a list-mods-by-uploader endpoint.";
            return new List<NexusRemoteMod>();
        }

        public List<NexusRemoteMod> GetModsByAuthor(string gameDomain, string authorName, int count, out string errorMessage)
        {
            errorMessage = "Nexus v3 OpenAPI does not expose a list-mods-by-author endpoint.";
            return new List<NexusRemoteMod>();
        }

        public List<NexusRemoteMod> GetModsForUploadOwnership(NexusAccountStatus account, string gameDomain, string authorName, out string errorMessage)
        {
            errorMessage = "Nexus v3 verifies file ownership when creating a mod file or update-group version. It does not expose an owned-mod listing endpoint.";
            return new List<NexusRemoteMod>();
        }

        public List<NexusModFileUpdateGroup> GetModFileUpdateGroups(string modUniqueId, out string errorMessage)
        {
            errorMessage = null;
            var groups = new List<NexusModFileUpdateGroup>();
            if (string.IsNullOrEmpty(modUniqueId))
            {
                errorMessage = "Nexus v3 mod id is required.";
                return groups;
            }

            NexusV3RestResult response = _client.Get("/mods/" + Escape(modUniqueId) + "/file-update-groups");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return groups;
            }

            object[] rawGroups = AsArray(response.Data, "groups");
            if (rawGroups == null)
                return groups;

            for (int i = 0; i < rawGroups.Length; i++)
            {
                var node = rawGroups[i] as Dictionary<string, object>;
                if (node == null) continue;

                var group = new NexusModFileUpdateGroup();
                group.Id = ReadString(node, "id");
                group.Name = ReadString(node, "name");
                group.IsActive = ReadBool(node, "is_active");
                group.LastFileUploadedAtUtc = ReadDateTime(node, "last_file_uploaded_at");
                group.VersionsCount = ReadInt(node, "versions_count");
                if (!string.IsNullOrEmpty(group.Id))
                    groups.Add(group);
            }

            return groups;
        }

        public List<NexusRemoteModFile> GetFileUpdateGroupVersions(string groupId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            if (string.IsNullOrEmpty(groupId))
            {
                errorMessage = "File update group id is required.";
                return files;
            }

            NexusV3RestResult response = _client.Get("/file-update-groups/" + Escape(groupId) + "/versions");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return files;
            }

            object[] versions = AsArray(response.Data, "versions");
            if (versions == null)
                return files;

            for (int i = 0; i < versions.Length; i++)
            {
                var version = versions[i] as Dictionary<string, object>;
                var fileNode = AsDictionary(version, "file");
                NexusRemoteModFile file = ParseMinimalFile(fileNode, groupId);
                if (file != null)
                    files.Add(file);
            }

            return files;
        }

        public List<NexusRemoteModFile> GetModFiles(int gameId, int modId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            if (modId <= 0)
            {
                errorMessage = "Invalid mod ID for v3 file query.";
                return files;
            }

            errorMessage = "Nexus v3 file lookup requires the game domain. Use GetModFiles(gameDomain, modId) for v3-compatible file queries.";
            return files;
        }

        public List<NexusRemoteModFile> GetModFiles(string gameDomain, int modId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            NexusRemoteMod mod = GetModByDomainAndId(gameDomain, modId, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage) || mod == null || string.IsNullOrEmpty(mod.Uid))
                return files;

            List<NexusModFileUpdateGroup> groups = GetModFileUpdateGroups(mod.Uid, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage))
                return files;

            for (int i = 0; i < groups.Count; i++)
            {
                string versionError;
                List<NexusRemoteModFile> groupFiles = GetFileUpdateGroupVersions(groups[i].Id, out versionError);
                if (!string.IsNullOrEmpty(versionError) && string.IsNullOrEmpty(errorMessage))
                    errorMessage = versionError;
                files.AddRange(groupFiles);
            }

            return files;
        }

        public NexusRemoteModFile GetPreferredInstallFile(int gameId, int modId, out string errorMessage)
        {
            errorMessage = "Nexus v3 OpenAPI does not support direct Manager downloads by numeric game id.";
            return null;
        }

        public NexusRemoteModFile GetPreferredInstallFile(int gameId, int modId, bool includePrerelease, out string errorMessage)
        {
            return GetPreferredInstallFile(gameId, modId, out errorMessage);
        }

        public NexusRemoteModFile SelectPreferredInstallFile(List<NexusRemoteModFile> files, bool includePrerelease)
        {
            return SelectPreferredInstallFile(files, includePrerelease, false);
        }

        public NexusRemoteModFile SelectPreferredPrereleaseInstallFile(List<NexusRemoteModFile> files)
        {
            return SelectPreferredInstallFile(files, true, true);
        }

        public string GetV3DownloadUrl(string gameDomain, int modId, int fileId, string apiKey, out string errorMessage)
        {
            errorMessage = "Direct download URL resolution is not exposed by the Nexus v3 OpenAPI. Open the Nexus page for manual download.";
            return null;
        }

        public NexusAccountStatus GetAccountStatus(out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(_apiKey))
                return NexusAccountStatus.CreateNotConfigured();

            var status = new NexusAccountStatus();
            status.IsConfigured = true;
            status.IsConnected = true;
            status.UserName = "Nexus API key";
            status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Unavailable;
            status.Summary = "Nexus v3 API key configured.";
            status.DirectDownloadSummary = "The v3 OpenAPI used by Manager does not expose account profile or direct-download capability endpoints.";
            return status;
        }

        public NexusUploadPublishResult PublishPackage(NexusUploadDraft draft, out string errorMessage)
        {
            errorMessage = null;
            if (draft == null)
            {
                errorMessage = "No Nexus upload draft is selected.";
                return null;
            }
            if (string.IsNullOrEmpty(_apiKey))
            {
                errorMessage = "A Nexus API key is required to publish through v3.";
                return null;
            }
            if (string.IsNullOrEmpty(draft.PackagePath) || !File.Exists(draft.PackagePath))
            {
                errorMessage = "Build the upload package before publishing.";
                return null;
            }
            if (draft.NexusModId <= 0)
            {
                errorMessage = "Nexus v3 file publishing requires an existing Nexus mod id.";
                return null;
            }

            string modError;
            NexusRemoteMod mod = GetModByDomainAndId(draft.GameDomain, draft.NexusModId, out modError);
            if (!string.IsNullOrEmpty(modError) || mod == null || string.IsNullOrEmpty(mod.Uid))
            {
                errorMessage = !string.IsNullOrEmpty(modError) ? modError : "Could not resolve the Nexus v3 mod id.";
                return null;
            }

            string uploadId = UploadPackageFile(draft.PackagePath, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage) || string.IsNullOrEmpty(uploadId))
                return null;

            WaitForUploadAvailable(uploadId, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage))
                return null;

            Dictionary<string, object> request = BuildModFileRequest(draft, uploadId);
            NexusV3RestResult publishResponse;
            if (!string.IsNullOrEmpty(draft.UpdateGroupId))
            {
                publishResponse = _client.Post("/mod-file-update-groups/" + Escape(draft.UpdateGroupId) + "/versions", request);
            }
            else
            {
                request["mod_id"] = mod.Uid;
                publishResponse = _client.Post("/mod-files", request);
            }

            if (!string.IsNullOrEmpty(publishResponse.ErrorMessage))
            {
                errorMessage = publishResponse.ErrorMessage;
                return null;
            }

            string fileId = ReadString(publishResponse.Data, "id");
            string scopedFileId = ReadString(publishResponse.Data, "game_scoped_id");
            return new NexusUploadPublishResult
            {
                UploadId = uploadId,
                ModFileId = fileId,
                ModFileGameScopedId = scopedFileId,
                State = "published",
                Summary = "Published Nexus mod file " + (!string.IsNullOrEmpty(scopedFileId) ? scopedFileId : fileId) + "."
            };
        }

        private string UploadPackageFile(string filePath, out string errorMessage)
        {
            FileInfo info = new FileInfo(filePath);
            if (info.Length <= SinglePartUploadLimitBytes)
                return UploadSinglePart(filePath, info, out errorMessage);

            return UploadMultipart(filePath, info, out errorMessage);
        }

        private string UploadSinglePart(string filePath, FileInfo info, out string errorMessage)
        {
            errorMessage = null;
            var request = new Dictionary<string, object>();
            request["size_bytes"] = info.Length;
            request["filename"] = info.Name;

            NexusV3RestResult create = _client.Post("/uploads", request);
            if (!string.IsNullOrEmpty(create.ErrorMessage))
            {
                errorMessage = create.ErrorMessage;
                return null;
            }

            string uploadId = ReadString(create.Data, "id");
            string url = ReadString(create.Data, "presigned_url");
            NexusV3UploadResult upload = _client.PutFile(url, filePath);
            if (!upload.Success)
            {
                errorMessage = upload.ErrorMessage ?? "Nexus upload failed.";
                return null;
            }

            return FinaliseUpload(uploadId, out errorMessage);
        }

        private string UploadMultipart(string filePath, FileInfo info, out string errorMessage)
        {
            errorMessage = null;
            var request = new Dictionary<string, object>();
            request["size_bytes"] = info.Length;
            request["filename"] = info.Name;

            NexusV3RestResult create = _client.Post("/uploads/multipart", request);
            if (!string.IsNullOrEmpty(create.ErrorMessage))
            {
                errorMessage = create.ErrorMessage;
                return null;
            }

            string uploadId = ReadString(create.Data, "id");
            long partSize = ReadLong(create.Data, "part_size_bytes");
            object[] urls = AsArray(create.Data, "part_presigned_urls");
            string completeUrl = ReadString(create.Data, "complete_presigned_url");
            if (string.IsNullOrEmpty(uploadId) || partSize <= 0 || urls == null || urls.Length == 0)
            {
                errorMessage = "Nexus did not return a valid multipart upload session.";
                return null;
            }

            var etags = new List<string>();
            using (FileStream stream = File.OpenRead(filePath))
            {
                for (int i = 0; i < urls.Length; i++)
                {
                    string url = Convert.ToString(urls[i]);
                    int length = (int)Math.Min(partSize, stream.Length - stream.Position);
                    byte[] bytes = new byte[length];
                    int read = stream.Read(bytes, 0, bytes.Length);
                    if (read != bytes.Length)
                    {
                        errorMessage = "Could not read upload package part " + (i + 1) + ".";
                        return null;
                    }

                    NexusV3UploadResult upload = _client.PutBytes(url, bytes);
                    if (!upload.Success)
                    {
                        errorMessage = upload.ErrorMessage ?? ("Multipart upload failed at part " + (i + 1) + ".");
                        return null;
                    }
                    etags.Add(upload.ETag ?? string.Empty);
                }
            }

            NexusV3UploadResult complete = _client.PostXml(completeUrl, BuildMultipartCompleteXml(etags));
            if (!complete.Success)
            {
                errorMessage = complete.ErrorMessage ?? "Multipart upload completion failed.";
                return null;
            }

            return FinaliseUpload(uploadId, out errorMessage);
        }

        private string FinaliseUpload(string uploadId, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(uploadId))
            {
                errorMessage = "Nexus upload id was empty.";
                return null;
            }

            NexusV3RestResult finalise = _client.Post("/uploads/" + Escape(uploadId) + "/finalise", new Dictionary<string, object>());
            if (!string.IsNullOrEmpty(finalise.ErrorMessage))
            {
                errorMessage = finalise.ErrorMessage;
                return null;
            }

            return uploadId;
        }

        private void WaitForUploadAvailable(string uploadId, out string errorMessage)
        {
            errorMessage = null;
            for (int i = 0; i < 12; i++)
            {
                NexusV3RestResult upload = _client.Get("/uploads/" + Escape(uploadId));
                if (!string.IsNullOrEmpty(upload.ErrorMessage))
                {
                    errorMessage = upload.ErrorMessage;
                    return;
                }

                string state = ReadString(upload.Data, "state");
                if (string.Equals(state, "available", StringComparison.OrdinalIgnoreCase))
                    return;

                Thread.Sleep(2500);
            }

            errorMessage = "Nexus upload did not become available in time.";
        }

        private static Dictionary<string, object> BuildModFileRequest(NexusUploadDraft draft, string uploadId)
        {
            var request = new Dictionary<string, object>();
            request["upload_id"] = uploadId;
            request["name"] = Truncate(draft.Name, 50);
            request["version"] = Truncate(draft.Version, 50);
            request["description"] = draft.Description ?? string.Empty;
            request["file_category"] = NormalizeFileCategory(draft.FileCategory);
            request["primary_mod_manager_download"] = draft.PrimaryModManagerDownload;
            request["allow_mod_manager_download"] = draft.AllowModManagerDownload;
            request["show_requirements_pop_up"] = draft.ShowRequirementsPopup;
            if (!string.IsNullOrEmpty(draft.UpdateGroupId))
                request["archive_existing_file"] = draft.ArchiveExistingFile;
            return request;
        }

        private void PopulateLatestFileState(NexusRemoteMod mod, ref string errorMessage)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Uid))
                return;

            string groupsError;
            List<NexusModFileUpdateGroup> groups = GetModFileUpdateGroups(mod.Uid, out groupsError);
            if (!string.IsNullOrEmpty(groupsError))
            {
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = groupsError;
                return;
            }

            NexusRemoteModFile latest = null;
            for (int i = 0; i < groups.Count; i++)
            {
                string versionError;
                List<NexusRemoteModFile> files = GetFileUpdateGroupVersions(groups[i].Id, out versionError);
                if (!string.IsNullOrEmpty(versionError))
                {
                    if (string.IsNullOrEmpty(errorMessage))
                        errorMessage = versionError;
                    continue;
                }

                NexusRemoteModFile candidate = SelectPreferredInstallFile(files, true);
                if (candidate == null)
                    continue;

                if (latest == null || CompareNullableDates(candidate.UploadedAtUtc, latest.UploadedAtUtc) > 0)
                    latest = candidate;
            }

            if (latest != null)
            {
                mod.Version = latest.Version;
                mod.UpdatedAtUtc = latest.UploadedAtUtc;
            }
        }

        private NexusRemoteModFile SelectPreferredInstallFile(List<NexusRemoteModFile> files, bool includePrerelease, bool prereleaseOnly)
        {
            NexusRemoteModFile best = null;
            foreach (var file in files)
            {
                if (file == null)
                    continue;

                bool isPrerelease = NexusReleaseClassifier.IsPrerelease(file);
                if (!includePrerelease && isPrerelease)
                    continue;
                if (prereleaseOnly && !isPrerelease)
                    continue;

                if (best == null)
                {
                    best = file;
                    continue;
                }

                bool fileIsMain = string.Equals(file.Category, "main", StringComparison.OrdinalIgnoreCase);
                bool bestIsMain = string.Equals(best.Category, "main", StringComparison.OrdinalIgnoreCase);
                if (fileIsMain && !bestIsMain)
                {
                    best = file;
                    continue;
                }

                if (fileIsMain == bestIsMain && CompareNullableDates(file.UploadedAtUtc, best.UploadedAtUtc) > 0)
                    best = file;
            }

            return best;
        }

        private static List<NexusModReference> GetDistinctReferences(IEnumerable<NexusModReference> references)
        {
            var list = new List<NexusModReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (references == null)
                return list;

            foreach (var reference in references)
            {
                if (reference == null || !reference.IsValid)
                    continue;
                if (seen.Contains(reference.Key))
                    continue;
                seen.Add(reference.Key);
                list.Add(reference);
            }

            return list;
        }

        private static NexusRemoteMod ParseV3Mod(Dictionary<string, object> node, string fallbackDomain)
        {
            if (node == null)
                return null;

            var mod = new NexusRemoteMod();
            mod.Uid = ReadString(node, "id");
            mod.ModId = ReadInt(node, "game_scoped_id");
            mod.GameId = ReadInt(node, "game_id");
            mod.Name = ReadString(node, "name");
            mod.GameDomain = NormalizeDomain(fallbackDomain);
            mod.Status = ReadString(node, "status");
            return mod;
        }

        private static NexusRemoteMod ParseTrendingMod(Dictionary<string, object> node, string fallbackDomain)
        {
            if (node == null)
                return null;

            var mod = new NexusRemoteMod();
            mod.Name = ReadString(node, "name");
            mod.Author = ReadString(node, "author");
            mod.UploaderName = mod.Author;
            mod.Summary = ReadString(node, "summary");
            mod.PictureUrl = ReadString(node, "picture_url");
            mod.ThumbnailUrl = mod.PictureUrl;
            mod.GameDomain = NormalizeDomain(fallbackDomain);
            mod.ModId = ParseModIdFromUrl(ReadString(node, "mod_page_url"));
            return mod;
        }

        private static NexusRemoteModFile ParseMinimalFile(Dictionary<string, object> node, string groupId)
        {
            if (node == null)
                return null;

            var file = new NexusRemoteModFile();
            file.Id = ReadString(node, "id");
            file.UpdateGroupId = groupId;
            file.FileId = ReadInt(node, "game_scoped_id");
            file.Name = ReadString(node, "name");
            file.Version = ReadString(node, "version");
            file.Category = ReadString(node, "category");
            file.UploadedAtUtc = ReadDateTime(node, "uploaded_at");
            file.Primary = ReadBool(node, "is_primary") ? 1 : 0;
            file.Manager = 1;
            return file;
        }

        private static Dictionary<string, object> AsDictionary(Dictionary<string, object> parent, string key)
        {
            if (parent == null || string.IsNullOrEmpty(key))
                return null;
            object value;
            if (!parent.TryGetValue(key, out value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static object[] AsArray(Dictionary<string, object> parent, string key)
        {
            if (parent == null || string.IsNullOrEmpty(key))
                return null;
            object value;
            if (!parent.TryGetValue(key, out value))
                return null;
            return value as object[];
        }

        private static string ReadString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key))
                return string.Empty;
            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value);
        }

        private static int ReadInt(Dictionary<string, object> dict, string key)
        {
            long value = ReadLong(dict, key);
            if (value > int.MaxValue || value < int.MinValue)
                return 0;
            return (int)value;
        }

        private static long ReadLong(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key))
                return 0;
            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return 0;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is double) return (long)(double)value;
            if (value is decimal) return (long)(decimal)value;
            long parsed;
            return long.TryParse(Convert.ToString(value), out parsed) ? parsed : 0;
        }

        private static bool ReadBool(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key))
                return false;
            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return false;
            if (value is bool) return (bool)value;
            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) && parsed;
        }

        private static DateTime? ReadDateTime(Dictionary<string, object> dict, string key)
        {
            string text = ReadString(dict, key);
            if (string.IsNullOrEmpty(text))
                return null;
            DateTime parsed;
            if (DateTime.TryParse(text, out parsed))
            {
                if (parsed.Kind == DateTimeKind.Unspecified)
                    return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                return parsed.ToUniversalTime();
            }
            return null;
        }

        private static int CompareNullableDates(DateTime? left, DateTime? right)
        {
            if (!left.HasValue && !right.HasValue) return 0;
            if (left.HasValue && !right.HasValue) return 1;
            if (!left.HasValue) return -1;
            return DateTime.Compare(left.Value, right.Value);
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private static string NormalizeDomain(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeFileCategory(string value)
        {
            string category = string.IsNullOrEmpty(value) ? "main" : value.Trim().ToLowerInvariant();
            if (category == "optional" || category == "miscellaneous")
                return category;
            return "main";
        }

        private static string Truncate(string value, int maxLength)
        {
            string text = value ?? string.Empty;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }

        private static int ParseModIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return 0;
            Match match = Regex.Match(url, @"/mods/(?<id>\d+)", RegexOptions.IgnoreCase);
            int parsed;
            return match.Success && int.TryParse(match.Groups["id"].Value, out parsed) ? parsed : 0;
        }

        private static string BuildMultipartCompleteXml(IList<string> etags)
        {
            var sb = new StringBuilder();
            sb.Append("<CompleteMultipartUpload>");
            for (int i = 0; i < etags.Count; i++)
            {
                sb.Append("<Part><PartNumber>");
                sb.Append(i + 1);
                sb.Append("</PartNumber><ETag>");
                sb.Append(System.Security.SecurityElement.Escape(etags[i] ?? string.Empty));
                sb.Append("</ETag></Part>");
            }
            sb.Append("</CompleteMultipartUpload>");
            return sb.ToString();
        }
    }
}
