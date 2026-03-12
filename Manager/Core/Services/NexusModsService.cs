using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Nexus-facing operations for installed mod checks and browse feeds.
    /// </summary>
    public class NexusModsService
    {
        private readonly NexusGraphQlClient _client;
        private readonly string _apiKey;

        public NexusModsService(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _client = new NexusGraphQlClient(apiKey);
        }

        public Dictionary<string, NexusRemoteMod> GetModsByReferences(IEnumerable<NexusModReference> references, out string errorMessage)
        {
            errorMessage = null;
            var results = new Dictionary<string, NexusRemoteMod>(StringComparer.OrdinalIgnoreCase);
            var distinct = GetDistinctReferences(references);
            if (distinct.Count == 0)
                return results;

            const int chunkSize = 40;
            for (int i = 0; i < distinct.Count; i += chunkSize)
            {
                int size = Math.Min(chunkSize, distinct.Count - i);
                var chunk = distinct.GetRange(i, size);
                string chunkError;
                var chunkResult = QueryModsByLegacyDomainIds(chunk, out chunkError);
                if (!string.IsNullOrEmpty(chunkError))
                {
                    errorMessage = chunkError;
                    break;
                }

                foreach (var kvp in chunkResult)
                {
                    results[kvp.Key] = kvp.Value;
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

            if (count <= 0) count = 20;
            if (count > 100) count = 100;

            const string query = @"
query latestMods($filter: ModsFilter, $sort: [ModsSort!], $count: Int){
  mods(filter: $filter, sort: $sort, count: $count){
    nodes{
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
      game { id domainName }
    }
  }
}";

            var gameDomainFilter = new Dictionary<string, object>();
            gameDomainFilter["value"] = gameDomain;
            gameDomainFilter["op"] = "EQUALS";

            var filter = new Dictionary<string, object>();
            filter["op"] = "AND";
            filter["gameDomainName"] = new object[] { gameDomainFilter };

            var createdSort = new Dictionary<string, object>();
            createdSort["direction"] = "DESC";

            var sortEntry = new Dictionary<string, object>();
            sortEntry["createdAt"] = createdSort;

            var variables = new Dictionary<string, object>();
            variables["filter"] = filter;
            variables["sort"] = new object[] { sortEntry };
            variables["count"] = count;

            var response = _client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return list;
            }

            var page = AsDictionary(response.Data, "mods");
            var nodes = AsArray(page, "nodes");
            if (nodes == null) return list;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                if (node == null) continue;
                var parsed = ParseRemoteMod(node, gameDomain);
                if (parsed != null)
                    list.Add(parsed);
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

            var refs = new List<NexusModReference>();
            refs.Add(new NexusModReference { GameDomain = gameDomain, ModId = modId });
            string error;
            var found = GetModsByReferences(refs, out error);
            if (!string.IsNullOrEmpty(error))
            {
                errorMessage = error;
                return null;
            }

            var key = gameDomain.Trim().ToLowerInvariant() + ":" + modId;
            NexusRemoteMod mod;
            if (found.TryGetValue(key, out mod))
                return mod;

            return null;
        }

        public List<NexusRemoteMod> FindModsByName(string gameDomain, string modName, int count, out string errorMessage)
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
    nodes{
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
      game { id domainName }
    }
  }
}";

            var gameDomainFilter = new Dictionary<string, object>();
            gameDomainFilter["value"] = gameDomain;
            gameDomainFilter["op"] = "EQUALS";

            var nameFilter = new Dictionary<string, object>();
            nameFilter["value"] = modName;
            nameFilter["op"] = "EQUALS";

            var filter = new Dictionary<string, object>();
            filter["op"] = "AND";
            filter["gameDomainName"] = new object[] { gameDomainFilter };
            filter["name"] = new object[] { nameFilter };

            var updatedSort = new Dictionary<string, object>();
            updatedSort["direction"] = "DESC";
            var sortEntry = new Dictionary<string, object>();
            sortEntry["updatedAt"] = updatedSort;

            var variables = new Dictionary<string, object>();
            variables["filter"] = filter;
            variables["sort"] = new object[] { sortEntry };
            variables["count"] = count;

            var response = _client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return list;
            }

            var page = AsDictionary(response.Data, "mods");
            var nodes = AsArray(page, "nodes");
            if (nodes == null) return list;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                if (node == null) continue;
                var parsed = ParseRemoteMod(node, gameDomain);
                if (parsed != null)
                    list.Add(parsed);
            }

            return list;
        }

        public List<NexusRemoteModFile> GetModFiles(int gameId, int modId, out string errorMessage)
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
  }
}";

            var variables = new Dictionary<string, object>();
            variables["modId"] = modId;
            variables["gameId"] = gameId;

            var response = _client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return files;
            }

            var nodes = AsArray(response.Data, "modFiles");
            if (nodes == null)
                return files;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                if (node == null) continue;

                var file = new NexusRemoteModFile();
                file.FileId = ReadInt(node, "fileId");
                file.Name = ReadString(node, "name");
                file.Version = ReadString(node, "version");
                file.UnixDate = ReadInt(node, "date");
                file.Category = ReadString(node, "category");
                file.Primary = ReadInt(node, "primary");
                file.Manager = ReadInt(node, "manager");
                file.Uri = ReadString(node, "uri");
                if (file.FileId > 0)
                    files.Add(file);
            }

            return files;
        }

        public NexusRemoteModFile GetPreferredInstallFile(int gameId, int modId, out string errorMessage)
        {
            errorMessage = null;
            var files = GetModFiles(gameId, modId, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage) || files.Count == 0)
                return null;

            NexusRemoteModFile best = null;
            foreach (var file in files)
            {
                if (best == null)
                {
                    best = file;
                    continue;
                }

                bool fileIsMain = string.Equals(file.Category, "MAIN", StringComparison.OrdinalIgnoreCase);
                bool bestIsMain = string.Equals(best.Category, "MAIN", StringComparison.OrdinalIgnoreCase);

                if (fileIsMain && !bestIsMain)
                {
                    best = file;
                    continue;
                }

                if (fileIsMain == bestIsMain)
                {
                    if (file.Primary > best.Primary)
                    {
                        best = file;
                        continue;
                    }

                    if (file.UnixDate > best.UnixDate)
                    {
                        best = file;
                    }
                }
            }

            return best;
        }

        public string GetV1DownloadUrl(string gameDomain, int modId, int fileId, string apiKey, out string errorMessage)
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

            string url = "https://api.nexusmods.com/v1/games/" + gameDomain.Trim().ToLowerInvariant() +
                         "/mods/" + modId + "/files/" + fileId + "/download_link.json";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Accept = "application/json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers["apikey"] = apiKey;
                request.Headers["application-name"] = "Sheltered Mod Manager";
                request.Headers["application-version"] = "1.3.0";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var parsed = new JavaScriptSerializer().DeserializeObject(json) as object[];
                    if (parsed == null || parsed.Length == 0)
                    {
                        errorMessage = "Nexus did not return a downloadable mirror.";
                        return null;
                    }

                    foreach (var item in parsed)
                    {
                        var obj = item as Dictionary<string, object>;
                        if (obj == null) continue;
                        string uri = ReadString(obj, "URI");
                        if (!string.IsNullOrEmpty(uri))
                            return uri;
                    }

                    errorMessage = "Nexus response did not include a usable download URI.";
                    return null;
                }
            }
            catch (WebException ex)
            {
                try
                {
                    using (var response = ex.Response as HttpWebResponse)
                    {
                        if (response != null)
                        {
                            if (response.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                errorMessage = "Unauthorized download request. Check Nexus API key.";
                                return null;
                            }
                            if (response.StatusCode == HttpStatusCode.Forbidden)
                            {
                                errorMessage = "Nexus denied direct download for this account/file.";
                                return null;
                            }
                        }
                    }
                }
                catch { }

                errorMessage = "Failed to get Nexus download URL: " + ex.Message;
                return null;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to get Nexus download URL: " + ex.Message;
                return null;
            }
        }

        public NexusAccountStatus GetAccountStatus(out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(_apiKey))
                return NexusAccountStatus.CreateNotConfigured();

            const string viewerQuery = @"
