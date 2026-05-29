using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesPlayerFacade
    {
        internal ParalivesPlayerFacade()
        {
        }

        public int Count
        {
            get
            {
                return global::PlayerManager.Instance != null && global::PlayerManager.Instance.Players != null
                    ? global::PlayerManager.Instance.Players.Count
                    : 0;
            }
        }

        public bool TryGet(int playerIndex, out global::Player player)
        {
            player = null;
            if (global::PlayerManager.Instance == null || global::PlayerManager.Instance.Players == null)
                return false;
            if (playerIndex < 0 || playerIndex >= global::PlayerManager.Instance.Players.Count)
                return false;

            player = global::PlayerManager.Instance.Players[playerIndex];
            return player != null;
        }

        public global::Player GetOrNull(int playerIndex)
        {
            global::Player player;
            return TryGet(playerIndex, out player) ? player : null;
        }

        public ulong GetSelectedCharacterGuid(int playerIndex)
        {
            global::Player player;
            return TryGet(playerIndex, out player) ? player.GetSelectedCharacterGUID() : 0UL;
        }

        public ulong[] GetSelectedCharacterGuids(int playerIndex)
        {
            global::Player player;
            if (!TryGet(playerIndex, out player) || player.SelectedCharactersGUID == null)
                return new ulong[0];

            return player.SelectedCharactersGUID.ToArray();
        }

        public bool SetSelectedCharacterGuids(int playerIndex, IEnumerable<ulong> characterGuids)
        {
            global::Player player;
            if (!TryGet(playerIndex, out player))
                return false;

            List<ulong> selected = new List<ulong>();
            if (characterGuids != null)
            {
                foreach (ulong guid in characterGuids)
                {
                    if (guid != 0UL && !selected.Contains(guid))
                        selected.Add(guid);
                }
            }

            player.SelectedCharactersGUID = selected;
            return true;
        }
    }
}
