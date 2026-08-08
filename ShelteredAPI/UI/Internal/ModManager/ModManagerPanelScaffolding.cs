using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;
namespace ShelteredAPI.UI.Internal.ModManager{
    internal static class ModManagerPanelScaffolding
    {
        private const string ScenarioBookTemplateRootName = "ShelteredAPI_ScenarioBookVisualCache";

        private static GameObject _scenarioBookTemplateRoot;
        private static GameObject _scenarioBookVisualTemplate;

        internal static bool TryCloneBookVisuals(ModManagerPanel panel)
        {
            return panel != null && TryCloneScenarioBookVisuals(panel.gameObject, 10005);
        }

        internal static void WarmScenarioBookVisualCache()
        {
            if (_scenarioBookVisualTemplate != null)
                return;

            try
            {
                GameObject visualSource = FindScenarioBookVisualSource();
                if (visualSource == null)
                {
                    MMLog.WriteInfo("[ScenarioBookVisuals] Cache warm-up skipped; live scenario book source was not available yet.");
                    return;
                }

                CacheScenarioBookVisualTemplate(visualSource, "main-menu warm-up");
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookVisuals] Cache warm-up failed: " + ex.Message);
            }
        }

        internal static bool TryCloneScenarioBookVisuals(GameObject parent, int visualDepth)
        {
            int maxVisualDepth;
            return TryCloneScenarioBookVisuals(parent, visualDepth, out maxVisualDepth);
        }

        internal static bool TryCloneScenarioBookVisuals(GameObject parent, int visualDepth, out int maxVisualDepth)
        {
            maxVisualDepth = visualDepth;
            if (parent == null)
                return false;

            try
            {
                GameObject visualSource = FindScenarioBookVisualSource();
                if (visualSource == null)
                {
                    if (_scenarioBookVisualTemplate == null)
                    {
                        MMLog.WriteWarning("[ScenarioBookVisuals] Live scenario book source unavailable and cache is empty; procedural book fallback will be used. parent="
                            + GetObjectPath(parent) + ".");
                    }
                    else
                    {
                        MMLog.WriteInfo("[ScenarioBookVisuals] Live scenario book source unavailable; trying cached vanilla book template. parent="
                            + GetObjectPath(parent) + ".");
                    }

                    return CloneCachedScenarioBookVisuals(parent, visualDepth, out maxVisualDepth);
                }

                MMLog.WriteDebug("[ScenarioBookVisuals] Live scenario book source found. source="
                    + GetObjectPath(visualSource) + " parent=" + GetObjectPath(parent) + ".");
                CacheScenarioBookVisualTemplate(visualSource, "live clone request");
                return CloneScenarioBookVisual(parent, visualSource, visualDepth, out maxVisualDepth);
            }
            catch (System.Exception ex)
            {
                MMLog.WriteError("[ScenarioBookVisuals] Book clone error: " + ex.Message);
            }

            return CloneCachedScenarioBookVisuals(parent, visualDepth, out maxVisualDepth);
        }

        private static GameObject FindScenarioBookVisualSource()
        {
            BasePanel scenarioPanel = FindScenarioPanel();
            if (scenarioPanel == null)
                return null;

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

                if (isVisual)
                    return child.gameObject;
            }

