using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationTileContext
    {
        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong OccupationUnlockableGuid { get; internal set; }

        public bool IsRankUpgrade { get; internal set; }

        public bool IsUpgradeSelectionMode { get; internal set; }

        public bool IsGraduationMode { get; internal set; }

        public SchoolJobTypes OccupationType { get; internal set; }

        public int UnlockableLevel { get; internal set; }
    }

    public sealed class ParalivesUiFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        internal ParalivesUiFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public bool TryGet<TWindow>(int playerIndex, out TWindow window) where TWindow : global::UIWindow
        {
            return TryGet<TWindow>(playerIndex, false, out window);
        }

        public bool TryGet<TWindow>(int playerIndex, bool createIfMissing, out TWindow window) where TWindow : global::UIWindow
        {
            window = null;
            if (global::UI.Instance == null)
                return false;

            try
            {
                window = global::UI.GetOrNull<TWindow>(playerIndex, createIfMissing);
            }
            catch
            {
                window = null;
            }

            return window != null;
        }

        public bool Show<TWindow>(int playerIndex, out TWindow window) where TWindow : global::UIWindow
        {
            if (!TryGet<TWindow>(playerIndex, true, out window))
                return false;

            window.Show();
            return true;
        }

        public bool Hide<TWindow>(int playerIndex) where TWindow : global::UIWindow
        {
            TWindow window;
            if (!TryGet<TWindow>(playerIndex, false, out window))
                return false;

            window.Hide();
            return true;
        }

        public bool OpenOccupationsForCharacter(ulong characterGuid)
        {
            return OpenOccupationsForCharacter(characterGuid, 0, -1);
        }

        public bool OpenOccupationsForCharacter(ulong characterGuid, int playerIndex)
        {
            return OpenOccupationsForCharacter(characterGuid, playerIndex, -1);
        }

        public bool OpenOccupationsForCharacter(ulong characterGuid, int playerIndex, int occupationIndex)
        {
            if (!_characters.Select(characterGuid, playerIndex, true))
                return false;

            global::UIOccupations occupations;
            if (!Show<global::UIOccupations>(playerIndex, out occupations))
                return false;

            if (occupationIndex >= 0)
                occupations.SetSelectedOccupation(occupationIndex);

            return true;
        }

        public bool AnimateNewOccupationTask(ulong wantGuid)
        {
            return AnimateNewOccupationTask(wantGuid, 0);
        }

        public bool AnimateNewOccupationTask(ulong wantGuid, int playerIndex)
        {
            if (wantGuid == 0UL)
                return false;

            global::UIOccupations occupations;
            if (!TryGet<global::UIOccupations>(playerIndex, true, out occupations))
                return false;

            try
            {
                occupations.AnimateNewTask(wantGuid);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetOccupationTileContext(
            global::UIOccupationUnlockableItem item,
            out ParalivesOccupationTileContext context)
        {
            context = null;
            if (item == null)
                return false;

            try
            {
                context = new ParalivesOccupationTileContext
                {
                    CharacterGuid = item.Character == null ? 0UL : item.Character.GUID,
                    OccupationIndex = item.OccupationIndex,
                    OccupationGuid = item.OccupationData == null ? 0UL : item.OccupationData.Occupation,
                    OccupationUnlockableGuid = item.OccupationUnlockable == null ? 0UL : item.OccupationUnlockable.GUID,
                    IsRankUpgrade = item.IsRankUpgrade,
                    IsUpgradeSelectionMode = item.InUpgradeSelectionMode,
                    IsGraduationMode = item.InGraduationMode,
                    OccupationType = item.OccupationType,
                    UnlockableLevel = item.UnlockableData == null ? 0 : item.UnlockableData.Level
                };
                return true;
            }
            catch
            {
                context = null;
                return false;
            }
        }
    }
}
