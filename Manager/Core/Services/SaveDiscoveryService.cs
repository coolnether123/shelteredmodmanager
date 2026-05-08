using System.Collections.Generic;
using Manager.Core.Games.Profiles;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Legacy Sheltered save discovery facade kept for older call sites.
    /// New game integrations should use a profile ISaveDiscoveryStrategy.
    /// </summary>
    public class SaveDiscoveryService
    {
        public List<SaveSlotInfo> DiscoverSaves(string gamePath)
        {
            var profile = ShelteredGameProfileFactory.Create();
            return profile.SaveDiscovery.DiscoverSaves(profile, gamePath);
        }
    }
}