            return null;
        }

        private static bool CloneScenarioBookVisual(GameObject parent, GameObject source, int visualDepth, out int maxVisualDepth)
        {
            maxVisualDepth = visualDepth;
            if (parent == null || source == null)
                return false;

            GameObject clone = (GameObject)Object.Instantiate(source);
            clone.transform.parent = parent.transform;
            clone.name = "Cloned_" + source.name;
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localScale = source.transform.localScale;
            clone.transform.localRotation = source.transform.localRotation;

            StripClonedScenarioBehaviors(clone);
            maxVisualDepth = ApplyClonedScenarioBookPresentation(clone, parent.layer, visualDepth);
            clone.SetActive(true);
            MMLog.WriteDebug("[ScenarioBookVisuals] Activated live vanilla scenario book visuals. parent="
                + GetObjectPath(parent)
                + " source=" + GetObjectPath(source)
                + " clone=" + GetObjectPath(clone)
                + " visualDepth=" + visualDepth
                + " maxVisualDepth=" + maxVisualDepth
                + " widgets=" + CountWidgets(clone) + ".");
            return true;
        }

        private static bool CloneCachedScenarioBookVisuals(GameObject parent, int visualDepth, out int maxVisualDepth)
        {
            maxVisualDepth = visualDepth;
            if (parent == null || _scenarioBookVisualTemplate == null)
            {
                MMLog.WriteWarning("[ScenarioBookVisuals] Cached vanilla book clone unavailable. parent="
                    + GetObjectPath(parent)
                    + " hasCache=" + (_scenarioBookVisualTemplate != null) + ".");
                return false;
            }

            try
            {
                GameObject clone = (GameObject)Object.Instantiate(_scenarioBookVisualTemplate);
                clone.transform.parent = parent.transform;
                clone.name = "Cloned_" + _scenarioBookVisualTemplate.name;
                clone.transform.localPosition = _scenarioBookVisualTemplate.transform.localPosition;
                clone.transform.localScale = _scenarioBookVisualTemplate.transform.localScale;
                clone.transform.localRotation = _scenarioBookVisualTemplate.transform.localRotation;

                maxVisualDepth = ApplyClonedScenarioBookPresentation(clone, parent.layer, visualDepth);
                clone.SetActive(true);
                MMLog.WriteDebug("[ScenarioBookVisuals] Activated cached vanilla scenario book visuals. parent="
                    + GetObjectPath(parent)
                    + " template=" + GetObjectPath(_scenarioBookVisualTemplate)
                    + " clone=" + GetObjectPath(clone)
                    + " visualDepth=" + visualDepth
                    + " maxVisualDepth=" + maxVisualDepth
                    + " widgets=" + CountWidgets(clone) + ".");
                return true;
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookVisuals] Cached scenario book clone failed: " + ex.Message);
            }

            return false;
        }

        private static void CacheScenarioBookVisualTemplate(GameObject source, string reason)
        {
            if (source == null || _scenarioBookVisualTemplate != null)
                return;

            try
            {
                GameObject root = GetScenarioBookTemplateRoot();
                if (root == null)
                    return;

                GameObject template = (GameObject)Object.Instantiate(source);
                template.name = "ScenarioBookVisualTemplate_" + source.name;
                template.transform.parent = root.transform;
                template.transform.localPosition = source.transform.localPosition;
                template.transform.localScale = source.transform.localScale;
                template.transform.localRotation = source.transform.localRotation;

                StripClonedScenarioBehaviors(template);
                ApplyClonedScenarioBookPresentation(template, root.layer, 0);
                template.SetActive(false);

                _scenarioBookVisualTemplate = template;
                MMLog.WriteDebug("[ScenarioBookVisuals] Cached vanilla scenario book visuals. reason="
                    + (reason ?? "unspecified")
                    + " source=" + GetObjectPath(source)
                    + " template=" + GetObjectPath(template)
                    + " widgets=" + CountWidgets(template) + ".");
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookVisuals] Failed to cache scenario book visuals: " + ex.Message);
            }
        }

        private static GameObject GetScenarioBookTemplateRoot()
        {
            if (_scenarioBookTemplateRoot != null)
                return _scenarioBookTemplateRoot;

            GameObject root = GameObject.Find(ScenarioBookTemplateRootName);
            if (root == null)
            {
                root = new GameObject(ScenarioBookTemplateRootName);
                Object.DontDestroyOnLoad(root);
            }

            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            _scenarioBookTemplateRoot = root;
            return root;
        }

        private static int ApplyClonedScenarioBookPresentation(GameObject clone, int layer, int visualDepth)
        {
            int maxVisualDepth = visualDepth;
            if (clone == null)
                return maxVisualDepth;

            clone.layer = layer;
            NGUITools.SetLayer(clone, layer);

            UIPanel ownerPanel = clone.transform.parent != null
                ? NGUITools.FindInParents<UIPanel>(clone.transform.parent.gameObject)
                : null;
            UIPanel[] clonedPanels = clone.GetComponentsInChildren<UIPanel>(true);
            for (int i = 0; i < clonedPanels.Length; i++)
            {
                clonedPanels[i].alpha = 1f;
                if (ownerPanel != null)
                    clonedPanels[i].depth = ownerPanel.depth;
            }

            UIButton[] buttons = clone.GetComponentsInChildren<UIButton>(true);
            for (int i = 0; i < buttons.Length; i++)
                Object.Destroy(buttons[i].gameObject);

            UILabel[] labels = clone.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
                Object.Destroy(labels[i].gameObject);

            Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);

            Collider2D[] colliders2D = clone.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
                Object.Destroy(colliders2D[i]);

            UIWidget[] widgets = clone.GetComponentsInChildren<UIWidget>(true);
            int minDepth = FindMinimumWidgetDepth(widgets);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null)
                    continue;

                widget.gameObject.layer = layer;
                widget.depth = visualDepth + (widget.depth - minDepth);
                if (widget.depth > maxVisualDepth)
                    maxVisualDepth = widget.depth;
            }

            return maxVisualDepth;
        }

        private static int FindMinimumWidgetDepth(UIWidget[] widgets)
        {
            if (widgets == null || widgets.Length == 0)
                return 0;

            int minDepth = widgets[0] != null ? widgets[0].depth : 0;
            for (int i = 1; i < widgets.Length; i++)
            {
                if (widgets[i] != null && widgets[i].depth < minDepth)
                    minDepth = widgets[i].depth;
            }

            return minDepth;
        }

        private static int CountWidgets(GameObject root)
        {
            return root != null ? root.GetComponentsInChildren<UIWidget>(true).Length : 0;
        }

        private static string GetObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return "<null>";

            Transform current = gameObject.transform;
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
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
            foreach (TweenAlpha tween in clone.GetComponentsInChildren<TweenAlpha>(true))
                tween.value = 1f;
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

                UIExtensionService.ClearEventListenerDelegates(listener);
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
