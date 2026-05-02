using UnityEngine;

namespace ShelteredAPI.Scenarios.UiKit.Layout
{
    /// <summary>
    /// Pure rect-math primitives for laying out the inside of a window. Composes
    /// with <see cref="ShelteredAPI.Scenarios.ScenarioAuthoringShellLayout"/> rather
    /// than replacing it: the shell layout owns top-level regions (top bar,
    /// inspector, workspace) and this engine subdivides them.
    /// </summary>
    internal static class ScenarioUiLayoutEngine
    {
        /// <summary>Returns <paramref name="rect"/> shrunk uniformly by <paramref name="padding"/>.</summary>
        public static Rect Inset(Rect rect, float padding)
        {
            return Inset(rect, padding, padding, padding, padding);
        }

        public static Rect Inset(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(
                rect.x + left,
                rect.y + top,
                Mathf.Max(0f, rect.width - left - right),
                Mathf.Max(0f, rect.height - top - bottom));
        }

        /// <summary>Splits a rect from the top into a header strip and a remainder.</summary>
        public static void SplitTop(Rect rect, float headerHeight, float gutter, out Rect header, out Rect remainder)
        {
            float clamped = Mathf.Clamp(headerHeight, 0f, rect.height);
            header = new Rect(rect.x, rect.y, rect.width, clamped);
            float bodyY = header.yMax + gutter;
            float bodyHeight = Mathf.Max(0f, rect.yMax - bodyY);
            remainder = new Rect(rect.x, bodyY, rect.width, bodyHeight);
        }

        /// <summary>Splits a rect from the bottom into a footer strip and a remainder.</summary>
        public static void SplitBottom(Rect rect, float footerHeight, float gutter, out Rect remainder, out Rect footer)
        {
            float clamped = Mathf.Clamp(footerHeight, 0f, rect.height);
            footer = new Rect(rect.x, rect.yMax - clamped, rect.width, clamped);
            float bodyHeight = Mathf.Max(0f, footer.y - gutter - rect.y);
            remainder = new Rect(rect.x, rect.y, rect.width, bodyHeight);
        }

        /// <summary>Splits a rect from the left into a leading strip and a remainder.</summary>
        public static void SplitLeft(Rect rect, float leadingWidth, float gutter, out Rect leading, out Rect remainder)
        {
            float clamped = Mathf.Clamp(leadingWidth, 0f, rect.width);
            leading = new Rect(rect.x, rect.y, clamped, rect.height);
            float bodyX = leading.xMax + gutter;
            float bodyWidth = Mathf.Max(0f, rect.xMax - bodyX);
            remainder = new Rect(bodyX, rect.y, bodyWidth, rect.height);
        }

        /// <summary>Splits a rect from the right into a trailing strip and a remainder.</summary>
        public static void SplitRight(Rect rect, float trailingWidth, float gutter, out Rect remainder, out Rect trailing)
        {
            float clamped = Mathf.Clamp(trailingWidth, 0f, rect.width);
            trailing = new Rect(rect.xMax - clamped, rect.y, clamped, rect.height);
            float bodyWidth = Mathf.Max(0f, trailing.x - gutter - rect.x);
            remainder = new Rect(rect.x, rect.y, bodyWidth, rect.height);
        }

        /// <summary>
        /// Splits a rect into <paramref name="weights"/>.Length horizontal cells
        /// separated by <paramref name="gutter"/>. Weights are normalised, so
        /// passing <c>{1,1,2}</c> yields a 25/25/50 split.
        /// </summary>
        public static Rect[] Columns(Rect rect, float gutter, params float[] weights)
        {
            return Distribute(rect, gutter, weights, true);
        }

        /// <summary>Vertical equivalent of <see cref="Columns"/>.</summary>
        public static Rect[] Rows(Rect rect, float gutter, params float[] weights)
        {
            return Distribute(rect, gutter, weights, false);
        }

        /// <summary>
        /// Lays out <paramref name="count"/> equal-sized cells in a grid.
        /// Cells are populated row-major (left-to-right, then top-to-bottom).
        /// </summary>
        public static Rect[] Grid(Rect rect, int columns, int rows, float gutter, int count)
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            int safeCount = Mathf.Clamp(count, 0, safeColumns * safeRows);
            if (safeCount == 0)
                return new Rect[0];

            float cellWidth = Mathf.Max(0f, (rect.width - (gutter * (safeColumns - 1))) / safeColumns);
            float cellHeight = Mathf.Max(0f, (rect.height - (gutter * (safeRows - 1))) / safeRows);

            Rect[] cells = new Rect[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                int col = i % safeColumns;
                int row = i / safeColumns;
                cells[i] = new Rect(
                    rect.x + (col * (cellWidth + gutter)),
                    rect.y + (row * (cellHeight + gutter)),
                    cellWidth,
                    cellHeight);
            }
            return cells;
        }

        /// <summary>
        /// Stacks <paramref name="heights"/>.Length children vertically. A child
        /// height of <c>0</c> means "consume remaining space"; if more than one
        /// child is flexible, the slack is split evenly.
        /// </summary>
        public static Rect[] Stack(Rect rect, float gutter, params float[] heights)
        {
            if (heights == null || heights.Length == 0)
                return new Rect[0];

            int flexibleCount = 0;
            float fixedTotal = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] <= 0f)
                    flexibleCount++;
                else
                    fixedTotal += heights[i];
            }

            float gutterTotal = gutter * Mathf.Max(0, heights.Length - 1);
            float slack = Mathf.Max(0f, rect.height - fixedTotal - gutterTotal);
            float flexibleEach = flexibleCount > 0 ? slack / flexibleCount : 0f;

            Rect[] cells = new Rect[heights.Length];
            float cursor = rect.y;
            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i] <= 0f ? flexibleEach : heights[i];
                cells[i] = new Rect(rect.x, cursor, rect.width, height);
                cursor += height + gutter;
            }
            return cells;
        }

        private static Rect[] Distribute(Rect rect, float gutter, float[] weights, bool horizontal)
        {
            if (weights == null || weights.Length == 0)
                return new Rect[0];

            float weightTotal = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                    weightTotal += weights[i];
            }
            if (weightTotal <= 0f)
                weightTotal = weights.Length;

            float gutterTotal = gutter * Mathf.Max(0, weights.Length - 1);
            float available = horizontal
                ? Mathf.Max(0f, rect.width - gutterTotal)
                : Mathf.Max(0f, rect.height - gutterTotal);

            Rect[] cells = new Rect[weights.Length];
            float cursor = horizontal ? rect.x : rect.y;
            for (int i = 0; i < weights.Length; i++)
            {
                float weight = weights[i] > 0f ? weights[i] : 1f;
                float size = (weight / weightTotal) * available;
                if (horizontal)
                {
                    cells[i] = new Rect(cursor, rect.y, size, rect.height);
                    cursor += size + gutter;
                }
                else
                {
                    cells[i] = new Rect(rect.x, cursor, rect.width, size);
                    cursor += size + gutter;
                }
            }
            return cells;
        }
    }
}
