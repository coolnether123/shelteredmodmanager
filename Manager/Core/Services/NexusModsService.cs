using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Manager.Core;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Nexus Mods operations used by Manager.
    /// </summary>
    public class NexusModsService
    {
        private const long SinglePartUploadLimitBytes = 100L * 1024L * 1024L;
        private const string NexusApiBaseUrlV1 = "https://api.nexusmods.com/v1";
        private const string V2ModFields = @"
      modId
      uid
      name
      author
      uploader { name }
      version
      summary
      createdAt
      updatedAt
      downloads
      endorsements
      pictureUrl
      thumbnailUrl
      game { id domainName }";
        private static readonly TimeSpan MetadataCacheTtl = TimeSpan.FromMinutes(5);
        private readonly NexusGraphQlClient _v2Client;
        private readonly NexusV3RestClient _v3Client;
        private readonly string _apiKey;
        private readonly INexusCredentialProvider _credentialProvider;
        private readonly NexusRateLimitTracker _rateLimits;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, CacheEntry<NexusRemoteMod>> _modCache = new Dictionary<string, CacheEntry<NexusRemoteMod>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteMod>>> _latestCache = new Dictionary<string, CacheEntry<List<NexusRemoteMod>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusV3ModFileRecord>>> _v3ModFileCache = new Dictionary<string, CacheEntry<List<NexusV3ModFileRecord>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteModFile>>> _versionCache = new Dictionary<string, CacheEntry<List<NexusRemoteModFile>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<NexusRemoteModFile>>> _fileListCache = new Dictionary<string, CacheEntry<List<NexusRemoteModFile>>>(StringComparer.OrdinalIgnoreCase);
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private int _cacheGeneration;

        private sealed class CacheEntry<T>
        {
            public T Value;
            public DateTime CreatedUtc;
        }

        public NexusModsService(string apiKey)
            : this(new StaticNexusCredentialProvider(apiKey), apiKey)
        {
        }

        internal NexusModsService(INexusCredentialProvider credentialProvider)
            : this(credentialProvider, string.Empty)
        {
        }

        private NexusModsService(INexusCredentialProvider credentialProvider, string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _credentialProvider = credentialProvider ?? new StaticNexusCredentialProvider(_apiKey);
            _rateLimits = new NexusRateLimitTracker();
            _v2Client = new NexusGraphQlClient(_credentialProvider, _rateLimits, null);
            _v3Client = new NexusV3RestClient(_credentialProvider, _rateLimits, null);
        }

        public void ClearCachedResponses()
        {
            lock (_cacheLock)
            {
                _cacheGeneration++;
                _modCache.Clear();
                _latestCache.Clear();
                _v3ModFileCache.Clear();
                _versionCache.Clear();
                _fileListCache.Clear();
            }
        }

        public Dictionary<string, NexusRemoteMod> GetModsByReferences(IEnumerable<NexusModReference> references, out string errorMessage)
        {
            errorMessage = null;
            var results = new Dictionary<string, NexusRemoteMod>(StringComparer.OrdinalIgnoreCase);
            var distinct = GetDistinctReferences(references);

            const int chunkSize = 40;
            for (int i = 0; i < distinct.Count; i += chunkSize)
            {
                int size = Math.Min(chunkSize, distinct.Count - i);
                var chunk = distinct.GetRange(i, size);
                string chunkError;
                Dictionary<string, NexusRemoteMod> chunkResult = QueryV2ModsByLegacyDomainIds(chunk, out chunkError);
                if (!string.IsNullOrEmpty(chunkError))
                {
                    errorMessage = chunkError;
                    continue;
                }

                foreach (var pair in chunkResult)
                {
                    results[pair.Key] = pair.Value;
                }
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

            int cacheGeneration = GetCacheGeneration();
            string v2Error;
            list = QueryV2LatestMods(gameDomain, count, out v2Error);
            if (string.IsNullOrEmpty(v2Error))
            {
                StoreCached(_latestCache, cacheKey, list, cacheGeneration);
                return list;
            }

            errorMessage = v2Error;
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

            int cacheGeneration = GetCacheGeneration();
            var refs = new List<NexusModReference>();
            refs.Add(new NexusModReference { GameDomain = gameDomain, ModId = modId });
            string v2Error;
            Dictionary<string, NexusRemoteMod> found = QueryV2ModsByLegacyDomainIds(refs, out v2Error);
            if (string.IsNullOrEmpty(v2Error))
            {
                NexusRemoteMod v2Mod;
                if (found.TryGetValue(NormalizeDomain(gameDomain) + ":" + modId.ToString(), out v2Mod))
                {
                    StoreCached(_modCache, cacheKey, v2Mod, cacheGeneration);
                    return v2Mod;
                }

                errorMessage = "Nexus did not return mod details.";
                return null;
            }

            errorMessage = v2Error;
            return null;
        }

        public List<NexusRemoteMod> FindModsByName(string gameDomain, string modName, int count, out string errorMessage)
        {
            var matches = new List<NexusRemoteMod>();

            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(modName))
            {
                errorMessage = "Game domain and mod name are required for Nexus name search.";
                return matches;
            }

            matches = QueryV2ModsByName(gameDomain, modName, count, out errorMessage);
            if (matches.Count == 0 && string.IsNullOrEmpty(errorMessage))
                errorMessage = "No matching Nexus mod was found.";
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

        private List<NexusV3ModFileRecord> GetV3ModFiles(string modUniqueId, out string errorMessage)
        {
            errorMessage = null;
            var modFiles = new List<NexusV3ModFileRecord>();
            if (string.IsNullOrEmpty(modUniqueId))
            {
                errorMessage = "Nexus v3 mod id is required.";
                return modFiles;
            }

            string cacheKey = "v3-mod-files:" + modUniqueId;
            List<NexusV3ModFileRecord> cached;
            if (TryGetCached(_v3ModFileCache, cacheKey, out cached))
                return cached;

            int cacheGeneration = GetCacheGeneration();
            NexusV3RestResult response = _v3Client.Get("/mods/" + Escape(modUniqueId) + "/files");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return modFiles;
            }

            object[] rawModFiles = AsArray(response.Data, "mod_files");
            if (rawModFiles == null)
                return modFiles;

            for (int i = 0; i < rawModFiles.Length; i++)
            {
                var node = rawModFiles[i] as Dictionary<string, object>;
                if (node == null) continue;

                var modFile = new NexusV3ModFileRecord();
                modFile.Id = ReadString(node, "id");
                modFile.Name = ReadString(node, "name");
                modFile.IsActive = ReadBool(node, "is_active");
                modFile.LastFileUploadedAtUtc = ReadDateTime(node, "last_file_uploaded_at");
                modFile.VersionsCount = ReadInt(node, "versions_count");
                if (!string.IsNullOrEmpty(modFile.Id))
                    modFiles.Add(modFile);
            }

            StoreCached(_v3ModFileCache, cacheKey, modFiles, cacheGeneration);
            return modFiles;
        }

        private List<NexusRemoteModFile> GetV3ModFileVersions(string modFileId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            if (string.IsNullOrEmpty(modFileId))
            {
                errorMessage = "Nexus v3 mod file id is required.";
                return files;
            }

            string cacheKey = "v3-versions:" + modFileId;
            List<NexusRemoteModFile> cached;
            if (TryGetCached(_versionCache, cacheKey, out cached))
                return cached;

            int cacheGeneration = GetCacheGeneration();
            NexusV3RestResult response = _v3Client.Get("/mod-files/" + Escape(modFileId) + "/versions");
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
                NexusRemoteModFile file = ParseMinimalFile(version, modFileId);
                if (file != null)
                    files.Add(file);
            }

            StoreCached(_versionCache, cacheKey, files, cacheGeneration);
            return files;
        }

        public List<NexusRemoteModFile> GetModFiles(int gameId, int modId, out string errorMessage)
        {
            return QueryV2ModFiles(gameId, modId, out errorMessage);
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

            int cacheGeneration = GetCacheGeneration();
            string readError = null;
            NexusRemoteMod mod = GetModByDomainAndId(gameDomain, modId, out readError);
            if (string.IsNullOrEmpty(readError) && mod != null && mod.GameId > 0)
            {
                string v2FilesError;
                List<NexusRemoteModFile> v2Files = QueryV2ModFiles(mod.GameId, modId, out v2FilesError);
                if (v2Files.Count > 0)
                {
                    StoreCached(_fileListCache, cacheKey, v2Files, cacheGeneration);
                    return v2Files;
                }

                if (!string.IsNullOrEmpty(v2FilesError))
                    readError = v2FilesError;
            }

            string legacyError;
            List<NexusRemoteModFile> legacyFiles = GetLegacyModFiles(gameDomain, modId, out legacyError);
            if (legacyFiles.Count > 0)
            {
                StoreCached(_fileListCache, cacheKey, legacyFiles, cacheGeneration);
                return legacyFiles;
            }

            if (!string.IsNullOrEmpty(readError))
            {
                errorMessage = readError;
                if (!string.IsNullOrEmpty(legacyError))
                    errorMessage += " Legacy file fallback also failed: " + legacyError;
                return files;
            }

            if (files.Count > 0)
            {
                StoreCached(_fileListCache, cacheKey, files, cacheGeneration);
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

        public string GetDownloadUrl(string gameDomain, int modId, int fileId, string apiKey, out string errorMessage)
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
            if (!_credentialProvider.HasConfiguredCredential)
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

            if (isPremium)
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Available;
                status.DirectDownloadSummary = "Premium direct installs should usually work, but Nexus can still deny specific files or unapproved app access.";
            }
            else
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Limited;
                status.DirectDownloadSummary = "Browsing and update checks work. Downloads use a short-lived Nexus website authorization for non-premium accounts.";
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

            if (string.IsNullOrEmpty(apiKey) && !_credentialProvider.HasConfiguredCredential)
            {
                errorMessage = "Nexus sign-in or a personal API key is required for direct Manager download.";
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
            NexusRateLimitTracker.Lease rateLimitLease = null;
            bool requestSubmitted = false;
            try
            {
                string url = BuildLegacyUrl(relativePath, query);
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;
                NexusRequestCredential credential;
                if (!string.IsNullOrEmpty(apiKeyOverride))
                {
                    credential = NexusRequestCredential.FromApiKey(apiKeyOverride);
                }
                else
                {
                    string credentialError;
                    credential = _credentialProvider.GetCredential(out credentialError);
                    if (!string.IsNullOrEmpty(credentialError) &&
                        (credential == null || !credential.IsConfigured))
                    {
                        errorMessage = credentialError;
                        return null;
                    }
                }

                string credentialScope = credential != null ? credential.RateLimitScope : string.Empty;
                if (!_rateLimits.TryAcquire(credentialScope, out rateLimitLease, out errorMessage))
                    return null;

                NexusRequestHeaders.ApplyJsonHeaders(request, credential);

                requestSubmitted = true;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                {
                    _rateLimits.Observe(response, rateLimitLease);
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
                if (NexusRequestFailurePolicy.IsDefinitelyUnsent(ex))
                    _rateLimits.Release(rateLimitLease);

                using (var response = ex.Response as HttpWebResponse)
                {
                    if (response != null)
                    {
                        _rateLimits.Observe(response, rateLimitLease);
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            errorMessage = "Unauthorized Nexus request. Sign in again or check the legacy API key.";
                            return null;
                        }

                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            errorMessage = "Nexus denied this request for the current account, file, or app.";
                            return null;
                        }

                        if ((int)response.StatusCode == 429)
                        {
                            if (!_rateLimits.TryGetBlockingMessage(
                                rateLimitLease != null ? rateLimitLease.CredentialScope : string.Empty,
                                out errorMessage))
                                errorMessage = "Nexus rate limited the request. Wait and try again.";
                            return null;
                        }
                    }
                }

                errorMessage = "Nexus request failed: " + ex.Message;
                return null;
            }
            catch (Exception ex)
            {
                if (!requestSubmitted)
                    _rateLimits.Release(rateLimitLease);
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
            NexusRemoteMod mod = GetV3ModByDomainAndId(draft.GameDomain, draft.NexusModId, out modError);
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
            if (!string.IsNullOrEmpty(draft.ExistingModFileId))
            {
                publishResponse = _v3Client.Post("/mod-files/" + Escape(draft.ExistingModFileId) + "/versions", request);
            }
            else
            {
                request["mod_id"] = mod.Uid;
                publishResponse = _v3Client.Post("/mod-files", request);
            }

            if (!string.IsNullOrEmpty(publishResponse.ErrorMessage))
            {
                errorMessage = publishResponse.ErrorMessage;
                return null;
            }

            Dictionary<string, object> publishedFile = AsDictionary(publishResponse.Data, "file") ?? publishResponse.Data;
            string fileId = ReadString(publishedFile, "id");
            string scopedFileId = ReadString(publishedFile, "game_scoped_id");
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

            NexusV3RestResult create = _v3Client.Post("/uploads", request);
            if (!string.IsNullOrEmpty(create.ErrorMessage))
            {
                errorMessage = create.ErrorMessage;
                return null;
            }

            string uploadId = ReadString(create.Data, "id");
            string url = ReadString(create.Data, "presigned_url");
            NexusV3UploadResult upload = _v3Client.PutFile(url, filePath);
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

            NexusV3RestResult create = _v3Client.Post("/uploads/multipart", request);
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

                    NexusV3UploadResult upload = _v3Client.PutBytes(url, bytes);
                    if (!upload.Success)
                    {
                        errorMessage = upload.ErrorMessage ?? ("Multipart upload failed at part " + (i + 1) + ".");
                        return null;
                    }
                    etags.Add(upload.ETag ?? string.Empty);
                }
            }

            NexusV3UploadResult complete = _v3Client.PostXml(completeUrl, BuildMultipartCompleteXml(etags));
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

            NexusV3RestResult finalise = _v3Client.Post("/uploads/" + Escape(uploadId) + "/finalise", new Dictionary<string, object>());
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
                NexusV3RestResult upload = _v3Client.Get("/uploads/" + Escape(uploadId));
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
            if (!string.IsNullOrEmpty(draft.ExistingModFileId))
                request["archive_existing_file"] = draft.ArchiveExistingFile;
            return request;
        }

        private bool HasApiKey
        {
            get { return !string.IsNullOrEmpty(_apiKey); }
        }

        private static Dictionary<string, object> BuildModsFilter(params KeyValuePair<string, object>[] equalsFilters)
        {
            var filter = new Dictionary<string, object>();
            filter["op"] = "AND";
            for (int i = 0; equalsFilters != null && i < equalsFilters.Length; i++)
            {
                filter[equalsFilters[i].Key] = new object[] { BuildEqualsFilter(equalsFilters[i].Value) };
            }
            return filter;
        }

        private static Dictionary<string, object> BuildEqualsFilter(object value)
        {
            var filter = new Dictionary<string, object>();
            filter["value"] = value;
            filter["op"] = "EQUALS";
            return filter;
        }

        private static object[] BuildDescendingSort(string fieldName)
        {
            var direction = new Dictionary<string, object>();
            direction["direction"] = "DESC";

            var sortEntry = new Dictionary<string, object>();
            sortEntry[fieldName] = direction;
            return new object[] { sortEntry };
        }

        private Dictionary<string, NexusRemoteMod> QueryV2ModsByLegacyDomainIds(List<NexusModReference> references, out string errorMessage)
        {
            errorMessage = null;
            var results = new Dictionary<string, NexusRemoteMod>(StringComparer.OrdinalIgnoreCase);
            if (references == null || references.Count == 0)
                return results;

            const string query = @"
query legacyModsByDomain($ids: [CompositeDomainWithIdInput!]!, $count: Int){
  legacyModsByDomain(ids: $ids, count: $count){
    nodes{" + V2ModFields + @"
    }
  }
}";

            var ids = new List<Dictionary<string, object>>();
            foreach (var reference in references)
            {
                if (reference == null || !reference.IsValid)
                    continue;

                var entry = new Dictionary<string, object>();
                entry["gameDomain"] = reference.GameDomain;
                entry["modId"] = reference.ModId;
                ids.Add(entry);
            }

            var variables = new Dictionary<string, object>();
            variables["ids"] = ids.ToArray();
            variables["count"] = ids.Count;

            NexusGraphQlResponse response = _v2Client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return results;
            }

            var page = AsDictionary(response.Data, "legacyModsByDomain");
            object[] nodes = AsArray(page, "nodes");
            if (nodes == null)
                return results;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                NexusRemoteMod remote = ParseV2RemoteMod(node, null);
                if (remote == null || remote.ModId <= 0 || string.IsNullOrEmpty(remote.GameDomain))
                    continue;

                results[remote.GameDomain + ":" + remote.ModId.ToString()] = remote;
            }

            return results;
        }

        private List<NexusRemoteMod> QueryV2LatestMods(string gameDomain, int count, out string errorMessage)
        {
            errorMessage = null;
            var list = new List<NexusRemoteMod>();
            if (string.IsNullOrEmpty(gameDomain))
            {
                errorMessage = "Nexus game domain is not configured.";
                return list;
            }

            if (count <= 0) count = 20;
            if (count > 100) count = 100;

            const string query = @"
query latestMods($filter: ModsFilter, $sort: [ModsSort!], $count: Int){
  mods(filter: $filter, sort: $sort, count: $count){
    nodes{" + V2ModFields + @"
    }
  }
}";

            var variables = new Dictionary<string, object>();
            variables["filter"] = BuildModsFilter(new KeyValuePair<string, object>("gameDomainName", gameDomain));
            variables["sort"] = BuildDescendingSort("createdAt");
            variables["count"] = count;

            NexusGraphQlResponse response = _v2Client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return list;
            }

            var page = AsDictionary(response.Data, "mods");
            object[] nodes = AsArray(page, "nodes");
            if (nodes == null)
                return list;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                NexusRemoteMod parsed = ParseV2RemoteMod(node, gameDomain);
                if (parsed != null)
                    list.Add(parsed);
            }

            return list;
        }

        private List<NexusRemoteMod> QueryV2ModsByName(string gameDomain, string modName, int count, out string errorMessage)
        {
            errorMessage = null;
            var list = new List<NexusRemoteMod>();
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(modName))
            {
                errorMessage = "Game domain and mod name are required for Nexus name search.";
                return list;
            }

            if (count <= 0) count = 10;
            if (count > 25) count = 25;

            const string query = @"
