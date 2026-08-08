using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Scenarios.Infrastructure.Harmony;
using ShelteredAPI.Scenarios.Presentation.Selection;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    /// <summary>
    /// Adds one runtime scenario-library entry point to the vanilla scenario panel.
    /// Installed scenarios and scenario saves are browsed exclusively by the book.
    /// </summary>
    internal sealed class ShelteredScenarioSelectionBrowserController
    {
        private const string HubLabel = "Custom Scenarios";
        private static readonly Color HubButtonColor = new Color(0.88f, 0.76f, 0.63f, 1f);
        private static readonly Color HubHoverColor = new Color(0.97f, 0.85f, 0.70f, 1f);
        private static readonly Color HubPressedColor = new Color(0.74f, 0.61f, 0.49f, 1f);
        private static readonly Color HubDisabledColor = new Color(0.52f, 0.45f, 0.39f, 0.95f);
        private static readonly Color HubLabelColor = new Color(0.18f, 0.13f, 0.09f, 1f);

        private sealed class BrowserPanelState
        {
            public int VanillaButtonCount;
            public UIButton HubButton;
        }

        private static readonly ShelteredScenarioSelectionBrowserController _instance =
            new ShelteredScenarioSelectionBrowserController();
        private readonly Dictionary<int, BrowserPanelState> _states =
            new Dictionary<int, BrowserPanelState>();

        public static ShelteredScenarioSelectionBrowserController Instance
        {
            get { return _instance; }
        }

        private ShelteredScenarioSelectionBrowserController()
        {
        }

        public void Initialize(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            if (panel == null || scenarioButtons == null || scenarioButtons.Count == 0)
                return;

            int panelId = panel.GetInstanceID();
            BrowserPanelState existing;
            if (_states.TryGetValue(panelId, out existing)
                && existing != null
                && existing.HubButton != null)
                return;

            UIButton source = scenarioButtons[scenarioButtons.Count - 1];
            if (source == null || source.gameObject == null)
                return;

            try
            {
                UIButton hub = CloneHubButton(source);
                if (hub == null)
                    return;

                BrowserPanelState state = new BrowserPanelState
                {
                    VanillaButtonCount = scenarioButtons.Count,
                    HubButton = hub
                };
                _states[panelId] = state;

                PositionHub(source, hub);
                ConfigureHubButton(hub.gameObject);
                BindPressGuard(panel, hub.gameObject);
                UIEventListener.Get(hub.gameObject).onClick = delegate(GameObject ignored)
                {
                    ExecuteGuardedUiClick(panel, delegate
                    {
                        MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Opening the scenario library. panel="
                            + panel.GetInstanceID() + ".");
                        ScenarioBookBrowserPanel.Show(panel);
                    });
                };

                scenarioButtons.Add(hub);
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Added the scenario-library hub. panel="
                    + panelId + " vanillaButtons=" + state.VanillaButtonCount + ".");
            }
            catch (Exception ex)
            {
                _states.Remove(panelId);
                MMLog.WriteWarning("[ShelteredCustomScenarioSelection] Could not add the scenario-library hub: " + ex.Message);
            }
        }

        public bool HandleScenarioSelected(
            ScenarioSelectionPanel panel,
            int selectedScenario,
            UILabel scenarioNameLabel,
            UILabel scenarioDescLabel,
            UILabel scenarioHighScore,
            GameObject stasisScoreLabelsRoot)
        {
            BrowserPanelState state = GetState(panel);
            if (state == null || selectedScenario != state.VanillaButtonCount)
            {
                SlotPagingScopeResolver.RememberScenarioSelection(
                    panel != null ? panel.selectionPanel : null,
                    selectedScenario);
                return true;
            }

            SetLabel(scenarioNameLabel, HubLabel);
            SetLabel(scenarioDescLabel,
                "Browse installed custom scenarios and their saves. Unlimited vanilla runs are kept in separate archives.");
            SetLabel(scenarioHighScore, string.Empty);
            if (stasisScoreLabelsRoot != null)
                stasisScoreLabelsRoot.SetActive(false);
            return false;
        }

        public bool HandleScenarioChosen(
            ScenarioSelectionPanel panel,
            int selectedScenario,
            List<UIButton> scenarioButtons)
        {
            BrowserPanelState state = GetState(panel);
            if (state != null && selectedScenario == state.VanillaButtonCount)
            {
                ScenarioBookBrowserPanel.Show(panel);
                return false;
            }

            SlotPagingScopeResolver.RememberScenarioSelection(
                panel != null ? panel.selectionPanel : null,
                selectedScenario);
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
            return true;
        }

        public bool HandleCancel(ScenarioSelectionPanel panel, List<UIButton> scenarioButtons)
        {
            if (ScenarioBookBrowserPanel.TryHandleCancel())
                return false;

            SlotPagingScopeResolver.ForgetScenarioSelection(panel != null ? panel.selectionPanel : null);
            return true;
        }

        public void Cleanup(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            ScenarioBookBrowserPanel.NotifyUnderlyingPanelTeardown(panel);
            BrowserPanelState state;
            if (_states.TryGetValue(panel.GetInstanceID(), out state))
            {
                if (state != null && state.HubButton != null && state.HubButton.gameObject != null)
                    UnityEngine.Object.Destroy(state.HubButton.gameObject);
                _states.Remove(panel.GetInstanceID());
            }

            UIFlowGuard.BlockSlotClicksToggle(false);
            UIUtil.ClearClickBlockers();
        }

        private BrowserPanelState GetState(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return null;
            BrowserPanelState state;
            _states.TryGetValue(panel.GetInstanceID(), out state);
            return state;
        }

        private static UIButton CloneHubButton(UIButton source)
        {
            Transform parent = source.transform.parent;
            UIButton button = UIUtil.CloneButton(source, parent, string.Empty);
            if (button == null || button.gameObject == null)
                return null;

            button.gameObject.name = "ShelteredAPI_CustomScenarios_HubButton";
            button.gameObject.SetActive(true);
            if (button.onClick != null)
                button.onClick.Clear();

            UIButtonMessage[] messages = button.gameObject.GetComponentsInChildren<UIButtonMessage>(true);
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i] != null)
                    messages[i].enabled = false;
            }

            UIEventListener listener = button.gameObject.GetComponent<UIEventListener>();
            if (listener != null)
            {
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
            }
            return button;
        }

        private static void PositionHub(UIButton source, UIButton hub)
        {
            Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(source.transform, true);
            float width = bounds.size.x > 1f ? bounds.size.x : 320f;
            float offsetX = Mathf.Clamp(width + 360f, 620f, 720f);
            Vector3 sourcePosition = source.transform.localPosition;
            hub.transform.localPosition = new Vector3(
                Mathf.Round(sourcePosition.x + offsetX),
                Mathf.Round(sourcePosition.y),
                Mathf.Round(sourcePosition.z));
        }

        private static void ConfigureHubButton(GameObject root)
        {
            UIButton button = root != null ? root.GetComponent<UIButton>() : null;
            if (button != null)
            {
                button.isEnabled = true;
                button.defaultColor = HubButtonColor;
                button.hover = HubHoverColor;
                button.pressed = HubPressedColor;
                button.disabledColor = HubDisabledColor;
                button.SetState(UIButtonColor.State.Normal, true);
            }

            UILabel[] labels = root != null ? root.GetComponentsInChildren<UILabel>(true) : new UILabel[0];
            UILabel primary = null;
            int bestWidth = int.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label != null && label.width > bestWidth)
                {
                    primary = label;
                    bestWidth = label.width;
                }
            }

            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;
                label.enabled = label == primary;
                label.text = label == primary ? HubLabel : string.Empty;
                if (label == primary)
                {
                    label.color = HubLabelColor;
                    label.alignment = NGUIText.Alignment.Center;
                    label.overflowMethod = UILabel.Overflow.ShrinkContent;
                    label.ProcessText();
                    label.MarkAsChanged();
                }
            }

            if (root != null)
                NGUITools.UpdateWidgetCollider(root, true);
        }

        private static void BindPressGuard(ScenarioSelectionPanel panel, GameObject buttonObject)
        {
            UIEventListener.Get(buttonObject).onPress = delegate(GameObject ignored, bool pressed)
            {
                if (pressed)
                {
                    UIFlowGuard.BlockSlotClicksToggle(true);
                    ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
                }
                else if (panel != null)
                {
                    panel.StartCoroutine(ReleaseFlowGuardNextFrame());
                }
            };
        }

        private static void ExecuteGuardedUiClick(ScenarioSelectionPanel panel, Action action)
        {
            ShelteredCustomScenarioRuntimeState.BlockSlotClicksBriefly();
            UIFlowGuard.BlockSlotClicksOnce(panel);
            if (panel != null)
            {
                UIUtil.PushClickBlocker(panel.transform, 99999);
                panel.StartCoroutine(ReleaseFlowGuardNextFrame());
            }
            if (action != null)
                action();
        }

        private static IEnumerator ReleaseFlowGuardNextFrame()
        {
            yield return null;
            UIFlowGuard.BlockSlotClicksToggle(false);
            UIUtil.PopClickBlocker();
        }

        private static void SetLabel(UILabel label, string text)
        {
            if (label == null)
                return;
            label.text = text ?? string.Empty;
            label.ProcessText();
            label.MarkAsChanged();
        }
    }
}
