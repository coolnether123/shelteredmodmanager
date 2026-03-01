using System;

namespace Manager.Core.Models
{
    /// <summary>
    /// Identifies a Nexus mod by game domain and legacy mod ID.
    /// </summary>
    public class NexusModReference
    {
        public string GameDomain { get; set; }
        public int ModId { get; set; }

        public string Key
        {
            get
            {
                return (GameDomain ?? string.Empty).Trim().ToLowerInvariant() + ":" + ModId;
            }
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(GameDomain) && ModId > 0; }
        }

        public override string ToString()
        {
            return Key;
        }
    }
}
