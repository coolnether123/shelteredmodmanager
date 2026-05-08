using System.Collections.Generic;
using Manager.Core.Games.Models;
using Manager.Core.Models;

namespace Manager.Core.Games.Saves
{
    public interface ISaveDiscoveryStrategy
    {
        List<SaveSlotInfo> DiscoverSaves(GameProfile profile, string gamePath);
    }
}
