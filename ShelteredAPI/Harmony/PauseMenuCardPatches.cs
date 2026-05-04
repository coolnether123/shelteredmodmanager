using System;
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
        private static readonly Vector3 VanillaWalletBaseOffset = new Vector3(-60f, 0f, 0f);

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
            ClearLegacyInjectedWallet(hierarchy.Menu);

            Vector3 originalWalletPosition = PauseMenuOriginalPosition.Capture(hierarchy.Background.gameObject).OriginalLocalPosition;
            Vector3 walletPosition = originalWalletPosition + VanillaWalletBaseOffset;
            float cardGap = Mathf.Abs(step);

            GameObject keybindCard = PauseMenuWalletBuilder.EnsureInjectedCard(
                hierarchy.Background,
                hierarchy.SettingsTab.gameObject,
                KeybindsCardName);
            GameObject modsCard = PauseMenuWalletBuilder.EnsureInjectedCard(
                hierarchy.Background,
                hierarchy.SaveExitTab.gameObject,
                ModsCardName);

            Vector3 settingsPosition = OffsetY(PauseMenuOriginalPosition.Capture(hierarchy.SettingsTab.gameObject).OriginalLocalPosition, -cardGap);
            Vector3 saveExitPosition = OffsetY(PauseMenuOriginalPosition.Capture(hierarchy.SaveExitTab.gameObject).OriginalLocalPosition, -cardGap);
            Vector3 keybindsPosition = OffsetY(PauseMenuOriginalPosition.Capture(hierarchy.SettingsTab.gameObject).OriginalLocalPosition, cardGap);
            Vector3 modsPosition = OffsetY(PauseMenuOriginalPosition.Capture(hierarchy.SaveExitTab.gameObject).OriginalLocalPosition, cardGap);

            PauseMenuWalletPositioner.ApplyCoreLocalPosition(hierarchy.Background.gameObject, walletPosition);
            PauseMenuWalletPositioner.ApplyCoreLocalPosition(hierarchy.SettingsTab.gameObject, settingsPosition);
            PauseMenuWalletPositioner.ApplyCoreLocalPosition(hierarchy.SaveExitTab.gameObject, saveExitPosition);

            if (keybindCard != null)
            {
                PauseMenuWalletPositioner.ApplyCoreLocalPosition(keybindCard, keybindsPosition);
                PauseMenuWalletBuilder.ConfigureInjectedCard(keybindCard, KeybindsCard);
                PauseMenuWalletDepthService.ApplyDepthOffset(keybindCard, InjectedWalletDepthOffset);
            }

            if (modsCard != null)
            {
                PauseMenuWalletPositioner.ApplyCoreLocalPosition(modsCard, modsPosition);
                PauseMenuWalletBuilder.ConfigureInjectedCard(modsCard, ModsCard);
                PauseMenuWalletDepthService.ApplyDepthOffset(modsCard, InjectedWalletDepthOffset);
            }

            PauseMenuWalletDepthService.ApplyDepthOffset(hierarchy.SettingsTab.gameObject, VanillaWalletDepthOffset);
            PauseMenuWalletDepthService.ApplyDepthOffset(hierarchy.SaveExitTab.gameObject, VanillaWalletDepthOffset);

            PauseMenuWalletDebugService.LogUnifiedStackPlan(
                hierarchy.Background.gameObject,
                originalWalletPosition,
                walletPosition,
                keybindsPosition,
                modsPosition,
                settingsPosition,
                saveExitPosition,
                step,
                cardGap);
            PauseMenuWalletDebugService.LogUnifiedLayout(
                hierarchy.Menu,
                hierarchy.Background.gameObject,
                KeybindsCardName,
                ModsCardName);

            MMLog.WriteInfo("[PauseMenuCardPatches] Pause menu wallet injected or refreshed. model=single-wallet/4-cards cardGap=" + cardGap
                + " wallet=" + FormatVector(walletPosition) + ".");
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

        private static Vector3 OffsetY(Vector3 value, float offset)
        {
            return new Vector3(value.x, value.y + offset, value.z);
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
                hierarchy.SettingsTab.localPosition = PauseMenuOriginalPosition.Capture(hierarchy.SettingsTab.gameObject).OriginalLocalPosition;
            if (hierarchy.SaveExitTab != null)
                hierarchy.SaveExitTab.localPosition = PauseMenuOriginalPosition.Capture(hierarchy.SaveExitTab.gameObject).OriginalLocalPosition;
        }

        private static void ClearLegacyInjectedCards(Transform vanillaWallet)
        {
            if (vanillaWallet == null)
                return;

            DestroyChildIfPresent(vanillaWallet, KeybindsCardName);
            DestroyChildIfPresent(vanillaWallet, ModsCardName);
        }

        private static void ClearLegacyInjectedWallet(Transform menu)
        {
            if (menu == null)
                return;

            DestroyChildIfPresent(menu, InjectedWalletName);
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

    internal static class PauseMenuWalletBuilder
    {
        public static GameObject EnsureInjectedWallet(Transform parent, GameObject templateWallet, string objectName)
        {
            if (parent == null || templateWallet == null || string.IsNullOrEmpty(objectName))
                return null;

            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                existing.gameObject.SetActive(templateWallet.activeSelf);
                return existing.gameObject;
            }

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
            DisableLocalPositionTweens(clone);
            clone.SetActive(templateWallet.activeSelf);
            return clone;
        }

        public static GameObject EnsureInjectedCard(Transform parent, GameObject templateCard, string objectName)
        {
            if (parent == null || templateCard == null || string.IsNullOrEmpty(objectName))
                return null;

            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                existing.gameObject.SetActive(templateCard.activeSelf);
                return existing.gameObject;
            }

            GameObject clone = UnityEngine.Object.Instantiate(templateCard) as GameObject;
            if (clone == null)
                return null;

            clone.name = objectName;
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = templateCard.transform.localPosition;
            clone.transform.localRotation = templateCard.transform.localRotation;
            clone.transform.localScale = templateCard.transform.localScale;
            clone.layer = templateCard.layer;
            NGUITools.SetLayer(clone, clone.layer);
            StripTemplateState(clone);
            DisableLocalPositionTweens(clone);
            clone.SetActive(templateCard.activeSelf);
            return clone;
        }

        public static void ConfigureInjectedCard(GameObject card, PauseMenuInjectedCardDefinition definition)
        {
            if (card == null || definition == null)
                return;

            card.name = definition.ObjectName;
            PauseMenuCardBuilder.ConfigureExistingCard(card, definition);
        }

        public static void ConfigureInjectedWallet(
            GameObject wallet,
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
            PauseMenuCardBuilder.ConfigureExistingCard(keybindTab, keybindsCard);
            PauseMenuCardBuilder.ConfigureExistingCard(modsTab, modsCard);
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
            PauseMenuOriginalPosition[] positions = clone.GetComponentsInChildren<PauseMenuOriginalPosition>(true);
            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i] != null)
                    UnityEngine.Object.Destroy(positions[i]);
            }

            PauseMenuOriginalTweenPosition[] tweens = clone.GetComponentsInChildren<PauseMenuOriginalTweenPosition>(true);
            for (int i = 0; i < tweens.Length; i++)
            {
                if (tweens[i] != null)
                    UnityEngine.Object.Destroy(tweens[i]);
            }

            PauseMenuOriginalWidgetDepth[] widgetDepths = clone.GetComponentsInChildren<PauseMenuOriginalWidgetDepth>(true);
            for (int i = 0; i < widgetDepths.Length; i++)
            {
                if (widgetDepths[i] != null)
                    UnityEngine.Object.Destroy(widgetDepths[i]);
            }

            PauseMenuOriginalPanelDepth[] panelDepths = clone.GetComponentsInChildren<PauseMenuOriginalPanelDepth>(true);
            for (int i = 0; i < panelDepths.Length; i++)
            {
                if (panelDepths[i] != null)
                    UnityEngine.Object.Destroy(panelDepths[i]);
            }

            PauseMenuOriginalWidgetColor[] widgetColors = clone.GetComponentsInChildren<PauseMenuOriginalWidgetColor>(true);
            for (int i = 0; i < widgetColors.Length; i++)
            {
                if (widgetColors[i] != null)
                    UnityEngine.Object.Destroy(widgetColors[i]);
            }
        }

        private static void DisableLocalPositionTweens(GameObject clone)
        {
            TweenPosition[] tweens = clone.GetComponentsInChildren<TweenPosition>(true);
            for (int i = 0; i < tweens.Length; i++)
            {
                TweenPosition tween = tweens[i];
                if (tween == null)
                    continue;

                tween.enabled = false;
            }
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
            ConfigureButtons(card, definition);

            PauseMenuButton pauseButton = card.GetComponent<PauseMenuButton>();
            if (pauseButton != null)
                pauseButton.CancelHighlight();

            NGUITools.UpdateWidgetCollider(card, true);
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
                label.color = LabelColor;
                label.effectStyle = UILabel.Effect.Outline;
                label.effectColor = LabelOutlineColor;
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
                label.alignment = NGUIText.Alignment.Center;
                label.multiLine = false;
                label.ProcessText();
                label.MarkAsChanged();
            }
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

        private static void ConfigureButtons(GameObject card, PauseMenuInjectedCardDefinition definition)
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
                PauseMenuCardActionBinder.Bind(targetButton, definition.OnClick);
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

    internal static class PauseMenuCardActionBinder
    {
        public static void StripInheritedActions(GameObject card)
        {
            if (card == null)
                return;

            UILocalize[] localizers = card.GetComponentsInChildren<UILocalize>(true);
            for (int i = 0; i < localizers.Length; i++)
            {
                if (localizers[i] == null)
                    continue;

                localizers[i].enabled = false;
                UnityEngine.Object.Destroy(localizers[i]);
            }

            UIButtonMessage[] messages = card.GetComponentsInChildren<UIButtonMessage>(true);
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i] == null)
                    continue;

                messages[i].enabled = false;
                UnityEngine.Object.Destroy(messages[i]);
            }

            UIPlayAnimation[] animations = card.GetComponentsInChildren<UIPlayAnimation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i] == null)
                    continue;

                animations[i].enabled = false;
                UnityEngine.Object.Destroy(animations[i]);
            }

            UIPlayTween[] playTweens = card.GetComponentsInChildren<UIPlayTween>(true);
            for (int i = 0; i < playTweens.Length; i++)
            {
                if (playTweens[i] == null)
                    continue;

                playTweens[i].enabled = false;
                UnityEngine.Object.Destroy(playTweens[i]);
            }

            UIKeyNavigation[] navigations = card.GetComponentsInChildren<UIKeyNavigation>(true);
            for (int i = 0; i < navigations.Length; i++)
            {
                if (navigations[i] == null)
                    continue;

                navigations[i].enabled = false;
                UnityEngine.Object.Destroy(navigations[i]);
            }

            UIEventListener[] listeners = card.GetComponentsInChildren<UIEventListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                ResetListener(listeners[i]);
            }
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

        private static void ResetListener(UIEventListener listener)
        {
            if (listener == null)
                return;

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
        }
    }

    internal static class PauseMenuCardActions
    {
        public static void OpenKeybinds()
        {
            try
            {
                MMLog.WriteDebug("[PauseMenuCardPatches] Opening keybinds from pause menu.");
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
                MMLog.WriteDebug("[PauseMenuCardPatches] Opening mod manager from pause menu.");
                ModManagerPanel.ShowPanel();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[PauseMenuCardPatches] Failed to open mod manager from pause menu: " + ex);
            }
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

    internal static class PauseMenuWalletDebugService
    {
        public static void LogUnifiedStackPlan(
            GameObject wallet,
            Vector3 originalWalletPosition,
            Vector3 walletPosition,
            Vector3 keybindsPosition,
            Vector3 modsPosition,
            Vector3 settingsPosition,
            Vector3 saveExitPosition,
            float cardStep,
            float cardGap)
        {
            float walletHeight = ResolveLargestWidgetHeight(wallet);
            MMLog.WriteInfo("[PauseMenuCardPatches] Unified wallet stack plan"
                + " originalWallet=" + FormatVector(originalWalletPosition)
                + " wallet=" + FormatVector(walletPosition)
                + " keybinds=" + FormatVector(keybindsPosition)
                + " mods=" + FormatVector(modsPosition)
                + " settings=" + FormatVector(settingsPosition)
                + " saveExit=" + FormatVector(saveExitPosition)
                + " cardStep=" + cardStep.ToString("0.0")
                + " cardGap=" + cardGap.ToString("0.0")
                + " largestWidgetHeight=" + walletHeight.ToString("0.0") + ".");
        }

        public static void LogUnifiedLayout(
            Transform menu,
            GameObject wallet,
            string keybindsCardName,
            string modsCardName)
        {
            bool previous = UIDebug.Enabled;
            UIDebug.Enabled = true;
            try
            {
                if (menu != null)
                    UIDebug.LogWidgetHierarchy(menu, 2);

                LogWallet("Unified", wallet);
                LogCard("Keybinds", wallet, keybindsCardName);
                LogCard("Mods", wallet, modsCardName);
                LogCard("Settings", wallet, "tab1");
                LogCard("SaveExit", wallet, "tab2");
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

        public static void LogStackPlan(
            GameObject templateWallet,
            Vector3 originalWalletPosition,
            Vector3 injectedWalletPosition,
            Vector3 vanillaWalletPosition,
            float cardStep,
            float walletGap)
        {
            float walletHeight = ResolveLargestWidgetHeight(templateWallet);
            float xDelta = Mathf.Abs(injectedWalletPosition.x - vanillaWalletPosition.x);
            float yDelta = Mathf.Abs(injectedWalletPosition.y - vanillaWalletPosition.y);
            MMLog.WriteInfo("[PauseMenuCardPatches] Wallet stack plan"
                + " original=" + FormatVector(originalWalletPosition)
                + " injected=" + FormatVector(injectedWalletPosition)
                + " vanilla=" + FormatVector(vanillaWalletPosition)
                + " cardStep=" + cardStep.ToString("0.0")
                + " walletGap=" + walletGap.ToString("0.0")
                + " yDelta=" + yDelta.ToString("0.0")
                + " xDelta=" + xDelta.ToString("0.0")
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
            int effectiveDepth = widget != null ? UIDebug.GetEffectiveDepth(widget) : 0;

            MMLog.WriteInfo("[PauseMenuCardPatches] " + label
                + " card local=" + FormatVector(card.transform.localPosition)
                + " layer=" + card.layer
                + " widgetDepth=" + (widget != null ? widget.depth.ToString() : "<none>")
                + " effectiveDepth=" + effectiveDepth
                + " buttonDelegates=" + (button != null && button.onClick != null ? button.onClick.Count.ToString() : "<none>")
                + ".");

            LogWidgetChildren(label, card);
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

        private static void ApplyLocalPosition(GameObject wallet, Vector3 targetLocalPosition)
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
