using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Thin Traverse-based wrapper for the private NGUI fields on
    /// <see cref="ScenarioSelectionPanel"/>. Centralises every reflective access so
    /// the rest of the browser stack only deals with strongly-typed members.
    /// </summary>
    internal sealed class ScenarioBrowserPanelAdapter
    {
        private readonly ScenarioSelectionPanel _panel;
        private readonly Traverse _traverse;

        public ScenarioBrowserPanelAdapter(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                throw new ArgumentNullException("panel");

            _panel = panel;
            _traverse = Traverse.Create(panel);
        }

        public ScenarioSelectionPanel Panel
        {
            get { return _panel; }
        }

        public int InstanceId
        {
            get { return _panel.GetInstanceID(); }
        }

        public int GetSelectedScenario()
        {
            return ReadInt("m_selectedScenario", -1);
        }

        public void SetSelectedScenario(int index)
        {
            try { _traverse.Field("m_selectedScenario").SetValue(index); }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBrowserPanelAdapter] SetSelectedScenario failed: " + ex.Message);
            }
        }

        public List<UIButton> GetScenarioButtons()
        {
            try { return _traverse.Field("m_scenarioButtons").GetValue<List<UIButton>>(); }
            catch { return null; }
        }

        public UILabel GetScenarioNameLabel()
        {
            return ReadField<UILabel>("m_scenarioNameLabel");
        }

        public UILabel GetScenarioDescLabel()
        {
            return ReadField<UILabel>("m_scenarioDescLabel");
        }

        public UILabel GetScenarioHighScoreLabel()
        {
            return ReadField<UILabel>("m_scenarioHighScore");
        }

        public GameObject GetStasisScoreLabelsRoot()
        {
            return ReadField<GameObject>("m_stasis_scoreLabelsRoot");
        }

        public SlotSelectionPanel GetSlotSelectionPanel()
        {
            try { return _panel.selectionPanel; }
            catch { return null; }
        }

        public BasePanel GetCustomizationPanel()
        {
            SlotSelectionPanel slotPanel = GetSlotSelectionPanel();
            if (slotPanel == null)
                return null;

            try { return Traverse.Create(slotPanel).Field("m_customizationPanel").GetValue<BasePanel>(); }
            catch { return null; }
        }

        public bool GetInputEnabled()
        {
            return ReadBool("m_inputEnabled", true);
        }

        public bool SetInputEnabled(bool enabled)
        {
            try
            {
                _traverse.Field("m_inputEnabled").SetValue(enabled);
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBrowserPanelAdapter] SetInputEnabled failed: " + ex.Message);
                return false;
            }
        }

        public void MarkSlotInfoNeedsRefresh()
        {
            SlotSelectionPanel slotPanel = GetSlotSelectionPanel();
            if (slotPanel == null)
                return;

            try { Traverse.Create(slotPanel).Field("m_infoNeedsRefresh").SetValue(true); }
            catch { }
        }

        private T ReadField<T>(string name) where T : class
        {
            try { return _traverse.Field(name).GetValue<T>(); }
            catch { return null; }
        }

        private int ReadInt(string name, int fallback)
        {
            try { return _traverse.Field(name).GetValue<int>(); }
            catch { return fallback; }
        }

        private bool ReadBool(string name, bool fallback)
        {
            try { return _traverse.Field(name).GetValue<bool>(); }
            catch { return fallback; }
        }
    }
}
