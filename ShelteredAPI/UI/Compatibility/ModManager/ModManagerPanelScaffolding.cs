using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    internal static class ModManagerPanelScaffolding
    {
        internal static bool TryCloneBookVisuals(ModManagerPanel panel)
        {
            return panel != null && TryCloneScenarioBookVisuals(panel.gameObject, 10005);
        }

        internal static bool TryCloneScenarioBookVisuals(GameObject parent, int visualDepth)
        {
            if (parent == null)
                return false;

            try
            {
                BasePanel scenarioPanel = FindScenarioPanel();
                if (scenarioPanel == null)
                    return false;

                MMLog.WriteDebug("Loading scenario book visuals...");

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
                    clone.transform.parent = parent.transform;
                    clone.name = "Cloned_" + child.name;
                    clone.transform.localPosition = child.localPosition;
                    clone.transform.localScale = child.localScale;
                    clone.transform.localRotation = child.localRotation;
                    clone.layer = parent.layer;
                    NGUITools.SetLayer(clone, parent.layer);

                    StripClonedScenarioBehaviors(clone);

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
                        widget.gameObject.layer = parent.layer;
                        widget.depth = visualDepth;
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

        private static void StripClonedScenarioBehaviors(GameObject clone)
        {
            if (clone == null)
                return;

            foreach (UILocalize localize in clone.GetComponentsInChildren<UILocalize>(true))
                Object.Destroy(localize);
            foreach (UIButtonMessage message in clone.GetComponentsInChildren<UIButtonMessage>(true))
                Object.Destroy(message);
            foreach (UIPlaySound sound in clone.GetComponentsInChildren<UIPlaySound>(true))
                Object.Destroy(sound);
            foreach (UI_PlaySound sound in clone.GetComponentsInChildren<UI_PlaySound>(true))
                Object.Destroy(sound);
            foreach (UIPlayTween tween in clone.GetComponentsInChildren<UIPlayTween>(true))
                Object.Destroy(tween);
            foreach (UIPlayAnimation animation in clone.GetComponentsInChildren<UIPlayAnimation>(true))
                Object.Destroy(animation);
            foreach (UIKeyNavigation navigation in clone.GetComponentsInChildren<UIKeyNavigation>(true))
                Object.Destroy(navigation);

            UIEventListener[] listeners = clone.GetComponentsInChildren<UIEventListener>(true);
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
                Object.Destroy(listener);
            }

            Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                colliders[i].enabled = false;
                Object.Destroy(colliders[i]);
            }

            Collider2D[] colliders2D = clone.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
            {
                if (colliders2D[i] == null)
                    continue;

                colliders2D[i].enabled = false;
                Object.Destroy(colliders2D[i]);
            }

            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is UIWidget || behaviour is UIPanel)
                    continue;

                behaviour.enabled = false;
                Object.Destroy(behaviour);
            }
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
