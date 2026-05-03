using System;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    internal enum PageTurnArrowDirection
    {
        Previous,
        Next
    }

    /// <summary>
    /// Finds reusable vanilla clipboard/journal assets at runtime without requiring
    /// new bundled resources.
    /// </summary>
    internal sealed class VanillaPageTurnAssets
    {
        private AudioClip _cachedPageSound;
        private bool _searchedPageSound;
        private GameObject _cachedPreviousArrow;
        private GameObject _cachedNextArrow;
        private bool _searchedArrows;
        private GameObject _cachedFlipAnimation;
        private bool _searchedFlipAnimation;

        public AudioClip FindPageTurnSound()
        {
            if (_searchedPageSound)
                return _cachedPageSound;

            _searchedPageSound = true;
            _cachedPageSound = FindVanillaPageSound();
            return _cachedPageSound;
        }

        public GameObject FindArrowTemplate(PageTurnArrowDirection direction)
        {
            if (!_searchedArrows)
            {
                _searchedArrows = true;
                FindClipboardArrowTemplates(out _cachedPreviousArrow, out _cachedNextArrow);
            }

            return direction == PageTurnArrowDirection.Previous ? _cachedPreviousArrow : _cachedNextArrow;
        }

        public GameObject FindFlipAnimationTemplate()
        {
            if (_searchedFlipAnimation)
                return _cachedFlipAnimation;

            _searchedFlipAnimation = true;
            _cachedFlipAnimation = FindNamedSpriteAnimationTemplate();
            return _cachedFlipAnimation;
        }

        private static AudioClip FindVanillaPageSound()
        {
            try
            {
                ClipboardPanel[] panels = Resources.FindObjectsOfTypeAll<ClipboardPanel>();
                if (panels != null)
                {
                    for (int i = 0; i < panels.Length; i++)
                    {
                        ClipboardPanel panel = panels[i];
                        if (panel != null && panel.nextPageSound != null)
                            return panel.nextPageSound;
                    }
                }

                JournalPanel[] journalPanels = Resources.FindObjectsOfTypeAll<JournalPanel>();
                if (journalPanels != null)
                {
                    for (int i = 0; i < journalPanels.Length; i++)
                    {
                        JournalPanel panel = journalPanels[i];
                        if (panel != null && panel.m_newTabSound != null)
                            return panel.m_newTabSound;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[FieldManual] Could not resolve vanilla page sound: " + ex.Message);
            }

            return null;
        }

        private static void FindClipboardArrowTemplates(out GameObject previous, out GameObject next)
        {
            previous = null;
            next = null;

            if (TryFindFamilyClipboardArrows(out previous, out next))
                return;
            if (TryFindCustomisationArrows(out previous, out next))
                return;
            TryFindBreachMemberArrows(out previous, out next);
        }

        private static bool TryFindFamilyClipboardArrows(out GameObject previous, out GameObject next)
        {
            previous = null;
            next = null;
            FamilyClipboard[] panels = Resources.FindObjectsOfTypeAll<FamilyClipboard>();
            if (panels == null)
                return false;

            for (int i = 0; i < panels.Length; i++)
            {
                FamilyClipboard panel = panels[i];
                if (panel == null)
                    continue;

                previous = panel.arrowLeft;
                next = panel.arrowRight;
                if (previous != null && next != null)
                    return true;
            }

            return false;
        }

        private static bool TryFindCustomisationArrows(out GameObject previous, out GameObject next)
        {
            previous = null;
            next = null;
            CustomisationPanel[] panels = Resources.FindObjectsOfTypeAll<CustomisationPanel>();
            if (panels == null)
                return false;

            FieldInfo leftField = typeof(CustomisationPanel).GetField("pageLeftArrow", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo rightField = typeof(CustomisationPanel).GetField("pageRightArrow", BindingFlags.Public | BindingFlags.Instance);
            if (leftField == null || rightField == null)
                return false;

            for (int i = 0; i < panels.Length; i++)
            {
                CustomisationPanel panel = panels[i];
                if (panel == null)
                    continue;

                previous = leftField.GetValue(panel) as GameObject;
                next = rightField.GetValue(panel) as GameObject;
                if (previous != null && next != null)
                    return true;
            }

            return false;
        }

        private static bool TryFindBreachMemberArrows(out GameObject previous, out GameObject next)
        {
            previous = null;
            next = null;
            BreachPartySetupPanel[] panels = Resources.FindObjectsOfTypeAll<BreachPartySetupPanel>();
            if (panels == null)
                return false;

            for (int i = 0; i < panels.Length; i++)
            {
                BreachPartySetupPanel panel = panels[i];
                if (panel == null)
                    continue;

                previous = panel.memberLeftArrow;
                next = panel.memberRightArrow;
                if (previous != null && next != null)
                    return true;
            }

            return false;
        }

        private static GameObject FindNamedSpriteAnimationTemplate()
        {
            GameObject fromEx = FindAnimationByName(Resources.FindObjectsOfTypeAll<UISpriteAnimationEx>());
            if (fromEx != null)
                return fromEx;

            return FindAnimationByName(Resources.FindObjectsOfTypeAll<UISpriteAnimation>());
        }

        private static GameObject FindAnimationByName(Component[] animations)
        {
            if (animations == null)
                return null;

            for (int i = 0; i < animations.Length; i++)
            {
                Component animation = animations[i];
                if (animation == null || animation.gameObject == null)
                    continue;

                string path = BuildPath(animation.transform).ToLowerInvariant();
                if (path.IndexOf("page") >= 0 || path.IndexOf("flip") >= 0
                    || path.IndexOf("clipboard") >= 0 || path.IndexOf("journal") >= 0
                    || path.IndexOf("book") >= 0)
                    return animation.gameObject;
            }

            return null;
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
