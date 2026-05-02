using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Layout
{
    /// <summary>
    /// Small state holder for vanilla-style page movement.
    /// </summary>
    internal sealed class PanelPageState
    {
        private int _currentPageIndex;
        private int _pageCount = 1;

        public int CurrentPageIndex { get { return _currentPageIndex; } }
        public int PageCount { get { return _pageCount; } }
        public bool CanGoPrevious { get { return _currentPageIndex > 0; } }
        public bool CanGoNext { get { return _currentPageIndex < _pageCount - 1; } }

        public void SetPageCount(int pageCount)
        {
            _pageCount = Mathf.Max(1, pageCount);
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, _pageCount - 1);
        }

        public bool MoveBy(int delta)
        {
            int nextPageIndex = Mathf.Clamp(_currentPageIndex + delta, 0, _pageCount - 1);
            if (nextPageIndex == _currentPageIndex)
                return false;

            _currentPageIndex = nextPageIndex;
            return true;
        }

        public void Reset()
        {
            _currentPageIndex = 0;
        }
    }
}
