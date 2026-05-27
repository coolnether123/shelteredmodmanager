using System;

namespace Manager.Core.Models
{
    /// <summary>
    /// Minimal Nexus mod snapshot used by Manager update/browse workflows.
    /// </summary>
    public class NexusRemoteMod
    {
        public string GameDomain { get; set; }
        public int GameId { get; set; }
        public int ModId { get; set; }
        public string Uid { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string UploaderName { get; set; }
        public string Version { get; set; }
        public string Summary { get; set; }
        public string PictureUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int Downloads { get; set; }
        public int Endorsements { get; set; }

        public string GetPageUrl()
        {
            if (string.IsNullOrEmpty(GameDomain) || ModId <= 0)
                return string.Empty;

            return "https://www.nexusmods.com/" + GameDomain + "/mods/" + ModId;
        }
    }
}
