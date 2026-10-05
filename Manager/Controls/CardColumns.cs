using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Flows cards into one or two columns depending on the available width
    /// and sizes itself to the tallest column. Dock it to the top of an
    /// auto-scrolling panel for vertical-only scrolling.
    /// </summary>
    public class CardColumns : Panel
    {
        private int _twoColumnMinWidth = 720;
        private int _gap = 12;
        private bool _arranging;

        public CardColumns()
        {
            Margin = new Padding(0);
            Padding = new Padding(0);
        }

        public int TwoColumnMinWidth
        {
            get { return _twoColumnMinWidth; }
            set
            {
                _twoColumnMinWidth = value;
                PerformLayout();
            }
        }

        public void AddCard(Control card)
        {
            card.Margin = new Padding(0);
            if (card is CardPanel)
                ((CardPanel)card).AutoSize = false;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(card);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (_arranging)
                return;
            _arranging = true;
            try
            {
                // Changing our height can show or hide the parent's scrollbar,
                // which changes our width; arrange again until it settles.
                for (int pass = 0; pass < 3; pass++)
                {
                    int width = ClientSize.Width;
                    Arrange();
                    if (ClientSize.Width == width)
                        break;
                }
            }
            finally
            {
                _arranging = false;
            }
        }

        private void Arrange()
        {
            int width = ClientSize.Width;
            if (width <= 0)
                return;
            int columns = width >= _twoColumnMinWidth ? 2 : 1;
            int columnWidth = columns == 2 ? (width - _gap) / 2 : width;
            int[] heights = new int[columns];

            for (int i = 0; i < Controls.Count; i++)
            {
                Control card = Controls[i];
                if (!card.Visible)
                    continue;
                int column = 0;
                if (columns == 2 && heights[1] < heights[0])
                    column = 1;
                int height = card.GetPreferredSize(new Size(columnWidth, 0)).Height;
                int x = column * (columnWidth + _gap);
                card.SetBounds(x, heights[column], columnWidth, height);
                heights[column] += height + _gap;
            }

            int total = 0;
            for (int i = 0; i < heights.Length; i++)
                total = Math.Max(total, heights[i]);
            total = Math.Max(0, total - _gap);
            if (Height != total)
                Height = total;
        }
    }
}
