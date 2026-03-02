using System;
using System.Collections.Generic;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Entry points for fluent NGUI/UI takeover operations.
    /// </summary>
    public static class UITakeover
    {
        public static UITakeoverSession For(BasePanel panel)
        {
            return panel == null ? new UITakeoverSession(null) : new UITakeoverSession(panel.transform);
        }

        public static UITakeoverSession For(GameObject root)
        {
            return root == null ? new UITakeoverSession(null) : new UITakeoverSession(root.transform);
        }

        public static UITakeoverSession For(Transform root)
        {
            return new UITakeoverSession(root);
        }
    }

    /// <summary>
    /// Fluent UI takeover session scoped to one UI root.
    /// </summary>
    public sealed class UITakeoverSession
    {
        private readonly Transform _root;
        private readonly List<Action> _restoreActions = new List<Action>();

        internal UITakeoverSession(Transform root)
        {
            _root = root;
        }

        public Transform Root { get { return _root; } }

        public GameObject Resolve(string path)
        {
            Transform t = ResolveTransform(path);
            return t != null ? t.gameObject : null;
        }

        public T Resolve<T>(string path) where T : Component
        {
            GameObject go = Resolve(path);
            if (go == null) return null;
            return go.GetComponent<T>();
        }

        public UITakeoverSession Do(string path, Action<GameObject> action)
        {
            if (action == null) return this;
            GameObject go = Resolve(path);
            if (go != null) action(go);
            return this;
        }

        public UITakeoverSession SetLabelText(string path, string text)
        {
            return SetLabelText(path, text, true);
        }

        public UITakeoverSession SetLabelText(string path, string text, bool rememberOriginal)
        {
            UILabel label = Resolve<UILabel>(path);
            if (label == null) return this;

            if (rememberOriginal)
            {
                string old = label.text;
                _restoreActions.Add(delegate
                {
                    if (label != null) label.text = old;
                });
            }

            label.text = text ?? string.Empty;
            return this;
        }

        public UITakeoverSession SetLabelColor(string path, Color color)
        {
            return SetLabelColor(path, color, true);
        }

        public UITakeoverSession SetLabelColor(string path, Color color, bool rememberOriginal)
        {
            UILabel label = Resolve<UILabel>(path);
            if (label == null) return this;

            if (rememberOriginal)
            {
                Color old = label.color;
                _restoreActions.Add(delegate
                {
                    if (label != null) label.color = old;
                });
            }

            label.color = color;
            return this;
        }

        public UITakeoverSession SetActive(string path, bool active)
        {
            return SetActive(path, active, true);
        }

        public UITakeoverSession SetActive(string path, bool active, bool rememberOriginal)
        {
            GameObject go = Resolve(path);
            if (go == null) return this;

            if (rememberOriginal)
            {
                bool old = go.activeSelf;
                _restoreActions.Add(delegate
                {
                    if (go != null) go.SetActive(old);
                });
            }

            go.SetActive(active);
            return this;
        }

        public UITakeoverSession EnsureCollider(string path)
        {
            return EnsureCollider(path, 24f, 24f, 8f, 6f);
        }

        public UITakeoverSession EnsureCollider(string path, float minWidth, float minHeight, float paddingX, float paddingY)
        {
            GameObject go = Resolve(path);
            if (go == null) return this;

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            if (box == null) return this;

            float width = Mathf.Max(1f, minWidth);
            float height = Mathf.Max(1f, minHeight);

            UIWidget widget = go.GetComponent<UIWidget>();
            if (widget != null)
            {
                Vector2 size = widget.localSize;
                width = Mathf.Max(width, size.x + paddingX * 2f);
                height = Mathf.Max(height, size.y + paddingY * 2f);
            }
            else
            {
                width = Mathf.Max(width, box.size.x);
                height = Mathf.Max(height, box.size.y);
            }

            box.size = new Vector3(width, height, 1f);
            if (Mathf.Abs(box.center.z) > 0.001f)
                box.center = new Vector3(box.center.x, box.center.y, 0f);

            return this;
        }

        public UITakeoverSession BindClick(string path, string bindingKey, Action<GameObject> onClick)
        {
            GameObject go = Resolve(path);
            if (go == null) return this;

            EnsureCollider(path);
            UIEventBindingRegistry.BindClick(go, bindingKey, onClick);
            return this;
        }

        public UITakeoverSession BindHover(string path, string bindingKey, Action<GameObject, bool> onHover)
        {
            GameObject go = Resolve(path);
            if (go == null) return this;

            EnsureCollider(path);
            UIEventBindingRegistry.BindHover(go, bindingKey, onHover);
            return this;
        }

        public UITakeoverSession BindTooltip(string path, string bindingKey, string tooltipText)
        {
            return BindHover(path, bindingKey, delegate(GameObject go, bool isOver)
            {
                if (isOver) ModTooltip.Show(tooltipText);
                else ModTooltip.Hide();
            });
        }

        public void Restore()
        {
            for (int i = _restoreActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _restoreActions[i]();
                }
                catch { }
            }
            _restoreActions.Clear();
        }

        private Transform ResolveTransform(string path)
        {
            if (_root == null) return null;
            if (string.IsNullOrEmpty(path) || path == "." || path == "/")
                return _root;

            Transform direct = _root.Find(path);
            if (direct != null) return direct;

            return FindByNameRecursive(_root, path);
        }

        private static Transform FindByNameRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindByNameRecursive(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }

    internal static class UIEventBindingRegistry
    {
        public static void BindClick(GameObject go, string key, Action<GameObject> onClick)
        {
            if (go == null) return;
            key = string.IsNullOrEmpty(key) ? "__default_click__" : key;
            TakeoverEventRelay relay = GetOrAddRelay(go);
            if (relay == null) return;
            relay.BindClick(key, onClick);
        }

        public static void BindHover(GameObject go, string key, Action<GameObject, bool> onHover)
        {
            if (go == null) return;
            key = string.IsNullOrEmpty(key) ? "__default_hover__" : key;
            TakeoverEventRelay relay = GetOrAddRelay(go);
            if (relay == null) return;
            relay.BindHover(key, onHover);
        }

        private static TakeoverEventRelay GetOrAddRelay(GameObject go)
        {
            TakeoverEventRelay relay = go.GetComponent<TakeoverEventRelay>();
            if (relay == null) relay = go.AddComponent<TakeoverEventRelay>();
            return relay;
        }
    }

    internal sealed class TakeoverEventRelay : MonoBehaviour
    {
        private readonly Dictionary<string, Action<GameObject>> _clickHandlers = new Dictionary<string, Action<GameObject>>();
        private readonly Dictionary<string, Action<GameObject, bool>> _hoverHandlers = new Dictionary<string, Action<GameObject, bool>>();
        private bool _wired;

        public void BindClick(string key, Action<GameObject> handler)
        {
            key = string.IsNullOrEmpty(key) ? "__default_click__" : key;
            _clickHandlers[key] = handler;
            EnsureWired();
        }

        public void BindHover(string key, Action<GameObject, bool> handler)
        {
            key = string.IsNullOrEmpty(key) ? "__default_hover__" : key;
            _hoverHandlers[key] = handler;
            EnsureWired();
        }

        private void EnsureWired()
        {
            if (_wired) return;

            UIEventListener listener = UIEventListener.Get(gameObject);
            if (listener == null) return;

            listener.onClick += OnClick;
            listener.onHover += OnHover;
            _wired = true;
        }

        private void OnClick(GameObject go)
        {
            if (_clickHandlers.Count == 0) return;
            List<Action<GameObject>> handlers = new List<Action<GameObject>>(_clickHandlers.Values);
            for (int i = 0; i < handlers.Count; i++)
            {
                try
                {
                    if (handlers[i] != null) handlers[i](go);
                }
                catch { }
            }
        }

        private void OnHover(GameObject go, bool isOver)
        {
            if (_hoverHandlers.Count == 0) return;
            List<Action<GameObject, bool>> handlers = new List<Action<GameObject, bool>>(_hoverHandlers.Values);
            for (int i = 0; i < handlers.Count; i++)
            {
                try
                {
                    if (handlers[i] != null) handlers[i](go, isOver);
                }
                catch { }
            }
        }
    }
}
