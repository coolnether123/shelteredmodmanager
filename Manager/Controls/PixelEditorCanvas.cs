using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShelteredModManager.Shared.PixelEditing;

namespace Manager.Controls
{
    /// <summary>
    /// System.Drawing host for the shared pixel-editing session. This control owns
    /// pointer/rendering concerns only; history and document mutations stay shared.
    /// </summary>
    public sealed class PixelEditorCanvas : ScrollableControl
    {
        private PixelEditorSession _session;
        private bool _pointerDown;
        private bool _pointerChanged;
        private bool _hasUnsavedChanges;
        private int _zoom = 12;
        private Bitmap _renderBitmap;

        public event EventHandler DocumentChanged;
        public event EventHandler ActiveColorChanged;
        public event EventHandler ActiveToolChanged;

        public PixelEditorCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.FromArgb(36, 36, 38);
            TabStop = true;
            AutoScroll = true;
            Session = new PixelEditorSession(new PixelDocument(32, 32), 50);
        }

        public PixelEditorSession Session
        {
            get { return _session; }
            private set
            {
                _session = value;
                InvalidateRenderCache();
                UpdateScrollArea();
                Invalidate();
            }
        }

        public PixelEditorTool ActiveTool
        {
            get { return Session.ActiveTool; }
            set
            {
                if (Session.ActiveTool == value)
                    return;
                Session.ActiveTool = value;
                EventHandler handler = ActiveToolChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public Color ActiveColor
        {
            get
            {
                Rgba32 value = Session.ActiveColor;
                return Color.FromArgb(value.A, value.R, value.G, value.B);
            }
            set
            {
                Session.ActiveColor = new Rgba32(value.R, value.G, value.B, value.A);
                OnActiveColorChanged();
            }
        }

        public int Zoom
        {
            get { return _zoom; }
            set
            {
                _zoom = Math.Max(1, Math.Min(48, value));
                UpdateScrollArea();
                Invalidate();
            }
        }

        public Size CanvasPixelSize
        {
            get { return new Size(Session.Document.Width, Session.Document.Height); }
        }

        public bool HasUnsavedChanges
        {
            get { return _hasUnsavedChanges; }
        }

        public void CreateDocument(int width, int height)
        {
            Session = new PixelEditorSession(new PixelDocument(width, height), 50);
            _hasUnsavedChanges = true;
            OnDocumentChanged();
        }

        public bool LoadPng(string path, out string error)
        {
            PixelDocument document;
            if (!PixelEditorDrawingCodec.TryLoadPng(path, out document, out error))
                return false;
            Session = new PixelEditorSession(document, 50);
            Session.MarkSaved();
            _hasUnsavedChanges = false;
            Invalidate();
            return true;
        }

        public bool SavePng(string path, out string error)
        {
            if (!PixelEditorDrawingCodec.TrySavePng(Session.Document, path, out error))
                return false;
            return true;
        }

        public void MarkSaved()
        {
            Session.MarkSaved();
            _hasUnsavedChanges = false;
            OnDocumentChanged();
        }

        public void FitZoomToClient()
        {
            int availableWidth = Math.Max(1, ClientSize.Width - 24);
            int availableHeight = Math.Max(1, ClientSize.Height - 24);
            int horizontal = availableWidth / Session.Document.Width;
            int vertical = availableHeight / Session.Document.Height;
            Zoom = Math.Max(1, Math.Min(48, Math.Min(horizontal, vertical)));
            AutoScrollPosition = Point.Empty;
        }

        public bool Undo()
        {
            if (!Session.Undo())
                return false;
            _hasUnsavedChanges = true;
            InvalidateRenderCache();
            Invalidate();
            OnDocumentChanged();
            return true;
        }

        public bool Redo()
        {
            if (!Session.Redo())
                return false;
            _hasUnsavedChanges = true;
            InvalidateRenderCache();
            Invalidate();
            OnDocumentChanged();
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PixelDocument document = Session.Document;
            Rectangle canvas = GetCanvasBounds();
            EnsureRenderBitmap();
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            DrawTransparency(e.Graphics, canvas);
            e.Graphics.DrawImage(
                _renderBitmap,
                canvas,
                new Rectangle(0, 0, document.Width, document.Height),
                GraphicsUnit.Pixel);

            if (Zoom >= 8)
            {
                using (Pen grid = new Pen(Color.FromArgb(45, Color.Black)))
                {
                    int firstX = Math.Max(0, (e.ClipRectangle.Left - canvas.Left) / Zoom);
                    int lastX = Math.Min(document.Width, (e.ClipRectangle.Right - canvas.Left) / Zoom + 1);
                    int firstY = Math.Max(0, (e.ClipRectangle.Top - canvas.Top) / Zoom);
                    int lastY = Math.Min(document.Height, (e.ClipRectangle.Bottom - canvas.Top) / Zoom + 1);
                    for (int x = firstX; x <= lastX; x++)
                    {
                        int lineX = canvas.Left + x * Zoom;
                        e.Graphics.DrawLine(grid, lineX, canvas.Top, lineX, canvas.Bottom);
                    }
                    for (int y = firstY; y <= lastY; y++)
                    {
                        int lineY = canvas.Top + y * Zoom;
                        e.Graphics.DrawLine(grid, canvas.Left, lineY, canvas.Right, lineY);
                    }
                }
            }
            using (Pen border = new Pen(Color.DimGray))
                e.Graphics.DrawRectangle(border, canvas);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left)
                return;
            _pointerDown = true;
            _pointerChanged = false;
            Session.BeginStroke();
            ApplyPointer(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_pointerDown)
                ApplyPointer(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_pointerDown)
                return;
            _pointerDown = false;
            Session.EndStroke();
            if (_pointerChanged)
                OnDocumentChanged();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) == Keys.Control)
                Zoom += e.Delta > 0 ? 2 : -2;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.P)
                ActiveTool = PixelEditorTool.Paint;
            else if (e.KeyCode == Keys.E)
                ActiveTool = PixelEditorTool.Erase;
            else if (e.KeyCode == Keys.I)
                ActiveTool = PixelEditorTool.Pick;
            base.OnKeyDown(e);
        }

        private void ApplyPointer(Point point)
        {
            Rectangle canvas = GetCanvasBounds();
            int x = (point.X - canvas.Left) / Zoom;
            int y = (point.Y - canvas.Top) / Zoom;
            if (point.X < canvas.Left || point.Y < canvas.Top ||
                !Session.Document.Contains(x, y))
                return;

            if (Session.ActiveTool == PixelEditorTool.Pick)
            {
                if (Session.PickColor(x, y))
                    OnActiveColorChanged();
                return;
            }
            if (Session.ActiveTool == PixelEditorTool.Paint ||
                Session.ActiveTool == PixelEditorTool.Erase)
            {
                if (Session.PaintPixel(x, y))
                {
                    _pointerChanged = true;
                    _hasUnsavedChanges = true;
                    InvalidateRenderCache();
                    Invalidate();
                }
            }
        }

        private static void DrawTransparency(Graphics graphics, Rectangle canvas)
        {
            using (Bitmap tile = new Bitmap(16, 16))
            using (Graphics tileGraphics = Graphics.FromImage(tile))
            {
                tileGraphics.Clear(Color.FromArgb(205, 205, 205));
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(160, 160, 160)))
                {
                    tileGraphics.FillRectangle(dark, 0, 0, 8, 8);
                    tileGraphics.FillRectangle(dark, 8, 8, 8, 8);
                }
                using (TextureBrush brush = new TextureBrush(tile, WrapMode.Tile))
                    graphics.FillRectangle(brush, canvas);
            }
        }

        private Rectangle GetCanvasBounds()
        {
            int width = Session.Document.Width * Zoom;
            int height = Session.Document.Height * Zoom;
            int originX = AutoScrollPosition.X + Math.Max(0, (ClientSize.Width - width) / 2);
            int originY = AutoScrollPosition.Y + Math.Max(0, (ClientSize.Height - height) / 2);
            return new Rectangle(originX, originY, width, height);
        }

        private void UpdateScrollArea()
        {
            if (Session == null)
                return;
            AutoScrollMinSize = new Size(
                Session.Document.Width * Zoom + 24,
                Session.Document.Height * Zoom + 24);
        }

        private void EnsureRenderBitmap()
        {
            if (_renderBitmap == null)
                _renderBitmap = PixelEditorDrawingCodec.ToBitmap(Session.Document);
        }

        private void InvalidateRenderCache()
        {
            if (_renderBitmap != null)
            {
                _renderBitmap.Dispose();
                _renderBitmap = null;
            }
        }

        private void OnDocumentChanged()
        {
            EventHandler handler = DocumentChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnActiveColorChanged()
        {
            Invalidate();
            EventHandler handler = ActiveColorChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                InvalidateRenderCache();
            base.Dispose(disposing);
        }
    }

    internal static class PixelEditorDrawingCodec
    {
        public static bool TryLoadPng(string path, out PixelDocument document, out string error)
        {
            document = null;
            error = null;
            try
            {
                using (Image source = Image.FromFile(path))
                using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                        graphics.DrawImageUnscaled(source, 0, 0);
                    document = FromBitmap(bitmap);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not load PNG: " + ex.Message;
                return false;
            }
        }

        public static bool TrySavePng(PixelDocument document, string path, out string error)
        {
            error = null;
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (Bitmap bitmap = ToBitmap(document))
                    bitmap.Save(temporary, ImageFormat.Png);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temporary, path);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
                error = "Could not save PNG: " + ex.Message;
                return false;
            }
        }

        private static PixelDocument FromBitmap(Bitmap bitmap)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] rgba = new byte[bitmap.Width * bitmap.Height * 4];
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int source = x * 4;
                        int target = (y * bitmap.Width + x) * 4;
                        rgba[target] = row[source + 2];
                        rgba[target + 1] = row[source + 1];
                        rgba[target + 2] = row[source];
                        rgba[target + 3] = row[source + 3];
                    }
                }
                return new PixelDocument(bitmap.Width, bitmap.Height, rgba);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        internal static Bitmap ToBitmap(PixelDocument document)
        {
            Bitmap bitmap = new Bitmap(document.Width, document.Height, PixelFormat.Format32bppArgb);
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < document.Height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < document.Width; x++)
                    {
                        Rgba32 pixel = document.GetPixel(x, y);
                        int target = x * 4;
                        row[target] = pixel.B;
                        row[target + 1] = pixel.G;
                        row[target + 2] = pixel.R;
                        row[target + 3] = pixel.A;
                    }
                    Marshal.Copy(row, 0, new IntPtr(data.Scan0.ToInt64() + y * data.Stride), row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}
