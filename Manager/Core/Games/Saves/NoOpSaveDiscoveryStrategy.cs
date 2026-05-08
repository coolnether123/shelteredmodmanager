using System.Collections.Generic;
using Manager.Core.Games.Models;
using Manager.Core.Models;

namespace Manager.Core.Games.Saves
{
    public sealed class NoOpSaveDiscoveryStrategy : ISaveDiscoveryStrategy
    {
        public List<SaveSlotInfo> DiscoverSaves(GameProfile profile, string gamePath)
        {
            return new List<SaveSlotInfo>();
        }
    }
}
