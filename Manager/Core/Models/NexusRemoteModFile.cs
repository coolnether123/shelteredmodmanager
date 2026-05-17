using System;

namespace Manager.Core.Models
{
    /// <summary>
    /// File entry for a Nexus mod.
    /// </summary>
    public class NexusRemoteModFile
    {
        private DateTime? _uploadedAtUtc;

        public string Id { get; set; }
        public string UpdateGroupId { get; set; }
        public int FileId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public int UnixDate { get; set; }
        public string Category { get; set; }
        public int Primary { get; set; }
        public int Manager { get; set; }
        public string Uri { get; set; }

        public DateTime? UploadedAtUtc
        {
            get
            {
                if (_uploadedAtUtc.HasValue)
                    return _uploadedAtUtc;

                if (UnixDate <= 0) return null;
                try
                {
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return epoch.AddSeconds(UnixDate);
                }
                catch
                {
                    return null;
                }
            }
            set { _uploadedAtUtc = value; }
        }
    }
}
