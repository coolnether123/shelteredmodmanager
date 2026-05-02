using System.Collections.Generic;

namespace ShelteredAPI.UI.FieldManual.Layout
{
    /// <summary>
    /// Splits fixed-height rows into pages without knowing what the rows render.
    /// </summary>
    internal sealed class PaperPagePaginator<T>
    {
        private readonly int _pageHeight;
        private readonly int _rowSpacing;

        public PaperPagePaginator(int pageHeight, int rowSpacing)
        {
            _pageHeight = pageHeight < 1 ? 1 : pageHeight;
            _rowSpacing = rowSpacing < 0 ? 0 : rowSpacing;
        }

        public List<List<T>> BuildPages(IList<PaperPageRow<T>> rows)
        {
            var pages = new List<List<T>>();
            if (rows == null || rows.Count == 0)
                return pages;

            var currentPage = new List<T>();
            int currentHeight = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                PaperPageRow<T> row = rows[i];
                if (row == null)
                    continue;

                if (ShouldStartNewPageForKeepWithNext(rows, i, currentPage.Count, currentHeight))
                {
                    pages.Add(currentPage);
                    currentPage = new List<T>();
                    currentHeight = 0;
                }

                int rowHeight = HeightWithSpacing(currentPage.Count, row.Height);
                if (currentPage.Count > 0 && currentHeight + rowHeight > _pageHeight)
                {
                    pages.Add(currentPage);
                    currentPage = new List<T>();
                    currentHeight = 0;
                    rowHeight = row.Height;
                }

                currentPage.Add(row.Item);
                currentHeight += rowHeight;
            }

            if (currentPage.Count > 0)
                pages.Add(currentPage);

            return pages;
        }

        private bool ShouldStartNewPageForKeepWithNext(
            IList<PaperPageRow<T>> rows,
            int index,
            int currentPageCount,
            int currentHeight)
        {
            if (currentPageCount == 0)
                return false;

            PaperPageRow<T> row = rows[index];
            if (row == null || !row.KeepWithNext || index >= rows.Count - 1)
                return false;

            PaperPageRow<T> next = rows[index + 1];
            if (next == null)
                return false;

            int requiredHeight = HeightWithSpacing(currentPageCount, row.Height) + _rowSpacing + next.Height;
            return currentHeight + requiredHeight > _pageHeight;
        }

        private int HeightWithSpacing(int currentPageCount, int rowHeight)
        {
            return currentPageCount > 0 ? _rowSpacing + rowHeight : rowHeight;
        }
    }
}
