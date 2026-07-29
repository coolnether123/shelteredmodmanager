using System;
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
            Extensions = new ParalivesUiExtensionFacade();
        }

        public ParalivesUiExtensionFacade Extensions { get; private set; }

        public IDisposable RegisterOccupationPanelProvider(IParalivesOccupationPanelProvider provider)
        {
            return Extensions.RegisterOccupationPanelProvider(provider);
        }

        public bool UnregisterOccupationPanelProvider(IParalivesOccupationPanelProvider provider)
        {
            return Extensions.UnregisterOccupationPanelProvider(provider);
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
            return OpenOccupationsCore(characterGuid, playerIndex, occupationIndex, false);
        }

        public bool OpenOccupations(ulong characterGuid)
        {
            return OpenOccupations(characterGuid, 0);
        }

        public bool OpenOccupations(ulong characterGuid, int playerIndex)
        {
            return OpenOccupations(characterGuid, playerIndex, -1);
        }

        public bool OpenOccupations(ulong characterGuid, int playerIndex, int occupationIndex)
        {
            return OpenOccupationsCore(characterGuid, playerIndex, occupationIndex, true);
        }

        public bool OpenSkills(ulong characterGuid)
        {
            return OpenSkills(characterGuid, 0);
        }

        public bool OpenSkills(ulong characterGuid, int playerIndex)
        {
            global::UISkills skills;
            return OpenCharacterTabCore(characterGuid, playerIndex, out skills);
        }

        public bool OpenCharacterTab(ulong characterGuid, ParalivesCharacterTab tab)
        {
            return OpenCharacterTab(characterGuid, tab, 0);
        }

        public bool OpenCharacterTab(ulong characterGuid, ParalivesCharacterTab tab, int playerIndex)
        {
            switch (tab)
            {
                case ParalivesCharacterTab.Thoughts:
                    global::UIThoughts thoughts;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out thoughts);
                case ParalivesCharacterTab.Profile:
                    global::UICharacterProfile profile;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out profile);
                case ParalivesCharacterTab.Skills:
                    return OpenSkills(characterGuid, playerIndex);
                case ParalivesCharacterTab.Social:
                    global::UIRelationships relationships;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out relationships);
                case ParalivesCharacterTab.Occupations:
                    return OpenOccupations(characterGuid, playerIndex);
                case ParalivesCharacterTab.Inventory:
                    global::UIInventory inventory;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out inventory);
                case ParalivesCharacterTab.Memories:
                    global::UIMemories memories;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out memories);
                case ParalivesCharacterTab.Goals:
                    global::UIGoals goals;
                    return OpenCharacterTabCore(characterGuid, playerIndex, out goals);
                default:
                    return false;
            }
        }

        private bool OpenOccupationsCore(ulong characterGuid, int playerIndex, int occupationIndex, bool showCharacterSubMenu)
        {
            if (!_characters.Select(characterGuid, playerIndex, true))
                return false;

            if (showCharacterSubMenu)
                ShowCharacterSubMenu(playerIndex);

            global::UIOccupations occupations;
            if (!Show<global::UIOccupations>(playerIndex, out occupations))
                return false;

            if (occupationIndex >= 0)
                occupations.SetSelectedOccupation(occupationIndex);

            return true;
        }

        private bool OpenCharacterTabCore<TWindow>(ulong characterGuid, int playerIndex, out TWindow window)
            where TWindow : global::UIWindow
        {
            window = null;
            if (!_characters.Select(characterGuid, playerIndex, true))
                return false;

            ShowCharacterSubMenu(playerIndex);
            return Show<TWindow>(playerIndex, out window);
        }

        private void ShowCharacterSubMenu(int playerIndex)
        {
            global::UICharacterSubMenuBar subMenu;
            Show<global::UICharacterSubMenuBar>(playerIndex, out subMenu);
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
