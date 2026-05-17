using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Manager.Core;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Nexus Mods v3 API operations used by Manager.
    /// </summary>
    public class NexusModsService
    {
        private const long SinglePartUploadLimitBytes = 100L * 1024L * 1024L;
        private const string NexusApiBaseUrlV1 = "https://api.nexusmods.com/v1";
        private static readonly TimeSpan MetadataCacheTtl = TimeSpan.FromMinutes(5);
        private readonly NexusV3RestClient _client;
        private readonly string _apiKey;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, CacheEntry<NexusRemoteMod>> _modCache = new Dictionary<string, CacheEntry<NexusRemoteMod>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteMod>>> _latestCache = new Dictionary<string, CacheEntry<List<NexusRemoteMod>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusModFileUpdateGroup>>> _groupCache = new Dictionary<string, CacheEntry<List<NexusModFileUpdateGroup>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteModFile>>> _versionCache = new Dictionary<string, CacheEntry<List<NexusRemoteModFile>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteModFile>>> _fileListCache = new Dictionary<string, CacheEntry<List<NexusRemoteModFile>>>(StringComparer.OrdinalIgnoreCase);
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        private sealed class CacheEntry<T>
        {
            public T Value;
            public DateTime CreatedUtc;
        }

        public NexusModsService(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _client = new NexusV3RestClient(apiKey);
        }

        public void ClearCachedResponses()
        {
            lock (_cacheLock)
            {
                _modCache.Clear();
                _latestCache.Clear();
                _groupCache.Clear();
                _versionCache.Clear();
                _fileListCache.Clear();
            }
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

            string cacheKey = "latest:" + NormalizeDomain(gameDomain) + ":" + count.ToString();
            List<NexusRemoteMod> cachedLatest;
            if (TryGetCached(_latestCache, cacheKey, out cachedLatest))
                return cachedLatest;

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

            StoreCached(_latestCache, cacheKey, list);
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

            string cacheKey = "mod:" + NormalizeDomain(gameDomain) + ":" + modId.ToString();
            NexusRemoteMod cached;
            if (TryGetCached(_modCache, cacheKey, out cached))
                return cached;

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
            {
                string fileStateWarning;
                PopulateLatestFileState(mod, out fileStateWarning);
                if (!string.IsNullOrEmpty(fileStateWarning))
                    System.Diagnostics.Debug.WriteLine("Nexus file state warning: " + fileStateWarning);
                StoreCached(_modCache, cacheKey, mod);
            }

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

            string cacheKey = "groups:" + modUniqueId;
            List<NexusModFileUpdateGroup> cached;
            if (TryGetCached(_groupCache, cacheKey, out cached))
                return cached;

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

            StoreCached(_groupCache, cacheKey, groups);
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

            string cacheKey = "versions:" + groupId;
            List<NexusRemoteModFile> cached;
            if (TryGetCached(_versionCache, cacheKey, out cached))
                return cached;

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

            StoreCached(_versionCache, cacheKey, files);
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

            if (string.IsNullOrEmpty(gameDomain) || modId <= 0)
            {
                errorMessage = "Invalid Nexus file query.";
                return files;
            }

            string cacheKey = "files:" + NormalizeDomain(gameDomain) + ":" + modId.ToString();
            List<NexusRemoteModFile> cached;
            if (TryGetCached(_fileListCache, cacheKey, out cached))
                return cached;

            string v3Error = null;
            NexusRemoteMod mod = GetModByDomainAndId(gameDomain, modId, out v3Error);
            if (string.IsNullOrEmpty(v3Error) && mod != null && !string.IsNullOrEmpty(mod.Uid))
            {
                List<NexusModFileUpdateGroup> groups = GetModFileUpdateGroups(mod.Uid, out v3Error);
                if (string.IsNullOrEmpty(v3Error))
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        string versionError;
                        List<NexusRemoteModFile> groupFiles = GetFileUpdateGroupVersions(groups[i].Id, out versionError);
                        if (!string.IsNullOrEmpty(versionError) && string.IsNullOrEmpty(v3Error))
                            v3Error = versionError;
                        files.AddRange(groupFiles);
                    }
                }
            }

            if (HasManagerInstallableFile(files))
            {
                StoreCached(_fileListCache, cacheKey, files);
                return files;
            }

            string legacyError;
            List<NexusRemoteModFile> legacyFiles = GetLegacyModFiles(gameDomain, modId, out legacyError);
            if (legacyFiles.Count > 0)
            {
                StoreCached(_fileListCache, cacheKey, legacyFiles);
                return legacyFiles;
            }

            if (!string.IsNullOrEmpty(v3Error))
            {
                errorMessage = v3Error;
                if (!string.IsNullOrEmpty(legacyError))
                    errorMessage += " Legacy file fallback also failed: " + legacyError;
                return files;
            }

            if (files.Count > 0)
            {
                StoreCached(_fileListCache, cacheKey, files);
                return files;
            }

            errorMessage = !string.IsNullOrEmpty(legacyError)
                ? legacyError
                : "Nexus did not return any files for this mod.";
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
            return GetLegacyDownloadUrl(gameDomain, modId, fileId, apiKey, out errorMessage);
        }

        public string GetDownloadUrlWithAuthorization(string gameDomain, int modId, int fileId, string apiKey, string downloadKey, long expires, out string errorMessage)
        {
            return GetLegacyDownloadUrl(gameDomain, modId, fileId, apiKey, downloadKey, expires, out errorMessage);
        }

        public NexusAccountStatus GetAccountStatus(out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(_apiKey))
                return NexusAccountStatus.CreateNotConfigured();

            Dictionary<string, object> data = SendLegacyGet("/users/validate", null, null, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage) || data == null)
                return NexusAccountStatus.CreateUnavailable(errorMessage);

            var status = new NexusAccountStatus();
            status.IsConfigured = true;
            status.IsConnected = true;
            status.UserId = ReadInt(data, "user_id");
            status.UserName = ReadString(data, "name");
            bool isPremium = ReadBool(data, "is_premium");
            bool isSupporter = ReadBool(data, "is_supporter");
            if (isPremium)
                status.MembershipRoles = new[] { "premium" };
            else if (isSupporter)
                status.MembershipRoles = new[] { "supporter" };
            else
                status.MembershipRoles = new[] { "member" };

            status.Summary = "Connected to Nexus as " +
                (!string.IsNullOrEmpty(status.UserName) ? status.UserName : "user " + status.UserId.ToString()) +
                " (" + status.GetMembershipLabel() + ").";

            if (isPremium || isSupporter)
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Available;
                status.DirectDownloadSummary = "Direct installs should usually work, but Nexus can still deny specific files or unapproved app access.";
            }
            else
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Limited;
                status.DirectDownloadSummary = "Browsing and update checks work. Direct installs may require Nexus mod-manager download authorization for non-premium accounts.";
            }

            return status;
        }

        private string GetLegacyDownloadUrl(string gameDomain, int modId, int fileId, string apiKey, out string errorMessage)
        {
            return GetLegacyDownloadUrl(gameDomain, modId, fileId, apiKey, string.Empty, 0, out errorMessage);
        }

        private string GetLegacyDownloadUrl(string gameDomain, int modId, int fileId, string apiKey, string downloadKey, long expires, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(gameDomain) || modId <= 0 || fileId <= 0)
            {
                errorMessage = "Invalid parameters for download URL request.";
                return null;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                errorMessage = "Nexus API key is required for direct Manager download.";
                return null;
            }

            var query = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(downloadKey) && expires > 0)
            {
                query["key"] = downloadKey;
                query["expires"] = expires.ToString();
            }

            Dictionary<string, object> data = SendLegacyGet(
                "/games/" + NormalizeDomain(gameDomain) + "/mods/" + modId + "/files/" + fileId + "/download_link",
                apiKey,
                query,
                out errorMessage);

            if (!string.IsNullOrEmpty(errorMessage))
                return null;

            string downloadUrl = ExtractDownloadUrl(data);
            if (!string.IsNullOrEmpty(downloadUrl))
                return downloadUrl;

            errorMessage = "Nexus response did not include a usable download URI.";
            return null;
        }

        private Dictionary<string, object> SendLegacyGet(
            string relativePath,
            string apiKeyOverride,
            Dictionary<string, string> query,
            out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string url = BuildLegacyUrl(relativePath, query);
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;
                string key = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _apiKey;
                NexusRequestHeaders.ApplyJsonHeaders(request, key);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                {
                    string json = reader != null ? reader.ReadToEnd() : string.Empty;
                    if (string.IsNullOrEmpty(json))
                        return new Dictionary<string, object>();

                    object parsed = _serializer.DeserializeObject(json);
                    Dictionary<string, object> dict = parsed as Dictionary<string, object>;
                    if (dict != null)
                        return dict;

                    object[] array = parsed as object[];
                    if (array != null)
                    {
                        var wrapper = new Dictionary<string, object>();
                        wrapper["data"] = array;
                        return wrapper;
                    }

                    return new Dictionary<string, object>();
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        errorMessage = "Unauthorized Nexus request. Check the API key.";
                        return null;
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        errorMessage = "Nexus denied this request for the current account, file, or app.";
                        return null;
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        errorMessage = "Nexus rate limited the request. Wait and try again.";
                        return null;
                    }
                }

                errorMessage = "Nexus request failed: " + ex.Message;
                return null;
            }
            catch (Exception ex)
            {
                errorMessage = "Nexus request failed: " + ex.Message;
                return null;
            }
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

        private void PopulateLatestFileState(NexusRemoteMod mod, out string warningMessage)
        {
            warningMessage = null;
            if (mod == null || string.IsNullOrEmpty(mod.Uid))
                return;

            string groupsError;
            List<NexusModFileUpdateGroup> groups = GetModFileUpdateGroups(mod.Uid, out groupsError);
            if (!string.IsNullOrEmpty(groupsError))
            {
                warningMessage = groupsError;
                return;
            }

            NexusRemoteModFile latest = null;
            for (int i = 0; i < groups.Count; i++)
            {
                string versionError;
                List<NexusRemoteModFile> files = GetFileUpdateGroupVersions(groups[i].Id, out versionError);
                if (!string.IsNullOrEmpty(versionError))
                {
                    if (string.IsNullOrEmpty(warningMessage))
                        warningMessage = versionError;
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
                if (!IsManagerDownloadEnabled(file))
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

        private List<NexusRemoteModFile> GetLegacyModFiles(string gameDomain, int modId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            if (string.IsNullOrEmpty(gameDomain) || modId <= 0)
            {
                errorMessage = "Invalid Nexus v1 file query.";
                return files;
            }

            Dictionary<string, object> data = SendLegacyGet(
                "/games/" + NormalizeDomain(gameDomain) + "/mods/" + modId + "/files",
                null,
                null,
                out errorMessage);

            if (!string.IsNullOrEmpty(errorMessage) || data == null)
                return files;

            object[] rawFiles = AsArray(data, "files");
            if (rawFiles == null)
                rawFiles = AsArray(data, "data");
            if (rawFiles == null)
                return files;

            for (int i = 0; i < rawFiles.Length; i++)
            {
                var node = rawFiles[i] as Dictionary<string, object>;
                NexusRemoteModFile file = ParseLegacyFile(node);
                if (file != null)
                    files.Add(file);
            }

            return files;
        }

        private static bool HasManagerInstallableFile(List<NexusRemoteModFile> files)
        {
            if (files == null)
                return false;

            for (int i = 0; i < files.Count; i++)
            {
                if (IsManagerDownloadEnabled(files[i]))
                    return true;
            }

            return false;
        }

        private static bool IsManagerDownloadEnabled(NexusRemoteModFile file)
        {
            return file != null && file.FileId > 0 && file.Manager > 0;
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
            file.Primary = ReadFirstIntLike(node, "primary", "is_primary");
            file.Manager = ReadFirstIntLike(node, "manager", "allow_mod_manager_download", "mod_manager_download");
            return file;
        }

        private static NexusRemoteModFile ParseLegacyFile(Dictionary<string, object> node)
        {
            if (node == null)
                return null;

            var file = new NexusRemoteModFile();
            file.Id = ReadFirstString(node, "uid", "id");
            file.FileId = ReadFirstIntLike(node, "file_id", "fileId", "game_scoped_id");
            file.Name = ReadFirstString(node, "name", "file_name");
            file.Version = ReadFirstString(node, "version", "mod_version");
            file.UnixDate = ReadFirstIntLike(node, "uploaded_timestamp", "date");
            file.Category = ReadFirstString(node, "category_name", "category");
            file.Primary = ReadFirstIntLike(node, "is_primary", "primary");
            file.Manager = ReadFirstIntLike(node, "manager", "allow_mod_manager_download", "mod_manager_download");
            file.Uri = ReadFirstString(node, "uri", "URI", "download_url", "downloadUrl");
            return file.FileId > 0 ? file : null;
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

        private static int ReadFirstIntLike(Dictionary<string, object> dict, params string[] keys)
        {
            if (dict == null || keys == null)
                return 0;

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (string.IsNullOrEmpty(key))
                    continue;

                object value;
                if (!dict.TryGetValue(key, out value) || value == null)
                    continue;

                if (value is bool)
                    return (bool)value ? 1 : 0;

                int parsed = ReadInt(dict, key);
                if (parsed != 0)
                    return parsed;

                string text = Convert.ToString(value);
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    return 0;
            }

            return 0;
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

        private static string BuildLegacyUrl(string relativePath, Dictionary<string, string> query)
        {
            string path = relativePath ?? string.Empty;
            if (!path.StartsWith("/", StringComparison.Ordinal))
                path = "/" + path;

            if (query == null || query.Count == 0)
                return NexusApiBaseUrlV1 + path;

            var sb = new StringBuilder();
            sb.Append(NexusApiBaseUrlV1);
            sb.Append(path);
            bool first = true;
            foreach (var pair in query)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                    continue;

                sb.Append(first ? "?" : "&");
                sb.Append(Uri.EscapeDataString(pair.Key));
                sb.Append("=");
                sb.Append(Uri.EscapeDataString(pair.Value));
                first = false;
            }

            return sb.ToString();
        }

        private bool TryGetCached<T>(Dictionary<string, CacheEntry<T>> cache, string key, out T value)
        {
            value = default(T);
            if (cache == null || string.IsNullOrEmpty(key))
                return false;

            lock (_cacheLock)
            {
                CacheEntry<T> entry;
                if (!cache.TryGetValue(key, out entry) || entry == null)
                    return false;

                if ((DateTime.UtcNow - entry.CreatedUtc) > MetadataCacheTtl)
                {
                    cache.Remove(key);
                    return false;
                }

                value = entry.Value;
                return true;
            }
        }

        private void StoreCached<T>(Dictionary<string, CacheEntry<T>> cache, string key, T value)
        {
            if (cache == null || string.IsNullOrEmpty(key))
                return;

            lock (_cacheLock)
            {
                cache[key] = new CacheEntry<T>
                {
                    Value = value,
                    CreatedUtc = DateTime.UtcNow
                };
            }
        }

        private static string ExtractDownloadUrl(object parsed)
        {
            if (parsed == null)
                return string.Empty;

            object[] array = parsed as object[];
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    string url = ExtractDownloadUrl(array[i]);
                    if (!string.IsNullOrEmpty(url))
                        return url;
                }

                return string.Empty;
            }

            Dictionary<string, object> dict = parsed as Dictionary<string, object>;
            if (dict == null)
                return string.Empty;

            string direct = ReadFirstString(dict, "URI", "uri", "url", "download_url", "downloadUrl");
            if (!string.IsNullOrEmpty(direct))
                return direct;

            object nested;
            if (dict.TryGetValue("data", out nested))
                return ExtractDownloadUrl(nested);

            return string.Empty;
        }

        private static string ReadFirstString(Dictionary<string, object> dict, params string[] keys)
        {
            if (dict == null || keys == null)
                return string.Empty;

            for (int i = 0; i < keys.Length; i++)
            {
                string value = ReadString(dict, keys[i]);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return string.Empty;
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
