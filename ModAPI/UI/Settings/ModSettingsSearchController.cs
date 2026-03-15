using System;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal delegate UILabel ModSettingsCreateLabelDelegate(
        Transform parent,
        string name,
        string text,
        Vector3 position,
        int fontSize,
        Color color,
        UIFont uiFont,
        Font ttfFont,
        int depth);

    internal sealed class ModSettingsSearchController
    {
        private GameObject _inputRoot;
        private UILabel _displayLabel;
        private bool _manualSearchEnabled;
        private bool _hasFocus;

        internal string Filter { get; private set; }

        internal void CreateSearchBar(
            Transform parent,
            UIFont uiFont,
            Font ttfFont,
            Texture2D whiteTexture,
            Color subtextColor,
            ModSettingsCreateLabelDelegate createLabel)
        {
            UILabel searchLabel = createLabel(parent, "SearchLabel", "SEARCH:", new Vector3(-160, 0, 0), 14, subtextColor, uiFont, ttfFont, 100);
            searchLabel.pivot = UIWidget.Pivot.Right;
            searchLabel.transform.localPosition = new Vector3(-110, 0, 0);

            GameObject inputGO = new GameObject("SearchInput");
            inputGO.transform.SetParent(parent, false);
            inputGO.transform.localPosition = new Vector3(60, 0, 0);
            inputGO.layer = parent.gameObject.layer;

            UITexture inputBg = inputGO.AddComponent<UITexture>();
            inputBg.mainTexture = whiteTexture;
            inputBg.width = 320;
            inputBg.height = 35;
            inputBg.depth = 100;
            inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            GameObject lblGO = new GameObject("Label");
            lblGO.transform.SetParent(inputGO.transform, false);
            lblGO.layer = inputGO.layer;
            lblGO.transform.localPosition = new Vector3(-150, 0, 0);

            UILabel label = lblGO.AddComponent<UILabel>();
            label.pivot = UIWidget.Pivot.Left;
            label.fontSize = 16;
            label.width = 300;
            label.depth = 105;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            if (uiFont != null)
                label.bitmapFont = uiFont;
            else if (ttfFont != null)
                label.trueTypeFont = ttfFont;
            else
                label.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.alignment = NGUIText.Alignment.Left;
            label.text = string.Empty;

            _inputRoot = inputGO;
            _displayLabel = label;
            Filter = string.Empty;
            _manualSearchEnabled = false;
            _hasFocus = false;

            bool hasUsableFont = label.bitmapFont != null || label.trueTypeFont != null;
            if (!hasUsableFont)
            {
                searchLabel.gameObject.SetActive(false);
                label.pivot = UIWidget.Pivot.Center;
                label.alignment = NGUIText.Alignment.Center;
                label.transform.localPosition = Vector3.zero;
                label.text = "Search unavailable";
                label.color = subtextColor;
                return;
            }

            EnableManualSearchInput(inputGO, label, subtextColor);
        }

        internal void HandleInput(int maxSearchLength, Color subtextColor, Action onFilterChanged)
        {
            if (!_manualSearchEnabled || _inputRoot == null || _displayLabel == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _hasFocus = IsHoveredWithin(_inputRoot);
                RefreshDisplay(subtextColor);
            }

            if (!_hasFocus)
                return;

            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed))
                return;

            bool changed = false;
            string currentFilter = Filter ?? string.Empty;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == '\b')
                {
                    if (!string.IsNullOrEmpty(currentFilter))
                    {
                        currentFilter = currentFilter.Substring(0, currentFilter.Length - 1);
                        changed = true;
                    }
                    continue;
                }

                if (c == '\n' || c == '\r')
                {
                    _hasFocus = false;
                    continue;
                }

                if (char.IsControl(c) || currentFilter.Length >= maxSearchLength)
                    continue;

                currentFilter += c;
                changed = true;
            }

            if (changed)
                Filter = currentFilter;

            if (changed && onFilterChanged != null)
                onFilterChanged();

            RefreshDisplay(subtextColor);
        }

        internal void RefreshDisplay(Color subtextColor)
        {
            if (_displayLabel == null)
                return;

            if (string.IsNullOrEmpty(Filter))
            {
                _displayLabel.text = _hasFocus ? "|" : "Search...";
                _displayLabel.color = _hasFocus ? Color.white : subtextColor;
                return;
            }

            _displayLabel.text = _hasFocus ? (Filter + "|") : Filter;
            _displayLabel.color = Color.white;
        }

        private void EnableManualSearchInput(GameObject inputGO, UILabel label, Color subtextColor)
        {
            if (inputGO == null || label == null)
                return;

            _manualSearchEnabled = true;
            _hasFocus = false;

            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = new Vector3(-150, 0, 0);
            label.color = Color.white;

            BoxCollider col = inputGO.GetComponent<BoxCollider>();
            if (col == null)
                col = inputGO.AddComponent<BoxCollider>();
            col.size = new Vector3(320, 35, 1);
            col.center = Vector3.zero;

            UIEventListener.Get(inputGO).onClick = delegate
            {
                _hasFocus = true;
                UICamera.selectedObject = null;
                RefreshDisplay(subtextColor);
            };

            RefreshDisplay(subtextColor);
        }

        private static bool IsHoveredWithin(GameObject root)
        {
            if (root == null)
                return false;

            GameObject hovered = UICamera.hoveredObject;
            if (hovered == null)
                return false;
            if (hovered == root)
                return true;

            return hovered.transform != null && hovered.transform.IsChildOf(root.transform);
        }
    }
}
