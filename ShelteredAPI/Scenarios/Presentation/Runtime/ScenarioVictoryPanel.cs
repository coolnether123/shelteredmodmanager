using System;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.FieldManual.Primitives;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Runtime
{
    /// <summary>
    /// Success presentation for authored scenarios whose base mode has no native
    /// success panel. UIPanelManager owns pause and input state; this component owns
    /// only Field Manual composition and the vanilla-compatible Continue transition.
    /// </summary>
    internal sealed class ScenarioVictoryPanel : BasePanel
    {
        private const string OverlayName = "ShelteredAPI_ScenarioVictoryPanel";
        private const int OverlayDepth = 52000;
        private static ScenarioVictoryPanel _active;

        private FieldManualWindowChrome _chrome;
        private bool _continued;

        internal static bool HasActivePanel { get { return _active != null; } }

        internal static void ResetForNewRun()
        {
            ScenarioVictoryPanel panel = _active;
            _active = null;
            if (panel == null)
                return;

            UIPanelManager manager = UIPanelManager.Instance();
            if (manager != null && manager.IsPanelOnStack(panel))
                manager.PopPanel(panel);
            else
                UnityEngine.Object.Destroy(panel.gameObject);
            if (panel != null && panel.gameObject != null)
                panel.gameObject.SetActive(false);
        }

        internal static bool TryShow(ScenarioEndGamePresentation presentation, out string reason)
        {
            reason = null;
            if (presentation == null)
            {
                reason = "Scenario victory details were not supplied.";
                return false;
            }
            if (_active != null)
                return true;
            if (UIPanelManager.Instance() == null)
            {
                reason = "UIPanelManager is not ready for the scenario victory panel.";
                return false;
            }

            try
            {
                GameObject contentRoot = FieldManualWindowChrome.CreateOverlayRoot(
                    OverlayName,
                    OverlayDepth,
                    "ScenarioVictoryPanel_Content");
                if (contentRoot == null || contentRoot.transform.parent == null)
                {
                    reason = "The Field Manual overlay root could not be created.";
                    return false;
                }

                GameObject panelRoot = contentRoot.transform.parent.gameObject;
                ScenarioVictoryPanel panel = panelRoot.GetComponent<ScenarioVictoryPanel>();
                if (panel == null)
                    panel = panelRoot.AddComponent<ScenarioVictoryPanel>();
                panel.Build(contentRoot, presentation);
                panelRoot.SetActive(false);
                _active = panel;
                UIPanelManager.Instance().PushPanel(panel);
                return true;
            }
            catch (Exception ex)
            {
                reason = "Scenario victory panel could not be created: " + ex.Message;
                return false;
            }
        }

        private void Build(GameObject root, ScenarioEndGamePresentation presentation)
        {
            if (_chrome != null)
                _chrome.Dispose();

            _continued = false;
            string displayName = string.IsNullOrEmpty(presentation.ScenarioDisplayName)
                ? "Custom Scenario"
                : presentation.ScenarioDisplayName;
            _chrome = FieldManualWindowChrome.BuildBook(root, OverlayDepth, "Scenario Complete", displayName);
            UIPrimitiveFactory ui = _chrome.Ui;

            ui.CreateLabel(_chrome.Regions.ContentRoot, "VictoryStamp", "VICTORY",
                new Vector3(-520f, 205f, 0f), 15, _chrome.Palette.StampRed,
                440, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());

            UILabel scenarioName = ui.CreateLabel(_chrome.Regions.ContentRoot, "ScenarioName", displayName,
                new Vector3(-520f, 135f, 0f), 31, _chrome.Palette.Ink,
                440, 94, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());
            scenarioName.multiLine = true;
            scenarioName.overflowMethod = UILabel.Overflow.ShrinkContent;

            ui.CreateLabel(_chrome.Regions.ContentRoot, "DaysCaption", "DAYS SURVIVED",
                new Vector3(-520f, 15f, 0f), 14, _chrome.Palette.InkFaded,
                440, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());
            ui.CreateLabel(_chrome.Regions.ContentRoot, "DaysValue", Math.Max(0, presentation.DaysSurvived).ToString(),
                new Vector3(-520f, -62f, 0f), 58, _chrome.Palette.Ink,
                240, 78, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());

            ui.CreateLabel(_chrome.Regions.ContentRoot, "ConditionCaption", "FULFILLED VICTORY CONDITION",
                new Vector3(82f, 205f, 0f), 14, _chrome.Palette.StampRed,
                430, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());
            UILabel condition = ui.CreateLabel(_chrome.Regions.ContentRoot, "ConditionText",
                string.IsNullOrEmpty(presentation.FulfilledConditionText)
                    ? "The authored victory condition was fulfilled."
                    : presentation.FulfilledConditionText,
                new Vector3(82f, 110f, 0f), 25, _chrome.Palette.Ink,
                430, 150, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());
            condition.multiLine = true;
            condition.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel note = ui.CreateLabel(_chrome.Regions.ContentRoot, "VictoryNote",
                "Your scenario run is complete. Continue to return to the main menu.",
                new Vector3(82f, -82f, 0f), 16, _chrome.Palette.InkFaded,
                430, 76, NGUIText.Alignment.Left, UIWidget.Pivot.Left, ui.NextDepth());
            note.multiLine = true;
            note.overflowMethod = UILabel.Overflow.ShrinkContent;

            _chrome.Buttons.Build(_chrome.Regions.FooterRoot, "ScenarioVictoryContinue", "Continue",
                new Vector3(0f, -400f, 0f), 220, 58, 23, ContinueToMenu);
        }

        public override bool AlwaysShow() { return false; }
        public override bool DestroyOnClose() { return true; }
        public override bool PausesGameInput() { return true; }
        public override bool PausesGameTime() { return true; }
        public override void OnSelect() { ContinueToMenu(); }
        public override void OnCancel() { ContinueToMenu(); }
        public override void OnExtra1() { ContinueToMenu(); }
        public override void OnExtra2() { ContinueToMenu(); }

        private void ContinueToMenu()
        {
            if (_continued)
                return;

            _continued = true;
            TooltipperObj.ShowTooltip(null);
            ResetForNewRun();
            if (SaveManager.instance != null)
                SaveManager.instance.DeleteCurrentSlot();
            if (LoadingScreen.Instance != null)
                LoadingScreen.Instance.ShowLoadingScreen("MenuScene");
        }

        public override void OnClose()
        {
            base.OnClose();
            DisposeChrome();
        }

        private void OnDestroy()
        {
            DisposeChrome();
            if (_active == this)
                _active = null;
        }

        private void DisposeChrome()
        {
            if (_chrome == null)
                return;
            _chrome.Dispose();
            _chrome = null;
        }
    }
}