query findModsByName($filter: ModsFilter, $sort: [ModsSort!], $count: Int){
  mods(filter: $filter, sort: $sort, count: $count){
    nodes{" + V2ModFields + @"
    }
  }
}";

            var variables = new Dictionary<string, object>();
            variables["filter"] = BuildModsFilter(
                new KeyValuePair<string, object>("gameDomainName", gameDomain),
                new KeyValuePair<string, object>("name", modName));
            variables["sort"] = BuildDescendingSort("updatedAt");
            variables["count"] = count;

            NexusGraphQlResponse response = _v2Client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return list;
            }

            var page = AsDictionary(response.Data, "mods");
            object[] nodes = AsArray(page, "nodes");
            if (nodes == null)
                return list;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                NexusRemoteMod parsed = ParseV2RemoteMod(node, gameDomain);
                if (parsed != null)
                    list.Add(parsed);
            }

            return list;
        }

        private List<NexusRemoteModFile> QueryV2ModFiles(int gameId, int modId, out string errorMessage)
        {
            errorMessage = null;
            var files = new List<NexusRemoteModFile>();
            if (gameId <= 0 || modId <= 0)
            {
                errorMessage = "Invalid game/mod ID for files query.";
                return files;
            }

            const string query = @"
query modFiles($modId: ID!, $gameId: ID!){
  modFiles(modId: $modId, gameId: $gameId){
    fileId
    name
    version
    date
    category
    primary
    manager
    uri
    description
    changelogText
  }
}";

            var variables = new Dictionary<string, object>();
            variables["modId"] = modId;
            variables["gameId"] = gameId;

            NexusGraphQlResponse response = _v2Client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return files;
            }

            object[] nodes = AsArray(response.Data, "modFiles");
            if (nodes == null)
                return files;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                if (node == null)
                    continue;

                var file = new NexusRemoteModFile();
                file.FileId = ReadFirstIntLike(node, "fileId", "file_id");
                file.Name = ReadString(node, "name");
                file.Version = ReadString(node, "version");
                file.Description = ReadString(node, "description");
                // GraphQL v2 exposes changelogText and no md5 field (live-introspected
                // 2026-07-19); md5 arrives only via the REST fallback path.
                file.Changelog = ReadFirstString(node, "changelogText", "changelog");
                file.Md5 = ReadFirstString(node, "md5", "MD5");
                file.UnixDate = ReadInt(node, "date");
                file.Category = ReadString(node, "category");
                file.Primary = ReadFirstIntLike(node, "primary", "is_primary");
                file.Manager = ReadFirstIntLike(node, "manager", "allow_mod_manager_download", "mod_manager_download");
                file.Uri = ReadString(node, "uri");
                if (file.FileId > 0)
                    files.Add(file);
            }

            return files;
        }

        private NexusRemoteMod GetV3ModByDomainAndId(string gameDomain, int modId, out string errorMessage)
        {
            errorMessage = null;
            NexusV3RestResult response = _v3Client.Get("/games/" + Escape(gameDomain) + "/mods/" + modId);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return null;
            }

            NexusRemoteMod mod = ParseV3Mod(response.Data, gameDomain);
            if (mod == null)
            {
                errorMessage = "Nexus v3 did not return mod details.";
                return null;
            }

            string fileStateWarning;
            PopulateLatestFileState(mod, out fileStateWarning);
            if (!string.IsNullOrEmpty(fileStateWarning))
                System.Diagnostics.Debug.WriteLine("Nexus file state warning: " + fileStateWarning);

            return mod;
        }

        private void PopulateLatestFileState(NexusRemoteMod mod, out string warningMessage)
        {
            warningMessage = null;
            if (mod == null || string.IsNullOrEmpty(mod.Uid))
                return;

            string modFilesError;
            List<NexusV3ModFileRecord> modFiles = GetV3ModFiles(mod.Uid, out modFilesError);
            if (!string.IsNullOrEmpty(modFilesError))
            {
                warningMessage = modFilesError;
                return;
            }

            NexusRemoteModFile latest = null;
            for (int i = 0; i < modFiles.Count; i++)
            {
                string versionError;
                List<NexusRemoteModFile> files = GetV3ModFileVersions(modFiles[i].Id, out versionError);
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
                if (!IsInstallCandidate(file))
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

        private static bool IsInstallCandidate(NexusRemoteModFile file)
        {
            if (file == null || file.FileId <= 0)
                return false;

            string category = (file.Category ?? string.Empty).Trim();
            return !string.Equals(category, "archived", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(category, "deleted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(category, "old_version", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(category, "old version", StringComparison.OrdinalIgnoreCase);
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

        private static NexusRemoteMod ParseV2RemoteMod(Dictionary<string, object> node, string fallbackDomain)
        {
            if (node == null)
                return null;

            var mod = new NexusRemoteMod();
            mod.ModId = ReadInt(node, "modId");
            mod.Uid = ReadString(node, "uid");
            mod.Name = ReadString(node, "name");
            mod.Author = ReadString(node, "author");
            mod.Version = ReadString(node, "version");
            mod.Summary = ReadString(node, "summary");
            mod.PictureUrl = ReadString(node, "pictureUrl");
            mod.ThumbnailUrl = ReadString(node, "thumbnailUrl");
            mod.Downloads = ReadInt(node, "downloads");
            mod.Endorsements = ReadInt(node, "endorsements");
            mod.CreatedAtUtc = ReadDateTime(node, "createdAt");
            mod.UpdatedAtUtc = ReadDateTime(node, "updatedAt");

            var uploader = AsDictionary(node, "uploader");
            mod.UploaderId = ReadInt(uploader, "id");
            mod.UploaderName = ReadString(uploader, "name");

            var game = AsDictionary(node, "game");
            mod.GameId = ReadInt(game, "id");
            mod.GameDomain = ReadString(game, "domainName");
            if (string.IsNullOrEmpty(mod.GameDomain))
                mod.GameDomain = fallbackDomain;

            mod.GameDomain = NormalizeDomain(mod.GameDomain);
            return mod.ModId > 0 && !string.IsNullOrEmpty(mod.GameDomain) ? mod : null;
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

        private static NexusRemoteModFile ParseMinimalFile(Dictionary<string, object> node, string modFileId)
        {
            if (node == null)
                return null;

            var file = new NexusRemoteModFile();
            file.Id = ReadString(node, "id");
            file.ModFileId = modFileId;
            file.FileId = ReadInt(node, "game_scoped_id");
            file.Name = ReadString(node, "name");
            file.Version = ReadString(node, "version");
            file.Description = ReadString(node, "description");
            file.Changelog = ReadString(node, "changelog");
            file.Md5 = ReadFirstString(node, "md5", "MD5");
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
            file.Description = ReadFirstString(node, "description", "desc");
            file.Changelog = ReadFirstString(node, "changelog", "change_log");
            file.Md5 = ReadFirstString(node, "md5", "MD5");
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

        private int GetCacheGeneration()
        {
            lock (_cacheLock)
            {
                return _cacheGeneration;
            }
        }

        private void StoreCached<T>(
            Dictionary<string, CacheEntry<T>> cache,
            string key,
            T value,
            int generation)
        {
            if (cache == null || string.IsNullOrEmpty(key))
                return;

            lock (_cacheLock)
            {
                if (generation != _cacheGeneration)
                    return;
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
