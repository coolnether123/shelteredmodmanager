using System.Reflection;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal static class ModManagerPanelScaffolding
    {
        internal static bool TryCloneBookVisuals(ModManagerPanel panel)
        {
            if (panel == null)
                return false;

            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel == null)
                    return false;

                MMLog.WriteDebug("Loading Mod Manager UI...");

                foreach (Transform child in scenarioPanel.transform)
                {
                    if (child.GetComponent<UIPanel>() != null)
                        continue;
                    if (child.GetComponent<UIButton>() != null)
                        continue;

                    string name = child.name.ToLower();
                    bool isVisual = name.Contains("background")
                        || name.Contains("book")
                        || name.Contains("visual")
                        || name.Contains("root")
                        || name.Contains("tween");

                    if (!isVisual)
                        continue;

                    GameObject clone = (GameObject)Object.Instantiate(child.gameObject);
                    clone.transform.parent = panel.transform;
                    clone.name = "Cloned_" + child.name;
                    clone.transform.localPosition = child.localPosition;
                    clone.transform.localScale = child.localScale;
                    clone.transform.localRotation = child.localRotation;
                    clone.layer = panel.gameObject.layer;

                    UIButton[] buttons = clone.GetComponentsInChildren<UIButton>(true);
                    for (int i = 0; i < buttons.Length; i++)
                        Object.Destroy(buttons[i].gameObject);

                    UILabel[] labels = clone.GetComponentsInChildren<UILabel>(true);
                    for (int i = 0; i < labels.Length; i++)
                        Object.Destroy(labels[i].gameObject);

                    Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < colliders.Length; i++)
                        Object.Destroy(colliders[i]);

                    UIWidget[] widgets = clone.GetComponentsInChildren<UIWidget>(true);
                    for (int i = 0; i < widgets.Length; i++)
                    {
                        UIWidget widget = widgets[i];
                        widget.gameObject.layer = panel.gameObject.layer;
                        widget.depth = 10005;
                    }

                    clone.SetActive(true);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                MMLog.WriteError("[ModManagerPanel] Book clone error: " + ex.Message);
            }

            return false;
        }

        internal static void CreateClickBlocker(Transform parent, int layer)
        {
            GameObject blocker = new GameObject("ClickBlocker");
            blocker.transform.parent = parent;
            blocker.transform.localPosition = Vector3.zero;
            blocker.layer = layer;

            UISprite sprite = blocker.AddComponent<UISprite>();
            sprite.color = new Color(0f, 0f, 0f, 0.75f);
            sprite.width = 10000;
            sprite.height = 10000;
            sprite.depth = 9999;

            BoxCollider col = blocker.AddComponent<BoxCollider>();
            col.size = new Vector3(10000, 10000, 1);

            UIEventListener.Get(blocker).onClick = delegate { };
        }

        internal static UIButton FindScenarioButtonTemplate()
        {
            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel != null)
                {
                    UIButton[] buttons = scenarioPanel.GetComponentsInChildren<UIButton>(true);
                    if (buttons != null && buttons.Length > 0)
                    {
                        for (int i = 0; i < buttons.Length; i++)
                        {
                            UIButton btn = buttons[i];
                            string name = btn.name.ToLower();
                            if (!name.Contains("back") && !name.Contains("cancel"))
                                return btn;
                        }

                        return buttons[0];
                    }
                }
            }
            catch (System.Exception ex)
            {
                MMLog.WriteError("[ModManagerPanel] Error finding button template: " + ex.Message);
            }

            return UIUtil.FindAnyButtonTemplate();
        }

        internal static BasePanel FindScenarioPanel()
        {
            FrontEndController fe = FrontEndController.instance;
            if (fe != null && fe.mainMenu != null)
            {
                MainMenu mm = fe.mainMenu as MainMenu;
                FieldInfo modeField = typeof(MainMenu).GetField("m_gameModeSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                GameModeSelectionPanel modePanel = modeField != null ? modeField.GetValue(mm) as GameModeSelectionPanel : null;
                if (modePanel != null)
                {
                    FieldInfo scenarioField = typeof(GameModeSelectionPanel).GetField("m_scenarioSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                    BasePanel panel = scenarioField != null ? scenarioField.GetValue(modePanel) as BasePanel : null;
                    if (panel != null)
                        return panel;
                }
            }

            BasePanel[] allPanels = Resources.FindObjectsOfTypeAll<BasePanel>();
            for (int i = 0; i < allPanels.Length; i++)
            {
                BasePanel panel = allPanels[i];
                if (panel.name.Contains("ScenarioSelectionPanel") || panel.GetType().Name.Contains("ScenarioSelection"))
                    return panel;
            }

            return null;
        }
    }
}
