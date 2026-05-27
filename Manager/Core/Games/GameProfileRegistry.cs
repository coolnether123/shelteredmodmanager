using System;
using System.Collections.Generic;
using Manager.Core.Games.Models;
using Manager.Core.Games.Profiles;

namespace Manager.Core.Games
{
    public sealed class GameProfileRegistry
    {
        private readonly Dictionary<string, GameProfile> _profiles;

        public GameProfileRegistry()
        {
            _profiles = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
        }

        public static GameProfileRegistry CreateDefault()
        {
            GameProfileRegistry registry = new GameProfileRegistry();
            registry.Register(ParalivesGameProfileFactory.Create());
            registry.Register(ShelteredGameProfileFactory.Create());
            registry.Register(GenericUnityGameProfileFactory.Create());
            return registry;
        }

        public void Register(GameProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Id))
                return;

            _profiles[profile.Id] = profile;
        }

        public GameProfile Resolve(string profileId)
        {
            GameProfile profile;
            if (!string.IsNullOrEmpty(profileId) && _profiles.TryGetValue(profileId, out profile))
                return profile;

            if (_profiles.TryGetValue(ParalivesGameProfileFactory.ProfileId, out profile))
                return profile;

            foreach (GameProfile registered in _profiles.Values)
                return registered;

            return ParalivesGameProfileFactory.Create();
        }

        public IList<GameProfile> GetAll()
        {
            List<GameProfile> profiles = new List<GameProfile>(_profiles.Values);
            profiles.Sort(delegate(GameProfile left, GameProfile right)
            {
                return string.Compare(left != null ? left.DisplayName : string.Empty,
                    right != null ? right.DisplayName : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });
            return profiles;
        }
    }
}
