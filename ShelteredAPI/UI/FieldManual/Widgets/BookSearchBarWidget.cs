using System;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Reusable geometry for book search controls. Callers can stack the scope
    /// label above a compact input or retain the legacy horizontal arrangement
    /// without reaching into the generated child hierarchy.
    /// </summary>
    internal sealed class BookSearchBarLayout
    {
        public Vector3 ScopeLabelPosition = new Vector3(-110f, 0f, 0f);
        public int ScopeLabelWidth = 100;
        public int ScopeLabelHeight = 24;
        public NGUIText.Alignment ScopeLabelAlignment = NGUIText.Alignment.Right;
        public UIWidget.Pivot ScopeLabelPivot = UIWidget.Pivot.Right;
        public Vector3 InputPosition = new Vector3(60f, 0f, 0f);
        public int InputWidth = 320;
        public int InputHeight = 35;
        public int InputTextPadding = 10;
    }

    /// <summary>
    /// Shared search bar for book-style Field Manual windows. It owns only input state
    /// and display; callers own filtering and page rebuilding.
    /// </summary>
    internal sealed class BookSearchBarWidget
    {
        private static readonly Color InputBackgroundColor = new Color(0.83f, 0.76f, 0.58f, 0.88f);
        private static readonly Color InputTextColor = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color PlaceholderColor = new Color(0.44f, 0.37f, 0.27f, 1f);

        private readonly IThemePalette _palette;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;
        private readonly int _maxLength;
        private GameObject _inputRoot;
        private UILabel _scopeLabel;
        private UILabel _displayLabel;
        private string _placeholder;
        private bool _hasFocus;
        private bool _evaluatePointerFocus;

        public BookSearchBarWidget(IThemePalette palette, ITextureLibrary textures, UIPrimitiveFactory ui)
            : this(palette, textures, ui, 64)
        {
        }

        public BookSearchBarWidget(IThemePalette palette, ITextureLibrary textures, UIPrimitiveFactory ui, int maxLength)
        {
            _palette = palette;
            _textures = textures;
            _ui = ui;
            _maxLength = maxLength < 1 ? 1 : maxLength;
            Filter = string.Empty;
        }

        public string Filter { get; private set; }

        public bool HasFocus { get { return _hasFocus; } }

        public void SetVisible(bool visible)
        {
            if (_inputRoot == null)
                return;

            Transform root = _inputRoot.transform.parent;
            if (root != null)
                root.gameObject.SetActive(visible);

            if (!visible)
                _hasFocus = false;
        }

        public GameObject Build(GameObject parent, string name, Vector3 localPosition, string placeholder)
        {
            return Build(parent, name, localPosition, "SEARCH:", placeholder);
        }

        public GameObject Build(GameObject parent, string name, Vector3 localPosition, string scopeLabel, string placeholder)
        {
            return Build(parent, name, localPosition, scopeLabel, placeholder, null);
        }

        public GameObject Build(
            GameObject parent,
            string name,
            Vector3 localPosition,
            string scopeLabel,
            string placeholder,
            BookSearchBarLayout layout)
        {
            BookSearchBarLayout resolvedLayout = layout ?? new BookSearchBarLayout();
            int inputWidth = Math.Max(40, resolvedLayout.InputWidth);
            int inputHeight = Math.Max(20, resolvedLayout.InputHeight);
            int inputPadding = Math.Max(0, Math.Min(resolvedLayout.InputTextPadding, (inputWidth - 1) / 2));
            GameObject root = _ui.CreateChild(parent, name, localPosition);

            _scopeLabel = _ui.CreateLabel(root, "SearchLabel",
                string.IsNullOrEmpty(scopeLabel) ? "SEARCH:" : scopeLabel,
                resolvedLayout.ScopeLabelPosition, 14, _palette.InkFaded,
                Math.Max(1, resolvedLayout.ScopeLabelWidth),
                Math.Max(1, resolvedLayout.ScopeLabelHeight),
                resolvedLayout.ScopeLabelAlignment,
                resolvedLayout.ScopeLabelPivot,
                _ui.NextDepth());
            _scopeLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            _placeholder = placeholder;

            _inputRoot = _ui.CreateChild(root, "SearchInput", resolvedLayout.InputPosition);
            _ui.CreateQuad(_inputRoot, "SearchInputPaper", _textures.White, Vector3.zero,
                inputWidth, inputHeight, InputBackgroundColor, _ui.NextDepth());
            _ui.AddClickCollider(_inputRoot, inputWidth, inputHeight, null);

            _displayLabel = _ui.CreateLabel(_inputRoot, "SearchText", string.Empty,
                new Vector3((-inputWidth * 0.5f) + inputPadding, 0f, 0f), 16, Color.white,
                Math.Max(1, inputWidth - (inputPadding * 2)),
                Math.Max(1, inputHeight - 7),
                NGUIText.Alignment.Left,
                UIWidget.Pivot.Left,
                _ui.NextDepth());
            _displayLabel.overflowMethod = UILabel.Overflow.ClampContent;
            _displayLabel.maxLineCount = 1;

            UIEventListener.Get(_inputRoot).onClick = delegate
            {
                _hasFocus = true;
                UICamera.selectedObject = null;
                RefreshDisplay(placeholder);
            };

            RefreshDisplay(placeholder);
            return root;
        }

        public void SetPresentation(string scopeLabel, string placeholder)
        {
            if (_scopeLabel != null)
                _scopeLabel.text = string.IsNullOrEmpty(scopeLabel) ? "SEARCH:" : scopeLabel;
            _placeholder = string.IsNullOrEmpty(placeholder) ? "Search..." : placeholder;
            RefreshDisplay(_placeholder);
        }

        public void HandleInput(Action onFilterChanged)
        {
            HandleInput("Search...", onFilterChanged);
        }

        public void HandleInput(string placeholder, Action onFilterChanged)
        {
            if (_inputRoot == null || _displayLabel == null)
                return;

            string activePlaceholder = string.IsNullOrEmpty(_placeholder) ? placeholder : _placeholder;

            // NGUI updates hoveredObject after regular MonoBehaviour.Update. Defer
            // the focus decision one frame so an outside click is evaluated against
            // the object that was actually clicked instead of the previous hover.
            if (_evaluatePointerFocus)
            {
                _hasFocus = IsHoveredWithin(_inputRoot);
                _evaluatePointerFocus = false;
                RefreshDisplay(activePlaceholder);
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _evaluatePointerFocus = true;

            if (!_hasFocus)
                return;

            string typed = UnityEngine.Input.inputString;
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

                if (char.IsControl(c) || currentFilter.Length >= _maxLength)
                    continue;

                currentFilter += c;
                changed = true;
            }

            if (changed)
                Filter = currentFilter;

            if (changed && onFilterChanged != null)
                onFilterChanged();

            RefreshDisplay(activePlaceholder);
        }

        public void Clear(Action onFilterChanged)
        {
            if (string.IsNullOrEmpty(Filter))
                return;

            Filter = string.Empty;
            RefreshDisplay("Search...");
            if (onFilterChanged != null)
                onFilterChanged();
        }

        public void RefreshDisplay(string placeholder)
        {
            if (_displayLabel == null)
                return;

            string emptyText = string.IsNullOrEmpty(placeholder) ? "Search..." : placeholder;
            if (string.IsNullOrEmpty(Filter))
            {
                _displayLabel.text = _hasFocus ? "|" : emptyText;
                _displayLabel.color = _hasFocus ? InputTextColor : PlaceholderColor;
                return;
            }

            string displayText = _hasFocus ? (Filter + "|") : Filter;
            _displayLabel.text = FitTrailingText(displayText);
            _displayLabel.color = InputTextColor;
        }

        private string FitTrailingText(string value)
        {
            if (_displayLabel == null || string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            string candidate = value;
            string wrapped;
            if (_displayLabel.Wrap(candidate, out wrapped, _displayLabel.height)
                && string.Equals(candidate, wrapped, StringComparison.Ordinal))
            {
                return candidate;
            }

            for (int start = 1; start < value.Length; start++)
            {
                candidate = "…" + value.Substring(start);
                if (_displayLabel.Wrap(candidate, out wrapped, _displayLabel.height)
                    && string.Equals(candidate, wrapped, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return value.Substring(value.Length - 1, 1);
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
