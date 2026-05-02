using System;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Reusable Sheltered book-style page controls. Disabled page arrows are hidden,
    /// matching vanilla clipboard/status pages.
    /// </summary>
    internal sealed class BookPageNavigatorWidget
    {
        private readonly IThemePalette _palette;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;
        private GameObject _previousButton;
        private GameObject _nextButton;
        private UILabel _pageLabel;

        public BookPageNavigatorWidget(IThemePalette palette, ITextureLibrary textures, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _textures = textures;
            _ui = ui;
        }

        public void Build(GameObject parent, Vector3 centerPosition, Action previous, Action next)
        {
            var buttonFactory = new BookButtonWidget(_palette, _textures, _ui);

            _previousButton = buttonFactory.Build(parent, "PreviousPageButton", "<",
                centerPosition + new Vector3(-86f, 0f, 0f), 64, 58, 28, previous);
            _nextButton = buttonFactory.Build(parent, "NextPageButton", ">",
                centerPosition + new Vector3(86f, 0f, 0f), 64, 58, 28, next);

            _pageLabel = _ui.CreateLabel(parent, "PageLabel", "1/1",
                centerPosition, 18, _palette.InkFaded,
                120, 30, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            _pageLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        public void UpdateState(int currentPageIndex, int pageCount)
        {
            int safePageCount = Mathf.Max(1, pageCount);
            int safeCurrentPage = Mathf.Clamp(currentPageIndex, 0, safePageCount - 1);

            if (_pageLabel != null)
                _pageLabel.text = (safeCurrentPage + 1).ToString() + "/" + safePageCount.ToString();

            bool hasMultiplePages = safePageCount > 1;
            if (_previousButton != null)
                _previousButton.SetActive(hasMultiplePages && safeCurrentPage > 0);
            if (_nextButton != null)
                _nextButton.SetActive(hasMultiplePages && safeCurrentPage < safePageCount - 1);
        }
    }
}
