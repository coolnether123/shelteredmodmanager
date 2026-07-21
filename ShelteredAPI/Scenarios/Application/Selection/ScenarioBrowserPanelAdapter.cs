using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Application.Selection{
    /// <summary>
    /// Thin Traverse-based wrapper for the private NGUI fields on
    /// <see cref="ScenarioSelectionPanel"/>. Centralises every reflective access so
    /// the rest of the browser stack only deals with strongly-typed members.
    /// </summary>
    internal sealed class ScenarioBrowserPanelAdapter
    {
        private readonly ScenarioSelectionPanel _panel;
        private readonly Traverse _traverse;

        internal sealed class ScenarioBrowserSuppressionHandle
        {
            private readonly List<SuppressedObject> _objects = new List<SuppressedObject>();
            private bool _restored;

            public void Add(GameObject gameObject)
            {
                if (gameObject == null || Contains(gameObject))
                    return;

                _objects.Add(new SuppressedObject(gameObject));
                gameObject.SetActive(false);
            }

            public void Restore()
            {
                if (_restored)
                    return;

                _restored = true;
                for (int i = _objects.Count - 1; i >= 0; i--)
                {
                    SuppressedObject item = _objects[i];
                    if (item != null && item.GameObject != null)
                        item.GameObject.SetActive(item.WasActive);
                }

                _objects.Clear();
            }

            private bool Contains(GameObject gameObject)
            {
                for (int i = 0; i < _objects.Count; i++)
                {
                    if (_objects[i] != null && _objects[i].GameObject == gameObject)
                        return true;
                }

                return false;
            }

            private sealed class SuppressedObject
            {
                public readonly GameObject GameObject;
                public readonly bool WasActive;

                public SuppressedObject(GameObject gameObject)
                {
                    GameObject = gameObject;
                    WasActive = gameObject != null && gameObject.activeSelf;
                }
            }
        }

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

        public ScenarioBrowserSuppressionHandle SuppressUnderlyingChrome()
        {
            ScenarioBrowserSuppressionHandle handle = new ScenarioBrowserSuppressionHandle();

            SlotSelectionPanel slotPanel = GetSlotSelectionPanel();
            if (slotPanel != null)
                handle.Add(slotPanel.gameObject);

            AddLabel(handle, GetScenarioNameLabel());
            AddLabel(handle, GetScenarioDescLabel());
            AddLabel(handle, GetScenarioHighScoreLabel());
            handle.Add(GetStasisScoreLabelsRoot());

            List<UIButton> buttons = GetScenarioButtons();
            for (int i = 0; buttons != null && i < buttons.Count; i++)
            {
                UIButton button = buttons[i];
                if (button != null)
                    handle.Add(button.gameObject);
            }

            AddPanelChromeChildren(handle);
            return handle;
        }

        public bool SetSelectedSlot(int slotIndex)
        {
            SlotSelectionPanel slotPanel = GetSlotSelectionPanel();
            if (slotPanel == null)
                return false;

            try
            {
                Traverse slotTraverse = Traverse.Create(slotPanel);
                slotTraverse.Field("m_selectedSlot").SetValue(slotIndex);
                slotTraverse.Field("m_inputEnabled").SetValue(true);
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBrowserPanelAdapter] SetSelectedSlot failed: " + ex.Message);
                return false;
            }
        }

        public bool ChooseSelectedSlot()
        {
            SlotSelectionPanel slotPanel = GetSlotSelectionPanel();
            if (slotPanel == null)
                return false;

            try
            {
                slotPanel.OnSlotChosen();
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBrowserPanelAdapter] ChooseSelectedSlot failed: " + ex.Message);
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

        private static void AddLabel(ScenarioBrowserSuppressionHandle handle, UILabel label)
        {
            if (handle != null && label != null)
                handle.Add(label.gameObject);
        }

        private void AddPanelChromeChildren(ScenarioBrowserSuppressionHandle handle)
        {
            if (handle == null || _panel == null)
                return;

            Transform root = _panel.transform;
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.gameObject != null)
                    handle.Add(child.gameObject);
            }
        }
    }
}
