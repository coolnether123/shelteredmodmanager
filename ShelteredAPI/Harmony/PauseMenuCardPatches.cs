using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    [PatchPolicy(PatchDomain.UI, "PauseMenuModApiCards",
        TargetBehavior = "Inject ModAPI keybind and mod-manager wallet cards into the in-game pause menu.",
        FailureMode = "The in-game pause menu only shows the vanilla settings and save/exit cards.",
        RollbackStrategy = "Disable the UI patch domain or remove the pause-menu card patch host.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
    [HarmonyPatch(typeof(MainMenuPanel), "OnShow")]
    internal static class PauseMenuCardPatches
    {
        public static void Postfix(MainMenuPanel __instance)
        {
            try
            {
                PauseMenuCardAugmentationService.Apply(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PauseMenuCardPatches] Failed to augment pause menu: " + ex);
            }
        }
    }

    internal static class PauseMenuCardAugmentationService
    {
        private const string KeybindsCardName = "ModAPI_PauseMenu_KeybindsCard";
        private const string ModsCardName = "ModAPI_PauseMenu_ModsCard";
        private const string InjectedWalletName = "ModAPI_PauseMenu_InjectedWallet";
        private const float FallbackCardSpacing = -90f;
        private const int VanillaWalletDepthOffset = 30;
        private const int InjectedWalletDepthOffset = 90;
        private static readonly Vector3 VanillaWalletBaseOffset = new Vector3(-120f, 0f, 0f);
        private static readonly Vector3 InjectedWalletBaseOffset = new Vector3(-120f, 0f, -20f);
        internal static readonly Vector3 CardLocalPositionCompensation = new Vector3(60f, 0f, 0f);

        private static readonly PauseMenuInjectedCardDefinition KeybindsCard = new PauseMenuInjectedCardDefinition(
            KeybindsCardName,
            "KEYBINDS",
            new Color(0.12f, 0.72f, 0.88f, 1f),
            new Color(0.36f, 0.92f, 1f, 1f),
            new[] { "keyboard", "keybind", "key", "controls", "control", "input" },
            PauseMenuCardActions.OpenKeybinds);

        private static readonly PauseMenuInjectedCardDefinition ModsCard = new PauseMenuInjectedCardDefinition(
            ModsCardName,
            "MOD SETTINGS",
            new Color(0.42f, 0.74f, 0.25f, 1f),
            new Color(0.66f, 0.95f, 0.40f, 1f),
            new[] { "mod", "mods", "settings", "option", "gear", "cog", "wrench" },
            PauseMenuCardActions.OpenModManager);

        public static void Apply(MainMenuPanel panel)
        {
            if (panel == null || panel.transform == null)
                return;

            PauseMenuHierarchy hierarchy;
            if (!PauseMenuHierarchyResolver.TryResolve(panel, out hierarchy))
                return;

            float step = ResolveCardStep(hierarchy.SettingsTab, hierarchy.SaveExitTab);
            RestoreVanillaTabLocalPositions(hierarchy);
            ClearLegacyInjectedCards(hierarchy.Background);

            Vector3 originalWalletPosition = PauseMenuOriginalPosition.Capture(hierarchy.Background.gameObject).OriginalLocalPosition;
            PauseMenuWalletLayout layout = PauseMenuWalletLayoutService.Resolve(
                hierarchy.Background.gameObject,
                originalWalletPosition,
                step,
                InjectedWalletBaseOffset,
                VanillaWalletBaseOffset);

            GameObject injectedWallet = PauseMenuWalletBuilder.EnsureInjectedWallet(
                hierarchy.Menu,
                hierarchy.Background.gameObject,
                InjectedWalletName);

            if (injectedWallet != null)
            {
                PauseMenuWalletDepthService.ApplyDepthOffset(injectedWallet, InjectedWalletDepthOffset);
                PauseMenuWalletPositioner.ApplyCoreLocalPosition(injectedWallet, layout.InjectedWalletPosition);
                PauseMenuWalletBuilder.ConfigureInjectedWallet(
                    injectedWallet,
                    hierarchy.SettingsTab,
                    hierarchy.SaveExitTab,
                    KeybindsCard,
                    ModsCard);
                PauseMenuWalletFaceDepthService.BringWalletFacesForward(
                    injectedWallet,
                    KeybindsCardName,
                    ModsCardName);
            }

            PauseMenuWalletDepthService.ApplyDepthOffset(hierarchy.Background.gameObject, VanillaWalletDepthOffset);
            PauseMenuWalletFaceDepthService.BringWalletFacesForward(
                hierarchy.Background.gameObject,
                "tab1",
                "tab2");
            PauseMenuWalletPositioner.ApplyCoreLocalPosition(
                hierarchy.Background.gameObject,
                layout.VanillaWalletPosition);

            PauseMenuWalletDebugService.LogStackPlan(
                hierarchy.Background.gameObject,
                originalWalletPosition,
                layout.InjectedWalletPosition,
                layout.VanillaWalletPosition,
                step,
                layout.WalletGap,
                layout.WalletVisibleHeight);
            PauseMenuWalletDebugService.LogLayout(
                hierarchy.Menu,
                injectedWallet,
                hierarchy.Background.gameObject,
                KeybindsCardName,
                ModsCardName);

            MMLog.WriteInfo("[PauseMenuCardPatches] Pause menu wallets injected or refreshed. model=2-wallets/2-cards-each gap=" + layout.WalletGap
                + " visibleHeight=" + layout.WalletVisibleHeight
                + " injected=" + FormatVector(layout.InjectedWalletPosition)
                + " vanilla=" + FormatVector(layout.VanillaWalletPosition) + ".");
        }

        private static float ResolveCardStep(Transform settingsTab, Transform saveExitTab)
        {
            if (settingsTab == null || saveExitTab == null)
                return FallbackCardSpacing;

            Vector3 settingsOriginal = PauseMenuOriginalPosition.Capture(settingsTab.gameObject).OriginalLocalPosition;
            Vector3 saveExitOriginal = PauseMenuOriginalPosition.Capture(saveExitTab.gameObject).OriginalLocalPosition;
            float step = saveExitOriginal.y - settingsOriginal.y;
            if (Mathf.Abs(step) >= 1f)
                return step;

            if (TryResolveChildCardStep(settingsTab, saveExitTab, out step))
                return step;

            return FallbackCardSpacing;
        }

        private static bool TryResolveChildCardStep(Transform settingsTab, Transform saveExitTab, out float step)
        {
            step = 0f;
            bool resolved = false;
            ResolveLargestChildStep(settingsTab, saveExitTab, "button", ref step, ref resolved);
            ResolveLargestChildStep(settingsTab, saveExitTab, "Icon", ref step, ref resolved);
            ResolveLargestChildStep(settingsTab, saveExitTab, "label", ref step, ref resolved);
            return resolved;
        }

        private static void ResolveLargestChildStep(
            Transform settingsTab,
            Transform saveExitTab,
            string childName,
            ref float step,
            ref bool resolved)
        {
            Transform settingsChild = FindChildIgnoreCase(settingsTab, childName);
            Transform saveExitChild = FindChildIgnoreCase(saveExitTab, childName);
            if (settingsChild == null || saveExitChild == null)
                return;

            float candidate = saveExitChild.localPosition.y - settingsChild.localPosition.y;
            if (Mathf.Abs(candidate) < 1f)
                return;

            if (!resolved || Mathf.Abs(candidate) > Mathf.Abs(step))
            {
                step = candidate;
                resolved = true;
            }
        }

        private static Transform FindChildIgnoreCase(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform nested = FindChildIgnoreCase(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.0") + "," + value.y.ToString("0.0") + "," + value.z.ToString("0.0") + ")";
        }

        private static void RestoreVanillaTabLocalPositions(PauseMenuHierarchy hierarchy)
        {
            if (hierarchy == null)
                return;

            if (hierarchy.SettingsTab != null)
                hierarchy.SettingsTab.localPosition = PauseMenuOriginalPosition.Capture(hierarchy.SettingsTab.gameObject).OriginalLocalPosition + CardLocalPositionCompensation;
            if (hierarchy.SaveExitTab != null)
                hierarchy.SaveExitTab.localPosition = PauseMenuOriginalPosition.Capture(hierarchy.SaveExitTab.gameObject).OriginalLocalPosition + CardLocalPositionCompensation;
        }

        private static void ClearLegacyInjectedCards(Transform vanillaWallet)
        {
            if (vanillaWallet == null)
                return;

            DestroyChildIfPresent(vanillaWallet, KeybindsCardName);
            DestroyChildIfPresent(vanillaWallet, ModsCardName);
        }

        private static void DestroyChildIfPresent(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
                return;

            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    internal sealed class PauseMenuHierarchy
    {
        public Transform Menu;
        public Transform Background;
        public Transform SettingsTab;
        public Transform SaveExitTab;
    }

    internal static class PauseMenuHierarchyResolver
    {
        private const string BackgroundPath = "menu_anchor/Menu/Background";

        public static bool TryResolve(MainMenuPanel panel, out PauseMenuHierarchy hierarchy)
        {
            hierarchy = null;
            if (panel == null || panel.transform == null)
                return false;

            Transform background = panel.transform.Find(BackgroundPath);
            if (background == null)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] Could not find pause menu Background at local path '" + BackgroundPath + "'.");
                return false;
            }

            Transform tab1 = background.Find("tab1");
            Transform tab2 = background.Find("tab2");
            if (tab1 == null || tab2 == null)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] Could not find vanilla pause menu tab1/tab2 under Background.");
                return false;
            }

            hierarchy = new PauseMenuHierarchy
            {
                Menu = background.parent,
                Background = background,
                SettingsTab = tab1,
                SaveExitTab = tab2
            };
            return true;
        }
    }

    internal sealed class PauseMenuInjectedCardDefinition
    {
        public readonly string ObjectName;
        public readonly string LabelText;
        public readonly Color CardTint;
        public readonly Color IconTint;
        public readonly string[] IconSpriteTokens;
        public readonly Action OnClick;

        public PauseMenuInjectedCardDefinition(
            string objectName,
            string labelText,
            Color cardTint,
            Color iconTint,
            string[] iconSpriteTokens,
            Action onClick)
        {
            ObjectName = objectName;
            LabelText = labelText;
            CardTint = cardTint;
            IconTint = iconTint;
            IconSpriteTokens = iconSpriteTokens;
            OnClick = onClick;
        }
    }

    internal sealed class PauseMenuWalletLayout
    {
        public readonly Vector3 InjectedWalletPosition;
        public readonly Vector3 VanillaWalletPosition;
        public readonly float WalletGap;
        public readonly float WalletVisibleHeight;

        public PauseMenuWalletLayout(
            Vector3 injectedWalletPosition,
            Vector3 vanillaWalletPosition,
            float walletGap,
            float walletVisibleHeight)
        {
            InjectedWalletPosition = injectedWalletPosition;
            VanillaWalletPosition = vanillaWalletPosition;
            WalletGap = walletGap;
            WalletVisibleHeight = walletVisibleHeight;
        }
    }

    internal static class PauseMenuWalletLayoutService
    {
        private const float FallbackWalletVisibleHeight = 552f;
        private const float MinimumWalletGapPadding = 12f;
        private const float MinimumWalletSeparation = 18f;

        public static PauseMenuWalletLayout Resolve(
            GameObject templateWallet,
            Vector3 originalWalletPosition,
            float cardStep,
            Vector3 injectedBaseOffset,
            Vector3 vanillaBaseOffset)
        {
            float visibleHeight = ResolveVisibleHeight(templateWallet);
            float walletGap = ResolveWalletGap(visibleHeight, cardStep);
            Vector3 halfGap = new Vector3(0f, walletGap * 0.5f, 0f);

            return new PauseMenuWalletLayout(
                originalWalletPosition + injectedBaseOffset + halfGap,
                originalWalletPosition + vanillaBaseOffset - halfGap,
                walletGap,
                visibleHeight);
        }

        private static float ResolveWalletGap(float visibleHeight, float cardStep)
        {
            float cardBasedGap = Mathf.Abs(cardStep) * 2f + MinimumWalletGapPadding;
            float walletBoundsGap = Mathf.Max(1f, visibleHeight + MinimumWalletSeparation);
            return Mathf.Max(cardBasedGap, walletBoundsGap);
        }

        private static float ResolveVisibleHeight(GameObject wallet)
        {
            if (wallet == null || wallet.transform == null)
                return FallbackWalletVisibleHeight;

            Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(wallet.transform, true);
            if (bounds.size.y >= 1f)
                return bounds.size.y;

            UIWidget widget = wallet.GetComponentInChildren<UIWidget>(true);
            return widget != null && widget.height > 0 ? widget.height : FallbackWalletVisibleHeight;
        }
    }

    internal static class PauseMenuComponentCleanup
    {
        public static int DestroyComponentsOnSelf<TComponent>(GameObject root) where TComponent : Component
        {
            if (root == null)
                return 0;

            int destroyed = 0;
            TComponent[] components = root.GetComponents<TComponent>();
            for (int i = 0; i < components.Length; i++)
            {
                TComponent component = components[i];
                if (component == null)
                    continue;

                Disable(component);
                UnityEngine.Object.Destroy(component);
                destroyed++;
            }

            return destroyed;
        }

        public static int DestroyComponents<TComponent>(GameObject root) where TComponent : Component
        {
            if (root == null)
                return 0;

            int destroyed = 0;
            TComponent[] components = root.GetComponentsInChildren<TComponent>(true);
            for (int i = 0; i < components.Length; i++)
            {
                TComponent component = components[i];
                if (component == null)
                    continue;

                Disable(component);
                UnityEngine.Object.Destroy(component);
                destroyed++;
            }

            return destroyed;
        }

        public static int CountChildComponentsExcludingRoot<TComponent>(GameObject root) where TComponent : Component
        {
            if (root == null)
                return 0;

            int count = 0;
            TComponent[] components = root.GetComponentsInChildren<TComponent>(true);
            for (int i = 0; i < components.Length; i++)
            {
                TComponent component = components[i];
                if (component == null || component.gameObject == root)
                    continue;

                count++;
            }

            return count;
        }

        public static int ResetAndDestroyEventListeners(GameObject root)
        {
            if (root == null)
                return 0;

            int destroyed = 0;
            UIEventListener[] listeners = root.GetComponentsInChildren<UIEventListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                UIEventListener listener = listeners[i];
                if (listener == null)
                    continue;

                listener.onSubmit = null;
                listener.onClick = null;
                listener.onDoubleClick = null;
                listener.onHover = null;
                listener.onPress = null;
                listener.onSelect = null;
                listener.onScroll = null;
                listener.onDrag = null;
                listener.onDrop = null;
                listener.onKey = null;
                listener.enabled = false;
                UnityEngine.Object.Destroy(listener);
                destroyed++;
            }

            return destroyed;
        }

        public static int ClearEventListenerActions(GameObject root)
        {
            if (root == null)
                return 0;

            int cleared = 0;
            UIEventListener[] listeners = root.GetComponentsInChildren<UIEventListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                UIEventListener listener = listeners[i];
                if (listener == null)
                    continue;

                listener.onSubmit = null;
                listener.onClick = null;
                listener.onDoubleClick = null;
                listener.onPress = null;
                listener.onScroll = null;
                listener.onDrag = null;
                listener.onDrop = null;
                listener.onKey = null;
                cleared++;
            }

            return cleared;
        }

        private static void Disable(Component component)
        {
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null)
                behaviour.enabled = false;

            Collider collider = component as Collider;
            if (collider != null)
                collider.enabled = false;

            Collider2D collider2D = component as Collider2D;
            if (collider2D != null)
                collider2D.enabled = false;
        }
    }

    internal static class PauseMenuWalletBuilder
    {
        public static GameObject EnsureInjectedWallet(Transform parent, GameObject templateWallet, string objectName)
        {
            if (parent == null || templateWallet == null || string.IsNullOrEmpty(objectName))
                return null;

            Transform existing = parent.Find(objectName);
            if (existing != null)
                RetireExistingInjectedWallet(existing);

            GameObject clone = UnityEngine.Object.Instantiate(templateWallet) as GameObject;
            if (clone == null)
                return null;

            clone.name = objectName;
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = templateWallet.transform.localPosition;
            clone.transform.localRotation = templateWallet.transform.localRotation;
            clone.transform.localScale = templateWallet.transform.localScale;
            clone.layer = ResolveUiLayer(templateWallet);
            NGUITools.SetLayer(clone, clone.layer);
            StripTemplateState(clone);
            SuppressInjectedWalletMotion(clone);
            clone.SetActive(templateWallet.activeSelf);
            return clone;
        }

        private static void RetireExistingInjectedWallet(Transform existing)
        {
            if (existing == null)
                return;

            GameObject oldWallet = existing.gameObject;
            oldWallet.SetActive(false);
            oldWallet.name = oldWallet.name + "_Retired_" + Time.frameCount;
            UnityEngine.Object.Destroy(oldWallet);
            MMLog.WriteInfo("[PauseMenuCardPatches] Retired stale injected wallet before rebuilding from the current vanilla template.");
        }

        public static void ConfigureInjectedWallet(
            GameObject wallet,
            Transform keybindTemplate,
            Transform modsTemplate,
            PauseMenuInjectedCardDefinition keybindsCard,
            PauseMenuInjectedCardDefinition modsCard)
        {
            if (wallet == null)
                return;

            GameObject keybindTab = ResolveTab(wallet.transform, keybindsCard.ObjectName, "tab1");
            GameObject modsTab = ResolveTab(wallet.transform, modsCard.ObjectName, "tab2");

            if (keybindTab == null || modsTab == null)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] Injected wallet is missing tab1/tab2.");
                return;
            }

            keybindTab.name = keybindsCard.ObjectName;
            modsTab.name = modsCard.ObjectName;
            RestoreTabTransform(keybindTab.transform, keybindTemplate, PauseMenuCardAugmentationService.CardLocalPositionCompensation);
            RestoreTabTransform(modsTab.transform, modsTemplate, PauseMenuCardAugmentationService.CardLocalPositionCompensation);
            PauseMenuCardBuilder.ConfigureExistingCard(keybindTab, keybindsCard);
            PauseMenuCardBuilder.ConfigureExistingCard(modsTab, modsCard);
        }

        private static void RestoreTabTransform(Transform tab, Transform template, Vector3 localPositionCompensation)
        {
            if (tab == null || template == null)
                return;

            tab.localPosition = template.localPosition + localPositionCompensation;
            tab.localRotation = template.localRotation;
            tab.localScale = template.localScale;
        }

        private static GameObject ResolveTab(Transform wallet, string stableName, string vanillaName)
        {
            if (wallet == null)
                return null;

            Transform stable = wallet.Find(stableName);
            if (stable != null)
                return stable.gameObject;

            Transform vanilla = wallet.Find(vanillaName);
            return vanilla != null ? vanilla.gameObject : null;
        }

        private static int ResolveUiLayer(GameObject templateWallet)
        {
            UIPanel panel = NGUITools.FindInParents<UIPanel>(templateWallet);
            if (panel != null && panel.gameObject != null)
                return panel.gameObject.layer;

            return templateWallet.layer;
        }

        private static void StripTemplateState(GameObject clone)
        {
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuWalletPositionLock>(clone);
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuOriginalPosition>(clone);
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuOriginalTweenPosition>(clone);
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuOriginalWidgetDepth>(clone);
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuOriginalPanelDepth>(clone);
            PauseMenuComponentCleanup.DestroyComponents<PauseMenuOriginalWidgetColor>(clone);
        }

        public static void SuppressInjectedWalletMotion(GameObject wallet)
        {
            if (wallet == null)
                return;

            int rootPauseButtons = PauseMenuComponentCleanup.DestroyComponentsOnSelf<PauseMenuButton>(wallet);
            int rootPlayTweens = PauseMenuComponentCleanup.DestroyComponentsOnSelf<UIPlayTween>(wallet);
            int rootPositionTweens = PauseMenuComponentCleanup.DestroyComponentsOnSelf<TweenPosition>(wallet);
            int preservedPauseButtons = PauseMenuComponentCleanup.CountChildComponentsExcludingRoot<PauseMenuButton>(wallet);
            int preservedPositionTweens = PauseMenuComponentCleanup.CountChildComponentsExcludingRoot<TweenPosition>(wallet);
            MMLog.WriteInfo("[PauseMenuCardPatches] Suppressed injected wallet root motion on " + wallet.name
                + " rootPauseButtons=" + rootPauseButtons
                + " rootPlayTweens=" + rootPlayTweens
                + " rootPositionTweens=" + rootPositionTweens
                + " preservedCardPauseButtons=" + preservedPauseButtons
                + " preservedChildPositionTweens=" + preservedPositionTweens + ".");
        }
    }

    internal static class PauseMenuCardBuilder
    {
        private static readonly Color LabelColor = new Color(0.98f, 0.98f, 0.92f, 1f);
        private static readonly Color LabelOutlineColor = new Color(0f, 0f, 0f, 0.85f);

        public static void ConfigureExistingCard(GameObject card, PauseMenuInjectedCardDefinition definition)
        {
            ConfigureCard(card, definition);
        }

        private static void ConfigureCard(GameObject card, PauseMenuInjectedCardDefinition definition)
        {
            PauseMenuCardActionBinder.StripInheritedActions(card);

            UILabel primaryLabel = FindPrimaryLabel(card);
            ConfigureLabels(card, primaryLabel, definition.LabelText);
            ConfigureSprites(card, definition, primaryLabel);
            ConfigureButtons(card, definition, primaryLabel);
        }

        private static UILabel FindPrimaryLabel(GameObject card)
        {
            Transform namedLabel = FindChildIgnoreCase(card.transform, "label");
            if (namedLabel != null)
            {
                UILabel label = namedLabel.GetComponent<UILabel>();
                if (label != null)
                    return label;
            }

            UILabel[] labels = card.GetComponentsInChildren<UILabel>(true);
            UILabel best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;

                int score = label.width * Math.Max(1, label.height) + label.fontSize;
                if (best == null || score > bestScore)
                {
                    best = label;
                    bestScore = score;
                }
            }

            return best;
        }

        private static void ConfigureLabels(GameObject card, UILabel primaryLabel, string text)
        {
            UILabel[] labels = card.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;

                bool isPrimary = label == primaryLabel;
                label.enabled = isPrimary;
                label.text = isPrimary ? text : string.Empty;
                label.color = WithAlpha(LabelColor, isPrimary ? 0f : LabelColor.a);
                label.effectStyle = UILabel.Effect.Outline;
                label.effectColor = LabelOutlineColor;
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
                label.alignment = NGUIText.Alignment.Center;
                label.multiLine = false;
                label.ProcessText();
                label.MarkAsChanged();
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void ConfigureSprites(GameObject card, PauseMenuInjectedCardDefinition definition, UILabel primaryLabel)
        {
            UISprite icon = FindIconSprite(card);
            RestoreOriginalWidgetColors(card, primaryLabel);

            if (icon != null)
            {
                string spriteName = PauseMenuIconSpriteResolver.Resolve(icon, definition.IconSpriteTokens);
                if (!string.IsNullOrEmpty(spriteName))
                    icon.spriteName = spriteName;
                icon.alpha = 1f;
                icon.enabled = true;
                icon.color = definition.IconTint;
                icon.MarkAsChanged();
            }
        }

        private static void RestoreOriginalWidgetColors(GameObject card, UILabel primaryLabel)
        {
            UIWidget[] widgets = card.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || widget == primaryLabel)
                    continue;

                UILabel label = widget as UILabel;
                if (label != null)
                    continue;

                PauseMenuOriginalWidgetColor original = PauseMenuOriginalWidgetColor.Capture(widget);
                widget.alpha = 1f;
                widget.enabled = true;
                widget.color = original.Color;
                widget.MarkAsChanged();
            }
        }

        private static void ConfigureButtons(GameObject card, PauseMenuInjectedCardDefinition definition, UILabel primaryLabel)
        {
            UIButton targetButton = FindPrimaryButton(card);
            UIButton[] buttons = card.GetComponentsInChildren<UIButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button == null)
                    continue;

                button.isEnabled = button == targetButton;
                if (button.onClick != null)
                    button.onClick.Clear();
            }

            if (targetButton != null)
            {
                PauseMenuCardActionBinder.ConfigureClickSurface(card, targetButton);
                PauseMenuCardActionBinder.Bind(targetButton, definition.OnClick);
                PauseMenuCardHoverBinder.Bind(card, targetButton, primaryLabel);
            }
        }

        private static UIButton FindPrimaryButton(GameObject card)
        {
            Transform namedButton = FindChildIgnoreCase(card.transform, "button");
            if (namedButton != null)
            {
                UIButton named = namedButton.GetComponent<UIButton>();
                if (named != null)
                    return named;
            }

            UIButton[] buttons = card.GetComponentsInChildren<UIButton>(true);
            if (buttons == null || buttons.Length == 0)
                return card.GetComponent<UIButton>();

            UIButton best = null;
            float bestArea = -1f;
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button == null)
                    continue;

                BoxCollider collider = button.GetComponent<BoxCollider>();
                float area = collider != null ? collider.size.x * collider.size.y : 0f;
                if (best == null || area > bestArea)
                {
                    best = button;
                    bestArea = area;
                }
            }

            return best;
        }

        private static UISprite FindIconSprite(GameObject card)
        {
            Transform iconTransform = FindChildIgnoreCase(card.transform, "Icon");
            if (iconTransform != null)
            {
                UISprite icon = iconTransform.GetComponent<UISprite>();
                if (icon != null)
                    return icon;
            }

            UISprite[] sprites = card.GetComponentsInChildren<UISprite>(true);
            UISprite best = null;
            int bestArea = int.MaxValue;
            for (int i = 0; i < sprites.Length; i++)
            {
                UISprite sprite = sprites[i];
                if (sprite == null)
                    continue;

                int area = Math.Max(1, sprite.width) * Math.Max(1, sprite.height);
                if (area < bestArea)
                {
                    best = sprite;
                    bestArea = area;
                }
            }

            return best;
        }

        private static Transform FindChildIgnoreCase(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform nested = FindChildIgnoreCase(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

    }

    internal static class PauseMenuCardHoverBinder
    {
        public static void Bind(GameObject card, UIButton targetButton, UILabel primaryLabel)
        {
            if (card == null || targetButton == null || targetButton.gameObject == null)
                return;

            PauseMenuButton pauseButton = ResolvePauseMenuButton(card, targetButton.gameObject);
            UIWidget selectionWidget = targetButton.GetComponent<UIWidget>();
            PauseMenuInjectedCardHoverController controller = targetButton.gameObject.GetComponent<PauseMenuInjectedCardHoverController>();
            if (controller == null)
                controller = targetButton.gameObject.AddComponent<PauseMenuInjectedCardHoverController>();

            bool vanillaHighlightReady = TryCancelHighlight(pauseButton, card.name);
            controller.Configure(card.name, pauseButton, primaryLabel, selectionWidget, vanillaHighlightReady);
            MMLog.WriteInfo("[PauseMenuCardPatches] Hover controller bound for " + card.name
                + " target=" + targetButton.gameObject.name
                + " pauseButton=" + (pauseButton != null ? pauseButton.gameObject.name : "<none>")
                + " label=" + (primaryLabel != null ? primaryLabel.gameObject.name : "<none>")
                + " selectionWidget=" + (selectionWidget != null ? selectionWidget.gameObject.name : "<none>")
                + " vanillaReady=" + vanillaHighlightReady + ".");
        }

        private static PauseMenuButton ResolvePauseMenuButton(GameObject card, GameObject buttonObject)
        {
            if (buttonObject != null)
            {
                PauseMenuButton onButton = buttonObject.GetComponent<PauseMenuButton>();
                if (onButton != null)
                    return onButton;
            }

            if (card == null)
                return null;

            PauseMenuButton onCard = card.GetComponent<PauseMenuButton>();
            if (onCard != null)
                return onCard;

            return card.GetComponentInChildren<PauseMenuButton>(true);
        }

        private static bool TryCancelHighlight(PauseMenuButton pauseButton, string cardName)
        {
            if (pauseButton == null)
                return false;

            try
            {
                pauseButton.CancelHighlight();
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] Failed to reset vanilla hover state for "
                    + (cardName ?? "<unknown>") + ": " + ex.Message);
                return false;
            }
        }
    }

    internal sealed class PauseMenuInjectedCardHoverController : MonoBehaviour
    {
        private const float FallbackFadeDuration = 0.14f;
        private const float RestAlpha = 0f;
        private const float HoverAlpha = 1f;

        private string _cardName;
        private PauseMenuButton _pauseButton;
        private UILabel _label;
        private UIWidget _selectionWidget;
        private bool _vanillaHighlightReady;
        private bool _configured;
        private bool _animatingFallback;
        private float _targetLabelAlpha;
        private float _targetSelectionAlpha;
        private bool _hasLoggedHoverState;
        private bool _lastHoverState;
        private bool _lastHoverWasSelect;

        public void Configure(string cardName, PauseMenuButton pauseButton, UILabel label, UIWidget selectionWidget, bool vanillaHighlightReady)
        {
            _cardName = cardName ?? gameObject.name;
            _pauseButton = pauseButton;
            _label = label;
            _selectionWidget = selectionWidget;
            _vanillaHighlightReady = vanillaHighlightReady;
            _configured = true;

            if (!_vanillaHighlightReady)
                SetFallbackHighlighted(false, true);
        }

        private void OnHover(bool selected)
        {
            if (PlatformInput.InputMethod != PlatformInput.InputType.KeyboardMouse && selected)
                return;

            ApplyHighlight(selected, false);
        }

        private void OnSelect(bool selected)
        {
            if (PlatformInput.InputMethod == PlatformInput.InputType.KeyboardMouse && selected)
                return;

            ApplyHighlight(selected, true);
        }

        private void ApplyHighlight(bool selected, bool selectionEvent)
        {
            if (!_configured)
                return;

            if (_vanillaHighlightReady)
            {
                if (_pauseButton == null)
                {
                    _vanillaHighlightReady = false;
                }
                else if (_pauseButton.gameObject != gameObject)
                {
                    try
                    {
                        if (selectionEvent)
                            _pauseButton.OnSelect(selected);
                        else
                            _pauseButton.OnHover(selected);
                        LogHighlight(selected, selectionEvent, "vanilla-forward");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _vanillaHighlightReady = false;
                        MMLog.WriteWarning("[PauseMenuCardPatches] Vanilla hover forwarding failed for "
                            + gameObject.name + ": " + ex.Message);
                    }
                }
                else
                {
                    return;
                }
            }

            SetFallbackHighlighted(selected, false);
            LogHighlight(selected, selectionEvent, "fallback");
        }

        private void LogHighlight(bool selected, bool selectionEvent, string route)
        {
            if (_hasLoggedHoverState && _lastHoverState == selected && _lastHoverWasSelect == selectionEvent)
                return;

            _hasLoggedHoverState = true;
            _lastHoverState = selected;
            _lastHoverWasSelect = selectionEvent;
            MMLog.WriteInfo("[PauseMenuCardPatches] Hover event card=" + (_cardName ?? gameObject.name)
                + " target=" + gameObject.name
                + " selected=" + selected
                + " event=" + (selectionEvent ? "select" : "hover")
                + " route=" + route
                + " input=" + PlatformInput.InputMethod
                + " hovered=" + (UICamera.hoveredObject != null ? UICamera.hoveredObject.name : "<none>")
                + " selectedObject=" + (UICamera.selectedObject != null ? UICamera.selectedObject.name : "<none>")
                + " labelAlpha=" + ReadAlpha(_label).ToString("0.00")
                + " selectionAlpha=" + ReadAlpha(_selectionWidget).ToString("0.00") + ".");
        }

        private void SetFallbackHighlighted(bool highlighted, bool immediate)
        {
            _targetLabelAlpha = highlighted ? HoverAlpha : RestAlpha;
            _targetSelectionAlpha = highlighted ? HoverAlpha : RestAlpha;

            if (immediate)
            {
                ApplyFallbackAlpha(_targetLabelAlpha, _targetSelectionAlpha);
                _animatingFallback = false;
                return;
            }

            _animatingFallback = true;
        }

        private void Update()
        {
            if (!_configured || _vanillaHighlightReady || !_animatingFallback)
                return;

            float delta = FallbackFadeDuration > 0f ? Time.unscaledDeltaTime / FallbackFadeDuration : 1f;
            float labelAlpha = Mathf.MoveTowards(ReadAlpha(_label), _targetLabelAlpha, delta);
            float selectionAlpha = Mathf.MoveTowards(ReadAlpha(_selectionWidget), _targetSelectionAlpha, delta);
            ApplyFallbackAlpha(labelAlpha, selectionAlpha);

            if (Mathf.Abs(labelAlpha - _targetLabelAlpha) < 0.001f
                && Mathf.Abs(selectionAlpha - _targetSelectionAlpha) < 0.001f)
                _animatingFallback = false;
        }

        private void ApplyFallbackAlpha(float labelAlpha, float selectionAlpha)
        {
            SetAlpha(_label, labelAlpha);
            SetAlpha(_selectionWidget, selectionAlpha);
        }

        private static float ReadAlpha(UIWidget widget)
        {
            return widget != null ? widget.alpha : 0f;
        }

        private static void SetAlpha(UIWidget widget, float alpha)
        {
            if (widget == null)
                return;

            widget.alpha = Mathf.Clamp01(alpha);
            widget.MarkAsChanged();
        }
    }

    internal static class PauseMenuCardActionBinder
    {
        public static void ConfigureClickSurface(GameObject card, UIButton targetButton)
        {
            if (card == null || targetButton == null || targetButton.gameObject == null)
                return;

            int disabledColliders = 0;
            BoxCollider[] colliders = card.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider == null || collider.gameObject == targetButton.gameObject)
                    continue;

                collider.enabled = false;
                disabledColliders++;
            }

            BoxCollider targetCollider = targetButton.GetComponent<BoxCollider>();
            if (targetCollider == null)
                targetCollider = targetButton.gameObject.AddComponent<BoxCollider>();

            UIWidget targetWidget = targetButton.GetComponent<UIWidget>();
            if (targetWidget != null)
            {
                targetCollider.size = new Vector3(Mathf.Max(1f, targetWidget.width), Mathf.Max(1f, targetWidget.height), 1f);
                targetCollider.center = Vector3.zero;
            }

            targetCollider.enabled = true;
            MMLog.WriteInfo("[PauseMenuCardPatches] Configured click surface for " + card.name
                + " target=" + targetButton.gameObject.name
                + " colliderSize=" + FormatVector(targetCollider.size)
                + " disabledColliders=" + disabledColliders + ".");
        }

        public static void StripInheritedActions(GameObject card)
        {
            if (card == null)
                return;

            PauseMenuComponentCleanup.DestroyComponents<UILocalize>(card);
            PauseMenuComponentCleanup.DestroyComponents<UIButtonMessage>(card);
            PauseMenuComponentCleanup.DestroyComponents<UIPlayAnimation>(card);
            PauseMenuComponentCleanup.DestroyComponents<UIPlayTween>(card);
            PauseMenuComponentCleanup.DestroyComponents<UIKeyNavigation>(card);
            PauseMenuComponentCleanup.ClearEventListenerActions(card);
        }

        public static void Bind(UIButton button, Action action)
        {
            if (button == null)
                return;

            if (button.onClick != null)
                button.onClick.Clear();

            button.isEnabled = true;
            EventDelegate.Add(button.onClick, delegate
            {
                if (action != null)
                    action();
            });
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.0") + "," + value.y.ToString("0.0") + "," + value.z.ToString("0.0") + ")";
        }

    }

    internal static class PauseMenuCardActions
    {
        public static void OpenKeybinds()
        {
            try
            {
                MMLog.WriteInfo("[PauseMenuCardPatches] Opening keybinds from pause menu.");
                PauseMenuCustomOverlayGuard.HidePauseMenuForDirectOverlay("keybinds");
                ShelteredKeybindsUI.Show();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PauseMenuCardPatches] Failed to open keybinds from pause menu: " + ex);
            }
        }

        public static void OpenModManager()
        {
            try
            {
                MMLog.WriteInfo("[PauseMenuCardPatches] Opening mod manager from pause menu.");
                ModManagerPanel.ShowPanel();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PauseMenuCardPatches] Failed to open mod manager from pause menu: " + ex);
            }
        }
    }

    internal static class PauseMenuCustomOverlayGuard
    {
        private static readonly List<GameObject> TemporarilyHiddenPanels = new List<GameObject>();
        private static bool _restoreHooked;

        public static void HidePauseMenuForDirectOverlay(string source)
        {
            EnsureRestoreHook();

            BasePanel topPanel = UIPanelManager.instance != null ? UIPanelManager.instance.GetTopPanel() : null;
            if (!(topPanel is MainMenuPanel) || topPanel.gameObject == null || !topPanel.gameObject.activeSelf)
                return;

            if (!TemporarilyHiddenPanels.Contains(topPanel.gameObject))
                TemporarilyHiddenPanels.Add(topPanel.gameObject);

            topPanel.gameObject.SetActive(false);
            UICamera.selectedObject = null;
            MMLog.WriteInfo("[PauseMenuCardPatches] Temporarily hid pause menu before opening " + source + ".");
        }

        private static void EnsureRestoreHook()
        {
            if (_restoreHooked)
                return;

            _restoreHooked = true;
            ShelteredKeybindsUIV2.Closed += RestoreHiddenPanels;
        }

        private static void RestoreHiddenPanels()
        {
            if (TemporarilyHiddenPanels.Count == 0)
                return;

            for (int i = 0; i < TemporarilyHiddenPanels.Count; i++)
            {
                GameObject panel = TemporarilyHiddenPanels[i];
                if (panel != null)
                    panel.SetActive(true);
            }

            MMLog.WriteInfo("[PauseMenuCardPatches] Restored " + TemporarilyHiddenPanels.Count + " temporarily hidden pause menu panel(s).");
            TemporarilyHiddenPanels.Clear();
        }
    }

    internal static class PauseMenuIconSpriteResolver
    {
        public static string Resolve(UISprite sprite, string[] tokens)
        {
            if (sprite == null || sprite.atlas == null || tokens == null || tokens.Length == 0)
                return null;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                    continue;

                if (sprite.atlas.GetSprite(token) != null)
                    return token;
            }

            if (sprite.atlas.spriteList == null)
                return null;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                    continue;

                for (int j = 0; j < sprite.atlas.spriteList.Count; j++)
                {
                    UISpriteData data = sprite.atlas.spriteList[j];
                    if (data == null || string.IsNullOrEmpty(data.name))
                        continue;

                    if (data.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        return data.name;
                }
            }

            return null;
        }
    }

    internal static class PauseMenuWalletDepthService
    {
        public static void ApplyDepthOffset(GameObject wallet, int offset)
        {
            if (wallet == null)
                return;

            UIPanel parentPanel = NGUITools.FindInParents<UIPanel>(wallet);
            int panelMaxDepth = NGUIHelper.GetMaxDepth(parentPanel);

            UIPanel[] panels = wallet.GetComponentsInChildren<UIPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel panel = panels[i];
                if (panel == null)
                    continue;

                PauseMenuOriginalPanelDepth original = PauseMenuOriginalPanelDepth.Capture(panel);
                panel.depth = original.Depth + offset;
            }

            UIWidget[] widgets = wallet.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                PauseMenuOriginalWidgetDepth original = PauseMenuOriginalWidgetDepth.Capture(widget);
                widget.depth = original.Depth + offset;
                widget.MarkAsChanged();
            }

            MMLog.WriteInfo("[PauseMenuCardPatches] Applied wallet depth offset " + offset
                + " to " + wallet.name
                + " widgets=" + widgets.Length
                + " childPanels=" + panels.Length
                + " parentPanelMaxDepth=" + panelMaxDepth + ".");
        }
    }

    internal static class PauseMenuWalletFaceDepthService
    {
        public static void BringWalletFacesForward(GameObject wallet, params string[] cardNames)
        {
            if (wallet == null)
                return;

            List<Transform> cardRoots = ResolveCardRoots(wallet.transform, cardNames);
            if (cardRoots.Count == 0)
                return;

            UIWidget[] widgets = wallet.GetComponentsInChildren<UIWidget>(true);
            int maxCardDepth = int.MinValue;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || !IsUnderAny(widget.transform, cardRoots))
                    continue;

                maxCardDepth = Math.Max(maxCardDepth, widget.depth);
            }

            if (maxCardDepth == int.MinValue)
                return;

            int updated = 0;
            int firstDepth = maxCardDepth + 1;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || IsUnderAny(widget.transform, cardRoots))
                    continue;

                widget.depth = firstDepth + updated;
                widget.MarkAsChanged();
                updated++;
            }

            MMLog.WriteInfo("[PauseMenuCardPatches] Raised wallet faces for " + wallet.name
                + " faceWidgets=" + updated
                + " cardMaxDepth=" + maxCardDepth
                + " firstFaceDepth=" + (updated > 0 ? firstDepth.ToString() : "<none>") + ".");
        }

        private static List<Transform> ResolveCardRoots(Transform wallet, string[] cardNames)
        {
            List<Transform> roots = new List<Transform>();
            if (wallet == null || cardNames == null)
                return roots;

            for (int i = 0; i < cardNames.Length; i++)
            {
                string cardName = cardNames[i];
                if (string.IsNullOrEmpty(cardName))
                    continue;

                Transform child = wallet.Find(cardName);
                if (child != null && !roots.Contains(child))
                    roots.Add(child);
            }

            return roots;
        }

        private static bool IsUnderAny(Transform current, List<Transform> roots)
        {
            if (current == null || roots == null)
                return false;

            for (int i = 0; i < roots.Count; i++)
            {
                if (IsUnder(current, roots[i]))
                    return true;
            }

            return false;
        }

        private static bool IsUnder(Transform current, Transform root)
        {
            Transform cursor = current;
            while (cursor != null)
            {
                if (cursor == root)
                    return true;

                cursor = cursor.parent;
            }

            return false;
        }
    }

    internal static class PauseMenuWalletDebugService
    {
        public static void LogStackPlan(
            GameObject templateWallet,
            Vector3 originalWalletPosition,
            Vector3 injectedWalletPosition,
            Vector3 vanillaWalletPosition,
            float cardStep,
            float walletGap,
            float walletVisibleHeight)
        {
            float walletHeight = ResolveLargestWidgetHeight(templateWallet);
            float xDelta = Mathf.Abs(injectedWalletPosition.x - vanillaWalletPosition.x);
            float yDelta = Mathf.Abs(injectedWalletPosition.y - vanillaWalletPosition.y);
            float walletOverlap = Mathf.Max(0f, walletVisibleHeight - yDelta);
            MMLog.WriteInfo("[PauseMenuCardPatches] Wallet stack plan"
                + " original=" + FormatVector(originalWalletPosition)
                + " injected=" + FormatVector(injectedWalletPosition)
                + " vanilla=" + FormatVector(vanillaWalletPosition)
                + " cardStep=" + cardStep.ToString("0.0")
                + " walletGap=" + walletGap.ToString("0.0")
                + " yDelta=" + yDelta.ToString("0.0")
                + " xDelta=" + xDelta.ToString("0.0")
                + " visibleHeight=" + walletVisibleHeight.ToString("0.0")
                + " verticalOverlap=" + walletOverlap.ToString("0.0")
                + " largestWidgetHeight=" + walletHeight.ToString("0.0")
                + " alignedX=" + (xDelta < 0.1f) + ".");
        }

        public static void LogLayout(
            Transform menu,
            GameObject injectedWallet,
            GameObject vanillaWallet,
            string keybindsCardName,
            string modsCardName)
        {
            bool previous = UIDebug.Enabled;
            UIDebug.Enabled = true;
            try
            {
                if (menu != null)
                    UIDebug.LogWidgetHierarchy(menu, 2);

                LogWallet("Injected", injectedWallet);
                LogWallet("Vanilla", vanillaWallet);
                LogCard("Keybinds", injectedWallet, keybindsCardName);
                LogCard("Mods", injectedWallet, modsCardName);
                LogCard("Settings", vanillaWallet, "tab1");
                LogCard("SaveExit", vanillaWallet, "tab2");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] UI debug logging failed: " + ex.Message);
            }
            finally
            {
                UIDebug.Enabled = previous;
            }
        }

        private static void LogWallet(string label, GameObject wallet)
        {
            if (wallet == null)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] " + label + " wallet is null.");
                return;
            }

            UIDebug.ValidateNGUISetup(wallet, "PauseMenu " + label + " wallet");
            UIWidget firstWidget = wallet.GetComponentInChildren<UIWidget>(true);
            UIPanel panel = NGUITools.FindInParents<UIPanel>(wallet);
            int effectiveDepth = firstWidget != null ? UIDebug.GetEffectiveDepth(firstWidget) : 0;
            int panelMaxDepth = NGUIHelper.GetMaxDepth(panel);

            MMLog.WriteInfo("[PauseMenuCardPatches] " + label
                + " wallet name=" + wallet.name
                + " layer=" + wallet.layer
                + " local=" + FormatVector(wallet.transform.localPosition)
                + " panel=" + (panel != null ? panel.name : "<none>")
                + " panelDepth=" + (panel != null ? panel.depth.ToString() : "0")
                + " panelMaxDepth=" + panelMaxDepth
                + " firstWidgetDepth=" + (firstWidget != null ? firstWidget.depth.ToString() : "<none>")
                + " firstEffectiveDepth=" + effectiveDepth + ".");
        }

        private static void LogCard(string label, GameObject wallet, string childName)
        {
            if (wallet == null || string.IsNullOrEmpty(childName))
                return;

            Transform child = wallet.transform.Find(childName);
            if (child == null)
            {
                MMLog.WriteWarning("[PauseMenuCardPatches] " + label + " card '" + childName + "' not found under " + wallet.name + ".");
                return;
            }

            GameObject card = child.gameObject;
            UIDebug.ValidateNGUISetup(card, "PauseMenu " + label + " card");
            UIWidget widget = card.GetComponentInChildren<UIWidget>(true);
            UIButton button = card.GetComponentInChildren<UIButton>(true);
            PauseMenuButton[] pauseButtons = card.GetComponentsInChildren<PauseMenuButton>(true);
            UITweener[] tweens = card.GetComponentsInChildren<UITweener>(true);
            UIEventListener[] listeners = card.GetComponentsInChildren<UIEventListener>(true);
            BoxCollider[] colliders = card.GetComponentsInChildren<BoxCollider>(true);
            int effectiveDepth = widget != null ? UIDebug.GetEffectiveDepth(widget) : 0;

            MMLog.WriteInfo("[PauseMenuCardPatches] " + label
                + " card local=" + FormatVector(card.transform.localPosition)
                + " layer=" + card.layer
                + " widgetDepth=" + (widget != null ? widget.depth.ToString() : "<none>")
                + " effectiveDepth=" + effectiveDepth
                + " buttonDelegates=" + (button != null && button.onClick != null ? button.onClick.Count.ToString() : "<none>")
                + " pauseButtons=" + pauseButtons.Length
                + " tweens=" + tweens.Length
                + " eventListeners=" + listeners.Length
                + " enabledColliders=" + CountEnabledColliders(colliders)
                + ".");

            LogWidgetChildren(label, card);
        }

        private static int CountEnabledColliders(BoxCollider[] colliders)
        {
            if (colliders == null)
                return 0;

            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider != null && collider.enabled)
                    count++;
            }

            return count;
        }

        private static void LogWidgetChildren(string label, GameObject card)
        {
            UIWidget[] widgets = card.GetComponentsInChildren<UIWidget>(true);
            int count = Math.Min(widgets.Length, 12);
            for (int i = 0; i < count; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                UISprite sprite = widget as UISprite;
                UILabel labelWidget = widget as UILabel;
                string kind = sprite != null ? "sprite:" + sprite.spriteName : (labelWidget != null ? "label:" + labelWidget.text : widget.GetType().Name);
                MMLog.WriteInfo("[PauseMenuCardPatches] " + label
                    + " widget[" + i + "] path=" + RelativePath(card.transform, widget.transform)
                    + " kind=" + kind
                    + " local=" + FormatVector(widget.transform.localPosition)
                    + " size=" + widget.width + "x" + widget.height
                    + " depth=" + widget.depth
                    + " color=" + FormatColor(widget.color) + ".");
            }
        }

        private static string RelativePath(Transform root, Transform current)
        {
            if (root == null || current == null)
                return "<unknown>";

            string path = current.name;
            Transform t = current.parent;
            while (t != null && t != root)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }

            return path;
        }

        private static string FormatColor(Color value)
        {
            return "(" + value.r.ToString("0.00") + "," + value.g.ToString("0.00") + "," + value.b.ToString("0.00") + "," + value.a.ToString("0.00") + ")";
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.0") + "," + value.y.ToString("0.0") + "," + value.z.ToString("0.0") + ")";
        }

        private static float ResolveLargestWidgetHeight(GameObject wallet)
        {
            if (wallet == null)
                return 0f;

            UIWidget[] widgets = wallet.GetComponentsInChildren<UIWidget>(true);
            float height = 0f;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                height = Mathf.Max(height, widget.height);
            }

            return height;
        }
    }

    internal static class PauseMenuWalletPositioner
    {
        public static void ApplyCoreLocalPosition(GameObject wallet, Vector3 targetLocalPosition)
        {
            if (wallet == null || wallet.transform == null)
                return;

            PauseMenuOriginalPosition originalPosition = PauseMenuOriginalPosition.Capture(wallet);
            Vector3 delta = targetLocalPosition - originalPosition.OriginalLocalPosition;
            RetargetPositionTweens(wallet, delta);
            ApplyLocalPosition(wallet, targetLocalPosition);
            PauseMenuWalletPositionLock.Bind(wallet, targetLocalPosition);
        }

        private static void RetargetPositionTweens(GameObject wallet, Vector3 delta)
        {
            TweenPosition[] tweens = wallet.GetComponents<TweenPosition>();
            for (int i = 0; i < tweens.Length; i++)
            {
                TweenPosition tween = tweens[i];
                if (tween == null)
                    continue;

                PauseMenuOriginalTweenPosition original = PauseMenuOriginalTweenPosition.Capture(tween);
                tween.from = original.From + delta;
                tween.to = original.To + delta;
            }
        }

        internal static void ApplyLocalPosition(GameObject wallet, Vector3 targetLocalPosition)
        {
            UIRect rect = wallet.GetComponent<UIRect>();
            if (rect != null && rect.isAnchored)
            {
                Vector3 delta = targetLocalPosition - wallet.transform.localPosition;
                NGUIMath.MoveRect(rect, delta.x, delta.y);
                Vector3 adjusted = wallet.transform.localPosition;
                wallet.transform.localPosition = new Vector3(adjusted.x, adjusted.y, targetLocalPosition.z);
                return;
            }

            wallet.transform.localPosition = targetLocalPosition;
        }
    }

    internal sealed class PauseMenuWalletPositionLock : MonoBehaviour
    {
        private const float Epsilon = 0.01f;
        private Vector3 _targetLocalPosition;
        private bool _configured;

        public static void Bind(GameObject wallet, Vector3 targetLocalPosition)
        {
            if (wallet == null)
                return;

            PauseMenuWalletPositionLock positionLock = wallet.GetComponent<PauseMenuWalletPositionLock>();
            if (positionLock == null)
                positionLock = wallet.AddComponent<PauseMenuWalletPositionLock>();

            positionLock._targetLocalPosition = targetLocalPosition;
            positionLock._configured = true;
            positionLock.Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (!_configured || transform == null)
                return;

            if ((transform.localPosition - _targetLocalPosition).sqrMagnitude <= Epsilon * Epsilon)
                return;

            PauseMenuWalletPositioner.ApplyLocalPosition(gameObject, _targetLocalPosition);
        }
    }

    internal sealed class PauseMenuOriginalPosition : MonoBehaviour
    {
        public Vector3 OriginalLocalPosition;
        private bool _captured;

        public static PauseMenuOriginalPosition Capture(GameObject target)
        {
            PauseMenuOriginalPosition state = target.GetComponent<PauseMenuOriginalPosition>();
            if (state == null)
                state = target.AddComponent<PauseMenuOriginalPosition>();

            if (!state._captured)
            {
                state.OriginalLocalPosition = target.transform.localPosition;
                state._captured = true;
            }

            return state;
        }
    }

    internal sealed class PauseMenuOriginalWidgetDepth : MonoBehaviour
    {
        public int Depth;
        private bool _captured;

        public static PauseMenuOriginalWidgetDepth Capture(UIWidget widget)
        {
            PauseMenuOriginalWidgetDepth state = widget.GetComponent<PauseMenuOriginalWidgetDepth>();
            if (state == null)
                state = widget.gameObject.AddComponent<PauseMenuOriginalWidgetDepth>();

            if (!state._captured)
            {
                state.Depth = widget.depth;
                state._captured = true;
            }

            return state;
        }
    }

    internal sealed class PauseMenuOriginalPanelDepth : MonoBehaviour
    {
        public int Depth;
        private bool _captured;

        public static PauseMenuOriginalPanelDepth Capture(UIPanel panel)
        {
            PauseMenuOriginalPanelDepth state = panel.GetComponent<PauseMenuOriginalPanelDepth>();
            if (state == null)
                state = panel.gameObject.AddComponent<PauseMenuOriginalPanelDepth>();

            if (!state._captured)
            {
                state.Depth = panel.depth;
                state._captured = true;
            }

            return state;
        }
    }

    internal sealed class PauseMenuOriginalWidgetColor : MonoBehaviour
    {
        public Color Color;
        private bool _captured;

        public static PauseMenuOriginalWidgetColor Capture(UIWidget widget)
        {
            PauseMenuOriginalWidgetColor state = widget.GetComponent<PauseMenuOriginalWidgetColor>();
            if (state == null)
                state = widget.gameObject.AddComponent<PauseMenuOriginalWidgetColor>();

            if (!state._captured)
            {
                state.Color = widget.color;
                state._captured = true;
            }

            return state;
        }
    }

    internal sealed class PauseMenuOriginalTweenPosition : MonoBehaviour
    {
        public Vector3 From;
        public Vector3 To;
        private bool _captured;

        public static PauseMenuOriginalTweenPosition Capture(TweenPosition tween)
        {
            PauseMenuOriginalTweenPosition state = tween.GetComponent<PauseMenuOriginalTweenPosition>();
            if (state == null)
                state = tween.gameObject.AddComponent<PauseMenuOriginalTweenPosition>();

            if (!state._captured)
            {
                state.From = tween.from;
                state.To = tween.to;
                state._captured = true;
            }

            return state;
        }
    }
}
