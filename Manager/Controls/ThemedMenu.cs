using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Drop-down menus in the palette. The default renderer paints a light
    /// system menu, which looks foreign next to the rest of the manager.
    /// </summary>
    public static class ThemedMenu
    {
        public static ContextMenuStrip Create()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = ManagerTheme.UiFont(9.5f);
            menu.Opening += delegate { ApplyTheme(menu); };
            return menu;
        }

        public static void ApplyTheme(ToolStrip menu)
        {
            ThemePalette palette = ManagerTheme.Current;
            menu.Renderer = new ToolStripProfessionalRenderer(new PaletteColors(palette)) { RoundedEdges = false };
            menu.BackColor = palette.Surface;
            menu.ForeColor = palette.Text;
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = item.Enabled ? palette.Text : palette.TextDisabled;
                item.Padding = new Padding(4, 3, 4, 3);
            }
        }

        /// <summary>Drops a menu below a button, aligned to its left edge.</summary>
        public static void ShowBelow(ContextMenuStrip menu, Control anchor)
        {
            menu.Show(anchor, new Point(0, anchor.Height + 2));
        }

        private sealed class PaletteColors : ProfessionalColorTable
        {
            private readonly ThemePalette _palette;

            public PaletteColors(ThemePalette palette)
            {
                _palette = palette;
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground { get { return _palette.Surface; } }
            public override Color MenuBorder { get { return _palette.BorderStrong; } }
            public override Color MenuItemBorder { get { return _palette.Border; } }
            public override Color MenuItemSelected { get { return _palette.SurfaceHover; } }
            public override Color MenuItemSelectedGradientBegin { get { return _palette.SurfaceHover; } }
            public override Color MenuItemSelectedGradientEnd { get { return _palette.SurfaceHover; } }
            public override Color MenuItemPressedGradientBegin { get { return _palette.Selection; } }
            public override Color MenuItemPressedGradientEnd { get { return _palette.Selection; } }
            public override Color ImageMarginGradientBegin { get { return _palette.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return _palette.Surface; } }
            public override Color ImageMarginGradientEnd { get { return _palette.Surface; } }
            public override Color SeparatorDark { get { return _palette.Border; } }
            public override Color SeparatorLight { get { return _palette.Border; } }
            public override Color CheckBackground { get { return _palette.Selection; } }
            public override Color CheckSelectedBackground { get { return _palette.Selection; } }
            public override Color CheckPressedBackground { get { return _palette.Selection; } }
        }
    }
}