query nexusViewer {
  personalApiKey {
    userId
  }
  preferences {
    download
    dlLocation
  }
}";

            var response = _client.Execute(viewerQuery, null);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return NexusAccountStatus.CreateUnavailable(response.ErrorMessage);
            }

            var status = new NexusAccountStatus();
            status.IsConfigured = true;

            var keyInfo = AsDictionary(response.Data, "personalApiKey");
            status.UserId = ReadInt(keyInfo, "userId");
            status.IsConnected = status.UserId > 0;

            var preferences = AsDictionary(response.Data, "preferences");
            status.DownloadPreference = ReadString(preferences, "download");
            status.DownloadLocation = ReadString(preferences, "dlLocation");

            if (status.UserId <= 0)
            {
                status.IsConnected = false;
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Unknown;
                status.Summary = "The stored Nexus API key did not return a valid account.";
                status.DirectDownloadSummary = "Direct-download capability could not be determined.";
                return status;
            }

            const string userQuery = @"
query nexusUser($id: Int!) {
  user(id: $id) {
    memberId
    name
    membershipRoles
  }
}";

            var userVariables = new Dictionary<string, object>();
            userVariables["id"] = status.UserId;

            var userResponse = _client.Execute(userQuery, userVariables);
            if (!string.IsNullOrEmpty(userResponse.ErrorMessage))
            {
                errorMessage = userResponse.ErrorMessage;
                status.Summary = "Connected to Nexus, but account details could not be loaded.";
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Unknown;
                status.DirectDownloadSummary = "Direct downloads may still work, but SMM could not determine the account tier.";
                status.ErrorMessage = userResponse.ErrorMessage;
                return status;
            }

            var user = AsDictionary(userResponse.Data, "user");
            status.UserId = ReadInt(user, "memberId") > 0 ? ReadInt(user, "memberId") : status.UserId;
            status.UserName = ReadString(user, "name");
            status.MembershipRoles = ReadStringArray(user, "membershipRoles");

            PopulateAccountSummaries(status);
            return status;
        }

        private Dictionary<string, NexusRemoteMod> QueryModsByLegacyDomainIds(List<NexusModReference> references, out string errorMessage)
        {
            errorMessage = null;
            var results = new Dictionary<string, NexusRemoteMod>(StringComparer.OrdinalIgnoreCase);

            const string query = @"
query legacyModsByDomain($ids: [CompositeDomainWithIdInput!]!, $count: Int){
  legacyModsByDomain(ids: $ids, count: $count){
    nodes{
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
      game { id domainName }
    }
  }
}";

            var ids = new List<Dictionary<string, object>>();
            foreach (var reference in references)
            {
                var entry = new Dictionary<string, object>();
                entry["gameDomain"] = reference.GameDomain;
                entry["modId"] = reference.ModId;
                ids.Add(entry);
            }

            var variables = new Dictionary<string, object>();
            variables["ids"] = ids.ToArray();
            variables["count"] = references.Count;

            var response = _client.Execute(query, variables);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                errorMessage = response.ErrorMessage;
                return results;
            }

            var page = AsDictionary(response.Data, "legacyModsByDomain");
            var nodes = AsArray(page, "nodes");
            if (nodes == null)
                return results;

            foreach (var raw in nodes)
            {
                var node = raw as Dictionary<string, object>;
                if (node == null) continue;
                var remote = ParseRemoteMod(node, null);
                if (remote == null || remote.ModId <= 0 || string.IsNullOrEmpty(remote.GameDomain))
                    continue;

                results[remote.GameDomain + ":" + remote.ModId] = remote;
            }

            return results;
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

                var key = reference.Key;
                if (seen.Contains(key))
                    continue;

                seen.Add(key);
                list.Add(reference);
            }

            return list;
        }

        private static void PopulateAccountSummaries(NexusAccountStatus status)
        {
            if (status == null)
                return;

            string name = !string.IsNullOrEmpty(status.UserName) ? status.UserName : ("User #" + status.UserId);
            string membership = status.GetMembershipLabel();

            status.Summary = "Connected as " + name + " (" + membership + ").";

            if (status.HasRole("premium") || status.HasRole("supporter"))
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Available;
                status.DirectDownloadSummary =
                    "This account appears to have elevated Nexus membership. Direct downloads should usually work, but Nexus can still deny specific files or unapproved apps.";
                return;
            }

            if (status.HasRole("member"))
            {
                status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Limited;
                status.DirectDownloadSummary =
                    "This account appears to be a regular member account. Browsing and update checks work, but Nexus may deny direct downloads for some files or apps.";
                return;
            }

            status.DirectDownloadAvailability = NexusDirectDownloadAvailability.Unknown;
            status.DirectDownloadSummary =
                "SMM could not map the Nexus membership role to a known download policy. Direct downloads may still depend on Nexus account and app approval.";
        }

        private static NexusRemoteMod ParseRemoteMod(Dictionary<string, object> node, string fallbackDomain)
        {
            if (node == null)
                return null;

            var mod = new NexusRemoteMod();
            mod.ModId = ReadInt(node, "modId");
            mod.Uid = ReadString(node, "uid");
            mod.Name = ReadString(node, "name");
            mod.Author = ReadString(node, "author");
            var uploader = AsDictionary(node, "uploader");
            mod.UploaderName = ReadString(uploader, "name");
            mod.Version = ReadString(node, "version");
            mod.Summary = ReadString(node, "summary");
            mod.PictureUrl = ReadString(node, "pictureUrl");
            mod.ThumbnailUrl = ReadString(node, "thumbnailUrl");
            mod.Downloads = ReadInt(node, "downloads");
            mod.Endorsements = ReadInt(node, "endorsements");
            mod.CreatedAtUtc = ReadDateTime(node, "createdAt");
            mod.UpdatedAtUtc = ReadDateTime(node, "updatedAt");

            var game = AsDictionary(node, "game");
            mod.GameId = ReadInt(game, "id");
            mod.GameDomain = ReadString(game, "domainName");
            if (string.IsNullOrEmpty(mod.GameDomain))
                mod.GameDomain = fallbackDomain;

            if (!string.IsNullOrEmpty(mod.GameDomain))
                mod.GameDomain = mod.GameDomain.Trim().ToLowerInvariant();

            return mod;
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
            if (dict == null || string.IsNullOrEmpty(key))
                return 0;

            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return 0;

            if (value is int) return (int)value;
            if (value is long) return (int)(long)value;
            if (value is double) return (int)(double)value;
            if (value is decimal) return (int)(decimal)value;

            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : 0;
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

        private static string[] ReadStringArray(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key))
                return new string[0];

            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return new string[0];

            var raw = value as object[];
            if (raw == null || raw.Length == 0)
                return new string[0];

            var list = new List<string>();
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == null)
                    continue;

                string text = Convert.ToString(raw[i]);
                if (!string.IsNullOrEmpty(text))
                    list.Add(text);
            }

            return list.ToArray();
        }

    }
}
