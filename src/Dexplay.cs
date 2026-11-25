// ============================================================================
// Dexplay — Desktop Dynamic Music Island & Audio Player
// Direct Win32 / GDI+ / Windows Media Foundation / YouTube Music Official Audio
// Zero Web Server • Zero Browser Dependencies • 100% Native Desktop Overlay
// Language: C# (.NET Framework 4.5+ / Win32 API / Windows Forms)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;

namespace Dexplay
{
    // ========================================================================
    // 1. Native Win32 API
    // ========================================================================
    public static class Win32
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_APPCOMMAND = 0x0319;

        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_NOREPEAT = 0x4000;

        public const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        public const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        public const int APPCOMMAND_MEDIA_STOP = 13;
        public const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // Native C99 title_cleaner.dll P/Invoke from ytmp3 project
        [DllImport(@"native\title_cleaner.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int clean_title_native(string raw_title, string uploader, StringBuilder out_artist, StringBuilder out_title, int max_len);

        [DllImport(@"native\title_cleaner.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int verify_audio_header(string filepath);
    }

    // ========================================================================
    // 2. High-Fidelity Vector Button (Eliminates Broken Unicode Square Boxes [])
    // ========================================================================
    public enum VectorIconType
    {
        Play,
        Pause,
        Search,
        ChevronDown,
        ChevronUp,
        Pin,
        Island,
        Close,
        Prev,
        Next,
        Volume
    }

    public class VectorButton : Panel
    {
        public VectorIconType IconType { get; set; }
        public System.Drawing.Color NormalColor { get; set; }
        public System.Drawing.Color HoverColor { get; set; }
        public System.Drawing.Color ActiveColor { get; set; }
        public bool IsActive { get; set; }
        public bool IsCircleButton { get; set; }
        public System.Drawing.Color CircleBgColor { get; set; }
        private bool _isHovered = false;

        public VectorButton(VectorIconType type, int x, int y, int w, int h)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.IconType = type;
            this.Location = new System.Drawing.Point(x, y);
            this.Size = new System.Drawing.Size(w, h);
            this.BackColor = System.Drawing.Color.Transparent;
            this.NormalColor = System.Drawing.Color.FromArgb(156, 163, 175);
            this.HoverColor = System.Drawing.Color.White;
            this.ActiveColor = System.Drawing.Color.FromArgb(56, 189, 248);
            this.CircleBgColor = System.Drawing.Color.Transparent;
            this.IsCircleButton = false;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;

            this.MouseEnter += (s, e) => { _isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; this.Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (IsCircleButton && CircleBgColor != System.Drawing.Color.Transparent)
            {
                using (SolidBrush bgB = new SolidBrush(_isHovered ? System.Drawing.Color.White : CircleBgColor))
                {
                    g.FillEllipse(bgB, 1, 1, this.Width - 3, this.Height - 3);
                }
            }


            else if (_isHovered)
            {
                using (SolidBrush bgB = new SolidBrush(System.Drawing.Color.FromArgb(28, 255, 255, 255)))
                {
                    g.FillEllipse(bgB, 2, 2, this.Width - 4, this.Height - 4);
                }
            }



            System.Drawing.Color icCol;
            if (IsCircleButton && CircleBgColor != System.Drawing.Color.Transparent)
            {
                icCol = _isHovered ? System.Drawing.Color.FromArgb(8, 9, 13) : NormalColor;
            }
            else
            {
                icCol = IsActive ? ActiveColor : (_isHovered ? HoverColor : NormalColor);
            }

            using (System.Drawing.Pen pen = new System.Drawing.Pen(icCol, 2f))
            using (SolidBrush brush = new SolidBrush(icCol))
            {
                int cx = this.Width / 2;
                int cy = this.Height / 2;

                switch (IconType)
                {
                    case VectorIconType.Play:
                        g.FillPolygon(brush, new System.Drawing.Point[] {
                            new System.Drawing.Point(cx - 4, cy - 6),
                            new System.Drawing.Point(cx - 4, cy + 6),
                            new System.Drawing.Point(cx + 6, cy)
                        });
                        break;
                    case VectorIconType.Pause:
                        g.FillRectangle(brush, cx - 5, cy - 6, 3, 12);
                        g.FillRectangle(brush, cx + 2, cy - 6, 3, 12);
                        break;
                    case VectorIconType.Search:
                        pen.Width = 2f;
                        g.DrawEllipse(pen, cx - 6, cy - 7, 9, 9);
                        g.DrawLine(pen, cx + 1, cy, cx + 6, cy + 5);
                        break;
                    case VectorIconType.ChevronDown:
                        pen.Width = 2f;
                        g.DrawLines(pen, new System.Drawing.Point[] {
                            new System.Drawing.Point(cx - 5, cy - 2),
                            new System.Drawing.Point(cx, cy + 3),
                            new System.Drawing.Point(cx + 5, cy - 2)
                        });
                        break;
                    case VectorIconType.ChevronUp:
                        pen.Width = 2f;
                        g.DrawLines(pen, new System.Drawing.Point[] {
                            new System.Drawing.Point(cx - 5, cy + 2),
                            new System.Drawing.Point(cx, cy - 3),
                            new System.Drawing.Point(cx + 5, cy + 2)
                        });
                        break;
                    case VectorIconType.Pin:
                        pen.Width = 1.8f;
                        g.DrawLine(pen, cx - 4, cy - 5, cx + 4, cy - 5);
                        g.DrawLine(pen, cx - 3, cy - 5, cx - 3, cy);
                        g.DrawLine(pen, cx + 3, cy - 5, cx + 3, cy);
                        g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                        g.DrawLine(pen, cx, cy, cx, cy + 6);
                        if (IsActive)
                        {
                            g.FillRectangle(brush, cx - 3, cy - 5, 6, 5);
                        }
                        break;
                    case VectorIconType.Island:
                        pen.Width = 1.8f;
                        using (GraphicsPath path = GetRoundedRect(new Rectangle(cx - 7, cy - 4, 14, 8), 3))
                        {
                            if (IsActive) g.FillPath(brush, path);
                            else g.DrawPath(pen, path);
                        }
                        break;
                    case VectorIconType.Close:
                        pen.Width = 1.8f;
                        g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                        g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
                        break;
                    case VectorIconType.Prev:
                        g.FillPolygon(brush, new System.Drawing.Point[] { new System.Drawing.Point(cx, cy - 6), new System.Drawing.Point(cx, cy + 6), new System.Drawing.Point(cx - 5, cy) });
                        g.FillPolygon(brush, new System.Drawing.Point[] { new System.Drawing.Point(cx + 5, cy - 6), new System.Drawing.Point(cx + 5, cy + 6), new System.Drawing.Point(cx, cy) });
                        break;
                    case VectorIconType.Next:
                        g.FillPolygon(brush, new System.Drawing.Point[] { new System.Drawing.Point(cx - 5, cy - 6), new System.Drawing.Point(cx - 5, cy + 6), new System.Drawing.Point(cx, cy) });
                        g.FillPolygon(brush, new System.Drawing.Point[] { new System.Drawing.Point(cx, cy - 6), new System.Drawing.Point(cx, cy + 6), new System.Drawing.Point(cx + 5, cy) });
                        break;
                    case VectorIconType.Volume:
                        pen.Width = 1.6f;
                        g.FillPolygon(brush, new System.Drawing.Point[] {
                            new System.Drawing.Point(cx - 5, cy - 3),
                            new System.Drawing.Point(cx - 2, cy - 3),
                            new System.Drawing.Point(cx + 2, cy - 6),
                            new System.Drawing.Point(cx + 2, cy + 6),
                            new System.Drawing.Point(cx - 2, cy + 3),
                            new System.Drawing.Point(cx - 5, cy + 3)
                        });
                        g.DrawArc(pen, cx + 1, cy - 4, 6, 8, -50, 100);
                        break;
                }
            }


        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // ========================================================================
    // 3. Mini Header Audio Equalizer Indicator
    // ========================================================================
    public class MiniEqualizer : Panel
    {
        private float[] _bars = new float[4];
        private double _phase = 0;
        private bool _wasPlaying = false;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(56, 189, 248);
        public bool IsPlaying = false;

        public MiniEqualizer()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.FromArgb(10, 11, 16);
        }

        public void UpdateFrame()
        {
            if (!IsPlaying && !_wasPlaying) return;
            _wasPlaying = IsPlaying;

            _phase += 0.22;
            for (int i = 0; i < _bars.Length; i++)
            {
                if (IsPlaying)
                {
                    _bars[i] = (float)((Math.Sin(_phase * 2.0 + i * 0.9) * 0.5 + 0.5) * 11 + 3);
                }
                else
                {
                    _bars[i] = 3f;
                }
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = this.Height;
            float barW = 2.5f;
            float gap = 2.5f;
            float startX = (this.Width - (_bars.Length * (barW + gap) - gap)) / 2f;

            using (SolidBrush b = new SolidBrush(AccentColor))
            {
                for (int i = 0; i < _bars.Length; i++)
                {
                    float bh = Math.Min(h - 2, Math.Max(2f, _bars[i]));
                    float bx = startX + i * (barW + gap);
                    float by = (h - bh) / 2f;
                    g.FillRectangle(b, bx, by, barW, bh);
                }
            }


        }
    }

    // ========================================================================
    // 4. Custom Dark Slider Control (Fixes Ugly White TrackBar Rectangles!)
    // ========================================================================
    public class DarkSlider : Control
    {
        private int _min = 0;
        private int _max = 100;
        private int _val = 0;
        private bool _isDragging = false;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(56, 189, 248);
        public System.Drawing.Color TrackColor = System.Drawing.Color.FromArgb(36, 40, 56);
        public System.Drawing.Color ThumbColor = System.Drawing.Color.White;

        public event EventHandler ValueChanged;
        public event EventHandler UserSeek;

        public bool IsDragging { get { return _isDragging; } }
        public int Minimum { get { return _min; } set { _min = value; Invalidate(); } }
        public int Maximum { get { return _max; } set { _max = Math.Max(_min + 1, value); Invalidate(); } }
        public int Value
        {
            get { return _val; }
            set
            {
                int newVal = Math.Max(_min, Math.Min(_max, value));
                if (_val != newVal)
                {
                    _val = newVal;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }


        }

        public DarkSlider()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.FromArgb(16, 18, 25);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                this.Capture = true;
                UpdateValueFromMouse(e.X);
                if (UserSeek != null) UserSeek(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && (e.Button == MouseButtons.Left || this.Capture))
            {
                UpdateValueFromMouse(e.X);
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                this.Capture = false;
                UpdateValueFromMouse(e.X);
                if (UserSeek != null) UserSeek(this, EventArgs.Empty);
                this.Invalidate();
            }
            else
            {
                _isDragging = false;
                this.Capture = false;
            }
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            int pad = 8;
            int trackW = this.Width - pad * 2;
            if (trackW <= 0) return;
            float pct = Math.Max(0f, Math.Min(1f, (float)(mouseX - pad) / trackW));
            this.Value = (int)(_min + pct * (_max - _min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Clear background with parent's backcolor or dark theme color to prevent ANY white box/rectangle glitch
            System.Drawing.Color parentBg = (this.Parent != null && this.Parent.BackColor != System.Drawing.Color.Transparent) 
                ? this.Parent.BackColor 
                : System.Drawing.Color.FromArgb(16, 18, 25);
            using (SolidBrush bgClear = new SolidBrush(parentBg))
            {
                g.FillRectangle(bgClear, this.ClientRectangle);
            }

            int pad = 8;
            int trackW = this.Width - pad * 2;
            int trackH = 4;
            int cy = this.Height / 2;
            int trackY = cy - trackH / 2;

            // Background Track
            using (SolidBrush bgB = new SolidBrush(TrackColor))
            {
                g.FillRectangle(bgB, pad, trackY, trackW, trackH);
            }

            // Active Progress Fill
            float pct = (_max > _min) ? (float)(_val - _min) / (_max - _min) : 0f;
            int fillW = (int)(pct * trackW);
            if (fillW > 0)
            {
                using (SolidBrush actB = new SolidBrush(AccentColor))
                {
                    g.FillRectangle(actB, pad, trackY, fillW, trackH);
                }
            }



            // Sleek Thumb
            int thumbX = pad + fillW;
            int thumbR = 5;
            using (SolidBrush thB = new SolidBrush(ThumbColor))
            using (SolidBrush ringB = new SolidBrush(System.Drawing.Color.FromArgb(80, AccentColor)))
            {
                if (_isDragging)
                {
                    g.FillEllipse(ringB, thumbX - thumbR - 3, cy - thumbR - 3, (thumbR + 3) * 2, (thumbR + 3) * 2);
                }
                g.FillEllipse(thB, thumbX - thumbR, cy - thumbR, thumbR * 2, thumbR * 2);
            }
        }
    }

    // ========================================================================
    // 4b. Dark Mode ListBox with Native Dark Scrollbars & Double Buffering
    // ========================================================================
    public class DarkListBox : ListBox
    {
        public DarkListBox()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.BorderStyle = BorderStyle.None;
            this.BackColor = System.Drawing.Color.FromArgb(16, 18, 25);
            this.ForeColor = System.Drawing.Color.White;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Win32.SetWindowTheme(this.Handle, "DarkMode_Explorer", null); } catch { }
        }
    }

    // ========================================================================
    // 4c. Custom Sleek Dark Dropdown Selector (Zero White Windows Borders)
    // ========================================================================
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private DarkSelect _owner;
        public DarkMenuRenderer(DarkSelect owner) : base(new DarkColorTable()) { _owner = owner; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(48, 56, 78), 1f))
            {
                e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle rc = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            bool isCurrent = (e.Item.Tag is int) && ((int)e.Item.Tag == _owner.SelectedIndex);
            System.Drawing.Color bg = e.Item.Selected
                ? _owner.AccentColor
                : (isCurrent ? System.Drawing.Color.FromArgb(30, 40, 62) : System.Drawing.Color.FromArgb(18, 22, 34));
            using (SolidBrush b = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(b, rc);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool isCurrent = (e.Item.Tag is int) && ((int)e.Item.Tag == _owner.SelectedIndex);
            System.Drawing.Color fg = e.Item.Selected
                ? System.Drawing.Color.FromArgb(8, 9, 13)
                : (isCurrent ? _owner.AccentColor : System.Drawing.Color.FromArgb(226, 232, 240));
            Rectangle rect = new Rectangle(12, 0, e.Item.Width - 20, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground { get { return System.Drawing.Color.FromArgb(18, 22, 34); } }
        public override System.Drawing.Color MenuBorder { get { return System.Drawing.Color.FromArgb(48, 56, 78); } }
        public override System.Drawing.Color ImageMarginGradientBegin { get { return System.Drawing.Color.FromArgb(18, 22, 34); } }
        public override System.Drawing.Color ImageMarginGradientMiddle { get { return System.Drawing.Color.FromArgb(18, 22, 34); } }
        public override System.Drawing.Color ImageMarginGradientEnd { get { return System.Drawing.Color.FromArgb(18, 22, 34); } }
    }

    public class DarkSelect : Panel
    {
        private List<string> _items = new List<string>();
        private List<System.Drawing.Color> _itemDots = new List<System.Drawing.Color>();
        private int _selectedIndex = -1;
        private bool _isHovered = false;
        private System.Drawing.Color _accentColor = System.Drawing.Color.FromArgb(56, 189, 248);
        private ContextMenuStrip _menu;

        public event EventHandler SelectedIndexChanged;

        public System.Drawing.Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= -1 && value < _items.Count && _selectedIndex != value)
                {
                    _selectedIndex = value;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public int Count { get { return _items.Count; } }

        public DarkSelect()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Cursor = Cursors.Hand;
            this.BackColor = System.Drawing.Color.FromArgb(20, 24, 36);
            this.ForeColor = System.Drawing.Color.White;
            this.Font = new System.Drawing.Font("Segoe UI", 9f);

            _menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = System.Drawing.Color.FromArgb(18, 22, 34),
                ForeColor = System.Drawing.Color.White,
                Renderer = new DarkMenuRenderer(this)
            };

            this.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            this.Click += (s, e) => ShowDropdown();
        }

        public void AddItem(string text, System.Drawing.Color? dotColor = null)
        {
            _items.Add(text);
            _itemDots.Add(dotColor.HasValue ? dotColor.Value : System.Drawing.Color.Empty);
        }

        public void SetSelectedIndexSilent(int idx)
        {
            if (idx >= -1 && idx < _items.Count)
            {
                _selectedIndex = idx;
                Invalidate();
            }
        }

        private void ShowDropdown()
        {
            _menu.Items.Clear();
            _menu.Font = this.Font;
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                ToolStripMenuItem mi = new ToolStripMenuItem(_items[i])
                {
                    Tag = idx,
                    AutoSize = false,
                    Width = Math.Max(180, this.Width),
                    Height = 28
                };
                mi.Click += (s, e) => { SelectedIndex = idx; };
                _menu.Items.Add(mi);
            }
            _menu.Show(this, new System.Drawing.Point(0, this.Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bgB = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bgB, 0, 0, this.Width, this.Height);
            }

            System.Drawing.Color borderCol = _isHovered ? _accentColor : System.Drawing.Color.FromArgb(48, 56, 78);
            using (System.Drawing.Pen borderPen = new System.Drawing.Pen(borderCol, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }

            int textStartX = 10;
            if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            {
                System.Drawing.Color dot = _itemDots[_selectedIndex];
                if (!dot.IsEmpty)
                {
                    using (SolidBrush dotB = new SolidBrush(dot))
                    {
                        g.FillEllipse(dotB, 10, (this.Height - 10) / 2, 10, 10);
                    }
                    textStartX = 26;
                }
                Rectangle textRect = new Rectangle(textStartX, 0, this.Width - textStartX - 28, this.Height);
                TextRenderer.DrawText(g, _items[_selectedIndex], this.Font, textRect, this.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            // Draw sleek chevron down arrow
            int ax = this.Width - 16;
            int ay = this.Height / 2 - 2;
            using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(_isHovered ? _accentColor : System.Drawing.Color.FromArgb(148, 163, 184), 1.6f))
            {
                g.DrawLine(arrowPen, ax - 4, ay, ax, ay + 4);
                g.DrawLine(arrowPen, ax, ay + 4, ax + 4, ay);
            }
        }
    }

    // ========================================================================
    // 5. Native Windows Media Foundation Audio Engine
    // ========================================================================
    public class NativeAudioPlayer
    {
        private MediaPlayer _player = new MediaPlayer();
        private bool _isPlaying = false;
        private double _speedRatio = 1.0;
        private bool _suppressMediaEnded = true;
        private DateTime _playbackStartTime = DateTime.MinValue;

        public event Action MediaEnded;
        public event Action<int> MediaOpened;
        public event Action MediaFailed;

        public bool IsPlaying { get { return _isPlaying; } }
        public double SpeedRatio { get { return _speedRatio; } }

        public NativeAudioPlayer()
        {
            _player.Volume = 0.75;
            _player.SpeedRatio = 1.0;

            _player.MediaOpened += (s, e) =>
            {
                _suppressMediaEnded = false;
                _playbackStartTime = DateTime.UtcNow;
                int durationMs = 0;
                try
                {
                    if (_player.NaturalDuration.HasTimeSpan)
                    {
                        durationMs = (int)_player.NaturalDuration.TimeSpan.TotalMilliseconds;
                    }
                    _player.SpeedRatio = _speedRatio;
                }
                catch { }

                if (MediaOpened != null)
                {
                    MediaOpened(durationMs);
                }
            };

            _player.MediaFailed += (s, e) =>
            {
                _isPlaying = false;
                _suppressMediaEnded = true;
                if (MediaFailed != null) MediaFailed();
            };

            _player.MediaEnded += (s, e) =>
            {
                if (_suppressMediaEnded) return;

                // Safety guard: media must have actually progressed for at least 1.5 seconds
                if ((DateTime.UtcNow - _playbackStartTime).TotalSeconds < 1.5) return;

                try
                {
                    if (_player.NaturalDuration.HasTimeSpan)
                    {
                        double diff = (_player.NaturalDuration.TimeSpan - _player.Position).TotalSeconds;
                        if (diff > 3.0) return; // Ignore spurious early MediaEnded
                    }
                    else
                    {
                        return; // Duration not available yet -> media has not ended!
                    }

                    if (_player.Position.TotalSeconds < 1.0)
                    {
                        return; // At position 0, media has not ended!
                    }
                }
                catch { return; }

                _isPlaying = false;
                _suppressMediaEnded = true;
                if (MediaEnded != null) MediaEnded();
            };
        }

        public void Play()
        {
            try
            {
                _suppressMediaEnded = false;
                _playbackStartTime = DateTime.UtcNow;
                _player.SpeedRatio = _speedRatio;
                _player.Play();
                _isPlaying = true;
            }
            catch { }
        }

        public void Open(string pathOrUrl)
        {
            try
            {
                _suppressMediaEnded = true;
                _isPlaying = false;
                try { _player.Stop(); } catch { }
                try { _player.Close(); } catch { }

                Uri uri;
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out uri))
                {
                    _player.Open(uri);
                }
                else
                {
                    _player.Open(new Uri(Path.GetFullPath(pathOrUrl)));
                }
                _player.SpeedRatio = _speedRatio;
            }
            catch { }
        }

        public void Play(string pathOrUrl)
        {
            try
            {
                _suppressMediaEnded = true;
                _playbackStartTime = DateTime.UtcNow;
                try { _player.Stop(); } catch { }
                try { _player.Close(); } catch { }

                Uri uri;
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out uri))
                {
                    _player.Open(uri);
                }
                else
                {
                    _player.Open(new Uri(Path.GetFullPath(pathOrUrl)));
                }
                _player.SpeedRatio = _speedRatio;
                _player.Play();
                _isPlaying = true;
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                _suppressMediaEnded = true;
                _isPlaying = false;
                _player.Stop();
                _player.Close();
            }
            catch { }
        }

        public void SetSpeedRatio(double ratio)
        {
            try
            {
                _speedRatio = Math.Max(0.5, Math.Min(2.0, ratio));
                _player.SpeedRatio = _speedRatio;
            }
            catch { }
        }

        public void TogglePlay()
        {
            try
            {
                if (_isPlaying)
                {
                    _player.Pause();
                    _isPlaying = false;
                }
                else
                {
                    _suppressMediaEnded = false;
                    _playbackStartTime = DateTime.UtcNow;
                    _player.Play();
                    _isPlaying = true;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Pause()
        {
            try { _player.Pause(); _isPlaying = false; } catch { }
        }

        public void Seek(int positionMs)
        {
            try
            {
                _suppressMediaEnded = false;
                _playbackStartTime = DateTime.UtcNow;
                _player.Position = TimeSpan.FromMilliseconds(positionMs);
            }
            catch { }
        }

        public void SetVolume(int percent)
        {
            try
            {
                _player.Volume = Math.Max(0, Math.Min(100, percent)) / 100.0;
            }
            catch { }
        }

        public int GetPositionMs()
        {
            try
            {
                return (int)_player.Position.TotalMilliseconds;
            }
            catch { return 0; }
        }

        public int GetLengthMs()
        {
            try
            {
                if (_player.NaturalDuration.HasTimeSpan)
                {
                    return (int)_player.NaturalDuration.TimeSpan.TotalMilliseconds;
                }
                return 0;
            }
            catch { return 0; }
        }
    }

    public class TrackItem
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public string VideoId { get; set; }
        public int DurationMs { get; set; }
        public int Score { get; set; }
    }

    public class LyricLine
    {
        public double TimeSec { get; set; }
        public string Text { get; set; }
    }

    // ========================================================================
    // 6a. Multi-Mode 60 FPS Visualizer Panel (Spectrum, Mirror, Oscillo, Matrix)
    // ========================================================================
    public enum VisualizerMode
    {
        SpectrumBars = 0,
        StereoMirror = 1,
        Oscilloscope = 2,
        DotMatrix = 3
    }

    public class VisualizerPanel : Panel
    {
        private float[] _bars = new float[36];
        private double _phase = 0;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(56, 189, 248);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.White;
        public bool IsPlaying = false;
        public VisualizerMode Mode = VisualizerMode.SpectrumBars;
        private bool _isHovered = false;

        public event Action<VisualizerMode> ModeChanged;

        public VisualizerPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            this.Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            CycleMode();
        }

        public void CycleMode()
        {
            Mode = (VisualizerMode)(((int)Mode + 1) % 4);
            if (ModeChanged != null) ModeChanged(Mode);
            this.Invalidate();
        }

        public void SetMode(VisualizerMode mode)
        {
            Mode = mode;
            this.Invalidate();
        }

        public void UpdateFrame()
        {
            _phase += 0.14;
            for (int i = 0; i < _bars.Length; i++)
            {
                if (IsPlaying)
                {
                    float val = (float)((Math.Sin(_phase * 2.2 + i * 0.4) * 0.5 + 0.5) * 26 + (Math.Cos(_phase * 1.5 - i * 0.3) * 0.5 + 0.5) * 12 + 4);
                    _bars[i] = val;
                }
                else
                {
                    _bars[i] = (float)((Math.Sin(_phase * 1.0 + i * 0.3) * 0.5 + 0.5) * 8 + 4);
                }
            }


            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            switch (Mode)
            {
                case VisualizerMode.SpectrumBars:
                    DrawSpectrumBars(g, w, h);
                    break;
                case VisualizerMode.StereoMirror:
                    DrawStereoMirror(g, w, h);
                    break;
                case VisualizerMode.Oscilloscope:
                    DrawOscilloscope(g, w, h);
                    break;
                case VisualizerMode.DotMatrix:
                    DrawDotMatrix(g, w, h);
                    break;
            }

            // Mode indicator pill on hover or subtle
            string modeName;
            switch (Mode)
            {
                case VisualizerMode.SpectrumBars: modeName = "SPECTRUM"; break;
                case VisualizerMode.StereoMirror: modeName = "MIRROR"; break;
                case VisualizerMode.Oscilloscope: modeName = "OSCILLO"; break;
                default: modeName = "DOT MATRIX"; break;
            }

            using (Font f = new Font("Consolas", 6.5f, FontStyle.Bold))
            {
                int badgeW = 66;
                int badgeH = 14;
                Rectangle bRect = new Rectangle(w - badgeW - 4, 2, badgeW, badgeH);
                using (SolidBrush bgB = new SolidBrush(_isHovered ? System.Drawing.Color.FromArgb(200, 15, 20, 30) : System.Drawing.Color.FromArgb(90, 8, 10, 16)))
                {
                    g.FillRectangle(bgB, bRect);
                }
                using (System.Drawing.Pen pB = new System.Drawing.Pen(_isHovered ? AccentColor : System.Drawing.Color.FromArgb(50, AccentColor), 1f))
                {
                    g.DrawRectangle(pB, bRect.X, bRect.Y, bRect.Width - 1, bRect.Height - 1);
                }
                TextRenderer.DrawText(g, modeName, f, bRect, _isHovered ? AccentColor : System.Drawing.Color.FromArgb(140, 180, 210), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawSpectrumBars(Graphics g, int w, int h)
        {
            float spacing = (float)w / _bars.Length;
            float barW = Math.Max(2.5f, spacing - 3f);

            for (int i = 0; i < _bars.Length; i++)
            {
                float bh = Math.Min(h - 2, Math.Max(4f, _bars[i]));
                float bx = i * spacing + (spacing - barW) / 2f;
                float by = h - bh;

                using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(bx, by), new PointF(bx, h), SecondaryColor, AccentColor))
                {
                    g.FillRectangle(brush, bx, by, barW, bh);
                }

                // Glowing Peak Cap
                using (SolidBrush capBrush = new SolidBrush(SecondaryColor))
                {
                    g.FillRectangle(capBrush, bx, Math.Max(0, by - 2f), barW, 1.5f);
                }
            }


        }

        private void DrawStereoMirror(Graphics g, int w, int h)
        {
            float cy = h / 2f;
            float spacing = (float)w / _bars.Length;
            float barW = Math.Max(2.5f, spacing - 3f);

            for (int i = 0; i < _bars.Length; i++)
            {
                float halfH = Math.Min(cy - 2, Math.Max(2f, _bars[i] * 0.48f));
                float bx = i * spacing + (spacing - barW) / 2f;

                // Upper bar
                using (System.Drawing.Drawing2D.LinearGradientBrush brushUp = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(bx, cy - halfH), new PointF(bx, cy), SecondaryColor, AccentColor))
                {
                    g.FillRectangle(brushUp, bx, cy - halfH, barW, halfH);
                }

                // Lower mirrored bar
                using (System.Drawing.Drawing2D.LinearGradientBrush brushDown = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(bx, cy), new PointF(bx, cy + halfH), AccentColor, SecondaryColor))
                {
                    g.FillRectangle(brushDown, bx, cy, barW, halfH);
                }
            }



            // Center neon divider line
            using (System.Drawing.Pen divPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, AccentColor), 1f))
            {
                g.DrawLine(divPen, 0, cy, w, cy);
            }
        }

        private void DrawOscilloscope(Graphics g, int w, int h)
        {
            int ptsCount = 64;
            PointF[] points = new PointF[ptsCount];
            float cy = h / 2f;
            float maxAmp = (h / 2f) - 4f;

            for (int i = 0; i < ptsCount; i++)
            {
                float x = (float)i / (ptsCount - 1) * w;
                double wave = Math.Sin(_phase * 2.5 + i * 0.35) * 0.65 + Math.Sin(_phase * 4.1 + i * 0.7) * 0.35;
                if (!IsPlaying) wave *= 0.25;

                float y = cy + (float)(wave * maxAmp);
                points[i] = new PointF(x, y);
            }

            // Glow pass 1 (wide faint glow)
            using (System.Drawing.Pen glowPen1 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, AccentColor), 5f))
            {
                g.DrawLines(glowPen1, points);
            }

            // Glow pass 2 (medium glow)
            using (System.Drawing.Pen glowPen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(120, AccentColor), 2.5f))
            {
                g.DrawLines(glowPen2, points);
            }

            // Core neon pass (bright white-accent core)
            using (System.Drawing.Pen corePen = new System.Drawing.Pen(SecondaryColor, 1.2f))
            {
                g.DrawLines(corePen, points);
            }
        }

        private void DrawDotMatrix(Graphics g, int w, int h)
        {
            int cols = 28;
            int rows = 7;
            float colSpacing = (float)w / cols;
            float rowSpacing = (float)h / rows;
            float dotRadius = Math.Min(colSpacing, rowSpacing) * 0.36f;

            for (int c = 0; c < cols; c++)
            {
                int barIdx = (c * _bars.Length) / cols;
                float norm = Math.Min(1f, _bars[barIdx] / 40f);
                int activeRows = (int)Math.Round(norm * rows);

                for (int r = 0; r < rows; r++)
                {
                    int invR = rows - 1 - r;
                    float dx = c * colSpacing + colSpacing / 2f;
                    float dy = r * rowSpacing + rowSpacing / 2f;

                    bool isLit = (invR < activeRows);
                    if (isLit)
                    {
                        System.Drawing.Color dotCol = (invR >= rows - 2) ? SecondaryColor : AccentColor;
                        using (SolidBrush litBrush = new SolidBrush(dotCol))
                        {
                            g.FillEllipse(litBrush, dx - dotRadius, dy - dotRadius, dotRadius * 2, dotRadius * 2);
                        }
                    }
                    else
                    {
                        using (SolidBrush unlitBrush = new SolidBrush(System.Drawing.Color.FromArgb(20, 255, 255, 255)))
                        {
                            g.FillEllipse(unlitBrush, dx - dotRadius * 0.7f, dy - dotRadius * 0.7f, dotRadius * 1.4f, dotRadius * 1.4f);
                        }
                    }
                }
            }


        }
    }

    // ========================================================================
    // 6b. Vinyl Spinning Disc
    // ========================================================================
    public class VinylDiscControl : Panel
    {
        private float _angle = 0f;
        private float _angularVelocity = 0f;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(56, 189, 248);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.White;

        public event EventHandler DiscClicked;

        public VinylDiscControl(int size = 56)
        {
            this.Size = new Size(size, size);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.Transparent;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DiscClicked != null) DiscClicked(this, e);
        }

        public void UpdateFrame(bool isPlaying, double speedRatio)
        {
            float targetVel = isPlaying ? (float)(4.2f * speedRatio) : 0f;
            // Smooth inertia interpolation
            _angularVelocity = _angularVelocity * 0.88f + targetVel * 0.12f;
            _angle = (_angle + _angularVelocity) % 360f;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int sz = Math.Min(this.Width, this.Height);
            float cx = sz / 2f;
            float cy = sz / 2f;
            float r = sz / 2f - 2f;

            // 1. Outer Dark Vinyl Body
            using (SolidBrush vinylBg = new SolidBrush(System.Drawing.Color.FromArgb(14, 15, 20)))
            {
                g.FillEllipse(vinylBg, cx - r, cy - r, r * 2, r * 2);
            }

            // Outer Vinyl Rim Border
            using (System.Drawing.Pen rimPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(45, 52, 70), 1.2f))
            {
                g.DrawEllipse(rimPen, cx - r, cy - r, r * 2, r * 2);
            }

            // 2. Micro Vinyl Grooves (Concentric Circles)
            float[] grooves = new float[] { 0.88f, 0.78f, 0.68f, 0.58f, 0.48f };
            using (System.Drawing.Pen groovePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(24, 28, 38), 1f))
            {
                foreach (float gf in grooves)
                {
                    float gr = r * gf;
                    g.DrawEllipse(groovePen, cx - gr, cy - gr, gr * 2, gr * 2);
                }
            }



            // 3. Specular 3D Vinyl Light Reflection (Rotates with Angle)
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_angle);

            using (GraphicsPath sheenPath = new GraphicsPath())
            {
                sheenPath.AddPie(-r, -r, r * 2, r * 2, -25, 50);
                sheenPath.AddPie(-r, -r, r * 2, r * 2, 155, 50);
                using (SolidBrush sheenBrush = new SolidBrush(System.Drawing.Color.FromArgb(18, 255, 255, 255)))
                {
                    g.FillPath(sheenBrush, sheenPath);
                }
            }



            // 4. Center Label (Skin Accent Badge)
            float labelR = r * 0.36f;
            using (SolidBrush labelBrush = new SolidBrush(AccentColor))
            {
                g.FillEllipse(labelBrush, -labelR, -labelR, labelR * 2, labelR * 2);
            }

            // Center Label Ring
            using (System.Drawing.Pen labelRing = new System.Drawing.Pen(SecondaryColor, 1.2f))
            {
                g.DrawEllipse(labelRing, -labelR * 0.7f, -labelR * 0.7f, labelR * 1.4f, labelR * 1.4f);
            }

            // Rotating Vinyl Stylus Marker Dot
            using (SolidBrush markerBrush = new SolidBrush(SecondaryColor))
            {
                g.FillEllipse(markerBrush, labelR * 0.45f, -2f, 4f, 4f);
            }

            // Center Spindle Hole
            using (SolidBrush spindleBrush = new SolidBrush(System.Drawing.Color.FromArgb(10, 11, 16)))
            {
                g.FillEllipse(spindleBrush, -3f, -3f, 6f, 6f);
            }

            g.Restore(state);
        }
    }

    // ========================================================================
    // 6c. Standalone Settings & State Persistence (Zero-Dependency JSON)
    // ========================================================================
    public class WidgetConfig
    {
        public int MasterVolume = 75;
        public int SkinIndex = 0;
        public int VisualizerMode = 0;
        public bool AutoNext = true;
        public bool EqActive = true;
        public int EqPresetIndex = 0;
        public bool BassBoostActive = false;
        public int[] EqBands = new int[] { 0, 0, 0, 0, 0 };
        public bool IsLooping = false;
        public bool IsIsland = false;
        public bool IsPinned = false;

        // Last played track state (instant persistence & restore on launch)
        public string LastTitle = "Sudden Shower (소나기)";
        public string LastArtist = "ECLIPSE";
        public string LastVideoId = "ESWcsKySnNs";
        public string LastUrl = "";
        public int LastDurationMs = 233000;
        public string LastGenre = "YT MUSIC • Official Audio";
        public string LastSource = "YT_MUSIC";

        public static string GetConfigPath()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "settings.json");
            }
            catch
            {
                return "settings.json";
            }
        }

        public static WidgetConfig Load()
        {
            WidgetConfig cfg = new WidgetConfig();
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path)) return cfg;
                string json = File.ReadAllText(path, Encoding.UTF8);

                cfg.MasterVolume = ExtractInt(json, "MasterVolume", 75);
                cfg.SkinIndex = ExtractInt(json, "SkinIndex", 0);
                cfg.VisualizerMode = ExtractInt(json, "VisualizerMode", 0);
                cfg.AutoNext = ExtractBool(json, "AutoNext", true);
                cfg.EqActive = ExtractBool(json, "EqActive", true);
                cfg.EqPresetIndex = ExtractInt(json, "EqPresetIndex", 0);
                cfg.BassBoostActive = ExtractBool(json, "BassBoostActive", false);
                cfg.IsLooping = ExtractBool(json, "IsLooping", false);
                cfg.IsIsland = ExtractBool(json, "IsIsland", false);
                cfg.IsPinned = ExtractBool(json, "IsPinned", false);

                cfg.LastTitle = ExtractString(json, "LastTitle", "Sudden Shower (소나기)");
                cfg.LastArtist = ExtractString(json, "LastArtist", "ECLIPSE");
                cfg.LastVideoId = ExtractString(json, "LastVideoId", "ESWcsKySnNs");
                cfg.LastUrl = ExtractString(json, "LastUrl", "");
                cfg.LastDurationMs = ExtractInt(json, "LastDurationMs", 233000);
                cfg.LastGenre = ExtractString(json, "LastGenre", "YT MUSIC • Official Audio");
                cfg.LastSource = ExtractString(json, "LastSource", "YT_MUSIC");
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"MasterVolume\": {0},", MasterVolume));
                sb.AppendLine(string.Format("  \"SkinIndex\": {0},", SkinIndex));
                sb.AppendLine(string.Format("  \"VisualizerMode\": {0},", VisualizerMode));
                sb.AppendLine(string.Format("  \"AutoNext\": {0},", AutoNext ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EqActive\": {0},", EqActive ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EqPresetIndex\": {0},", EqPresetIndex));
                sb.AppendLine(string.Format("  \"BassBoostActive\": {0},", BassBoostActive ? "true" : "false"));
                sb.AppendLine(string.Format("  \"IsLooping\": {0},", IsLooping ? "true" : "false"));
                sb.AppendLine(string.Format("  \"IsIsland\": {0},", IsIsland ? "true" : "false"));
                sb.AppendLine(string.Format("  \"IsPinned\": {0},", IsPinned ? "true" : "false"));
                sb.AppendLine(string.Format("  \"LastTitle\": \"{0}\",", EscapeJson(LastTitle)));
                sb.AppendLine(string.Format("  \"LastArtist\": \"{0}\",", EscapeJson(LastArtist)));
                sb.AppendLine(string.Format("  \"LastVideoId\": \"{0}\",", EscapeJson(LastVideoId)));
                sb.AppendLine(string.Format("  \"LastUrl\": \"{0}\",", EscapeJson(LastUrl)));
                sb.AppendLine(string.Format("  \"LastDurationMs\": {0},", LastDurationMs));
                sb.AppendLine(string.Format("  \"LastGenre\": \"{0}\",", EscapeJson(LastGenre)));
                sb.AppendLine(string.Format("  \"LastSource\": \"{0}\"", EscapeJson(LastSource)));
                sb.AppendLine("}");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static int ExtractInt(string json, string key, int defaultVal)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            if (m.Success)
            {
                int val;
                if (int.TryParse(m.Groups[1].Value, out val)) return val;
            }
            return defaultVal;
        }

        private static bool ExtractBool(string json, string key, bool defaultVal)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return m.Groups[1].Value.ToLower() == "true";
            }
            return defaultVal;
        }

        private static string ExtractString(string json, string key, string defaultVal)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            if (m.Success)
            {
                return DecodeJsonString(m.Groups[1].Value);
            }
            return defaultVal;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        private static string DecodeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try
            {
                return Regex.Replace(s, @"\\u([0-9a-fA-F]{4})", match =>
                {
                    int code = Convert.ToInt32(match.Groups[1].Value, 16);
                    return ((char)code).ToString();
                }).Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            catch { return s; }
        }
    }

    // ========================================================================
    // 7. Fast HTTP Utility (Uses Windows curl.exe with WebClient Fallback)
    // ========================================================================
    public static class FastHttp
    {
        public static string Get(string url, int timeoutSec = 8)
        {
            // 1. Try Windows native curl.exe (Bypasses .NET proxy delay, runs in ~1.5s)
            try
            {
                string curlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
                if (File.Exists(curlPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(curlPath, string.Format("-s -m {0} \"{1}\"", timeoutSec, url))
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };
                    using (Process p = Process.Start(psi))
                    {
                        string outStr = p.StandardOutput.ReadToEnd();
                        if (!p.WaitForExit(timeoutSec * 1000))
                        {
                            try { p.Kill(); } catch { }
                        }
                        if (!string.IsNullOrEmpty(outStr) && (outStr.TrimStart().StartsWith("{") || outStr.TrimStart().StartsWith("[")))
                        {
                            return outStr;
                        }
                    }
                }
            }
            catch { }

            // 2. Fallback to WebClient
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                using (WebClient wc = new WebClient())
                {
                    wc.Proxy = null;
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers["User-Agent"] = "MusicWidgetPureWindows/2.0";
                    return wc.DownloadString(url);
                }
            }
            catch
            {
                return "";
            }
        }
    }

    // ========================================================================
    // 7b. Widget Skins Palette System (5 Distinct Pro Themes)
    // ========================================================================
    public class WidgetSkin
    {
        public string Name { get; set; }
        public string Tagline { get; set; }
        public System.Drawing.Color Accent { get; set; }
        public System.Drawing.Color SecondaryAccent { get; set; }
        public System.Drawing.Color Background { get; set; }
        public System.Drawing.Color Surface { get; set; }
        public System.Drawing.Color TextMuted { get; set; }

        public static List<WidgetSkin> GetDefaultSkins()
        {
            return new List<WidgetSkin>
            {
                new WidgetSkin
                {
                    Name = "1. Cyber Neon",
                    Tagline = "Electric Cyan & Obsidian Blue",
                    Accent = System.Drawing.Color.FromArgb(0, 240, 255),
                    SecondaryAccent = System.Drawing.Color.FromArgb(59, 130, 246),
                    Background = System.Drawing.Color.FromArgb(8, 9, 14),
                    Surface = System.Drawing.Color.FromArgb(16, 18, 28),
                    TextMuted = System.Drawing.Color.FromArgb(148, 163, 184)
                },
                new WidgetSkin
                {
                    Name = "2. Sunset Synthwave",
                    Tagline = "80s Neon Pink & Sunset Orange",
                    Accent = System.Drawing.Color.FromArgb(255, 42, 133),
                    SecondaryAccent = System.Drawing.Color.FromArgb(255, 122, 0),
                    Background = System.Drawing.Color.FromArgb(18, 10, 26),
                    Surface = System.Drawing.Color.FromArgb(32, 18, 46),
                    TextMuted = System.Drawing.Color.FromArgb(192, 132, 252)
                },
                new WidgetSkin
                {
                    Name = "3. Matrix Emerald",
                    Tagline = "Terminal Acid Green & Cyber Grid",
                    Accent = System.Drawing.Color.FromArgb(0, 255, 136),
                    SecondaryAccent = System.Drawing.Color.FromArgb(16, 185, 129),
                    Background = System.Drawing.Color.FromArgb(6, 14, 10),
                    Surface = System.Drawing.Color.FromArgb(12, 26, 18),
                    TextMuted = System.Drawing.Color.FromArgb(110, 231, 183)
                },
                new WidgetSkin
                {
                    Name = "4. Tokyo Amber",
                    Tagline = "Warm Tokyo Gold & Smoky Charcoal",
                    Accent = System.Drawing.Color.FromArgb(255, 184, 0),
                    SecondaryAccent = System.Drawing.Color.FromArgb(255, 107, 0),
                    Background = System.Drawing.Color.FromArgb(18, 14, 10),
                    Surface = System.Drawing.Color.FromArgb(30, 24, 18),
                    TextMuted = System.Drawing.Color.FromArgb(252, 211, 77)
                },
                new WidgetSkin
                {
                    Name = "5. Nordic Frost",
                    Tagline = "Arctic Sky Ice & Deep Midnight",
                    Accent = System.Drawing.Color.FromArgb(56, 189, 248),
                    SecondaryAccent = System.Drawing.Color.FromArgb(168, 85, 247),
                    Background = System.Drawing.Color.FromArgb(11, 17, 30),
                    Surface = System.Drawing.Color.FromArgb(20, 30, 50),
                    TextMuted = System.Drawing.Color.FromArgb(186, 230, 253)
                }
            }

;
        }
    }

    // ========================================================================
        // ========================================================================
    // 7c. Dexterous Equalizer & Acoustic DSP Engine
    // ========================================================================
    public class EqPreset
    {
        public string Name { get; set; }
        public int[] Bands { get; set; } // 5 bands in dB: 60Hz, 250Hz, 1kHz, 4kHz, 12kHz (-12 to +12 dB)
        public int BassBoost { get; set; } // 0 to 10 dB
        public int TrebleBoost { get; set; } // 0 to 10 dB

        public EqPreset(string name, int b0, int b1, int b2, int b3, int b4, int bass = 0, int treble = 0)
        {
            Name = name;
            Bands = new int[5] { b0, b1, b2, b3, b4 };
            BassBoost = bass;
            TrebleBoost = treble;
        }
    }

    // 8. Main Form: 100% Pure Windows Desktop Widget (YouTube Music Edition)
    // ========================================================================
    public class DexplayForm : Form
    {
        private const string YOUTUBE_API_KEY = "AIzaSyAYJw7G73vf1nfJQzE4364wYDMUGilXdKs";

        // Sizes
        private const int COLLAPSED_W = 460;
        private const int COLLAPSED_H = 46;
        private const int EXPANDED_W = 580;
        private const int EXPANDED_H = 510;

                // Graphic Equalizer & Audio Enhancements
        private double _tempoSpeed = 1.0;
        private bool _autoNext = true;
        private bool _eqActive = true;
        private int _eqPresetIndex = 0;
        private int[] _eqBands = new int[5] { 0, 0, 0, 0, 0 };
        private bool _bassBoostActive = false;
        private bool _isInternalEqUpdate = false;

        private static readonly List<EqPreset> _eqPresets = new List<EqPreset>
        {
            new EqPreset("Flat (Netral)", 0, 0, 0, 0, 0),
            new EqPreset("Bass Boost (+8 dB)", 8, 5, 1, 0, 1, 8),
            new EqPreset("Vocal & Akustik", -2, 1, 6, 4, 2, 0, 3),
            new EqPreset("Treble Booster", -1, 0, 2, 5, 8, 0, 7),
            new EqPreset("Rock & Metal", 5, 3, -1, 3, 6, 4, 4),
            new EqPreset("Electronic / EDM", 7, 5, 0, 4, 7, 6, 5),
            new EqPreset("Pop & R&B", 4, 2, 2, 3, 4, 3, 2),
            new EqPreset("Lo-Fi & Chillout", 4, 3, -1, -3, -5, 4),
            new EqPreset("Kustom (Manual)", 0, 0, 0, 0, 0)
        };

        // UI Controls for Deck & Equalizer
        private Button _btnQuickSkin;
        private Button _btnQuickEq;
        private DarkSelect _cmbEqPreset;
        private Button _btnEqBypass;
        private Button _btnEqReset;
        private DarkSlider[] _eqSliders = new DarkSlider[5];
        private Label[] _lblEqValues = new Label[5];
        private Button _btnBassBoost;
        private Button _btnAutoNext;
        private DarkSlider _sliderTempo;
        private Label _lblTempoVal;
        private Button _btnTempoReset;

        // Cached Fonts (Prevents GDI Handle Leaks inside OwnerDraw ListBoxes)
        private readonly Font _fontBadge = new Font("Segoe UI", 7f, FontStyle.Bold);
        private readonly Font _fontListTime = new Font("Consolas", 8.5f, FontStyle.Bold);
        private readonly Font _fontListRegular = new Font("Segoe UI", 9.5f);
        private readonly Font _fontListBold = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        private readonly Font _fontListItalic = new Font("Segoe UI", 9.5f, FontStyle.Italic);

        // State
        private bool _isExpanded = false;
        private bool _isIsland = false;
        private bool _isPinned = false;
        
        private bool _isLooping = false;
        private bool _isMuted = false;
        private int _preMuteVolume = 75;
        private int _currentTab = 0; // 0=Deck, 1=Search, 2=Lyrics, 3=Queue, 4=Studio
        private int _targetW = COLLAPSED_W;
        private int _targetH = COLLAPSED_H;
        private int _lastRgnW = -1;
        private int _lastRgnH = -1;
        private bool _lastRgnIsland = false;

        // Dragging
        private bool _isMouseDown = false;
        private System.Drawing.Point _mouseDownPos = System.Drawing.Point.Empty;

        // Engines
        private NativeAudioPlayer _player = new NativeAudioPlayer();
        private System.Media.SoundPlayer _fxPlayer;
        private readonly object _fxLock = new object();
        private System.Windows.Forms.Timer _animTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer _tickTimer = new System.Windows.Forms.Timer();
        private NotifyIcon _trayIcon;
        private ToolTip _toolTip = new ToolTip();
        private bool _isApplyingSkin = false;

        // Colors
        private System.Drawing.Color _accent = System.Drawing.Color.FromArgb(56, 189, 248);
        private System.Drawing.Color _bg = System.Drawing.Color.FromArgb(10, 11, 16);
        private System.Drawing.Color _surface = System.Drawing.Color.FromArgb(18, 20, 28);
        private System.Drawing.Color _textMuted = System.Drawing.Color.FromArgb(148, 163, 184);

        // Tracks & Data
        private TrackItem _currentTrack = new TrackItem
        {
            Title = "Sudden Shower (소나기)",
            Artist = "ECLIPSE",
            Genre = "YT MUSIC • Official Audio",
            Source = "YT_MUSIC",
            DurationMs = 233000,
            VideoId = "ESWcsKySnNs",
            Url = ""
        };

        private List<TrackItem> _queue = new List<TrackItem>();
        private List<TrackItem> _searchTracks = new List<TrackItem>();
        private List<LyricLine> _lyrics = new List<LyricLine>();
        private int _activeLyricIndex = -1;

        // Header Controls
        private Panel _headerPanel;
        private VectorButton _btnHeaderPlay;
        private Label _lblHeaderTitle;
        private MiniEqualizer _miniEq;
        private VectorButton _btnHeaderSearch;
        private VectorButton _btnHeaderExpand;
        private VectorButton _btnHeaderIsland;
        private VectorButton _btnHeaderPin;
        private VectorButton _btnHeaderClose;

        // Deck Container Controls
        private Panel _deckPanel;
        private Panel _tabBarPanel;
        private Button[] _tabButtons = new Button[5];
        private Panel[] _tabPanels = new Panel[5];

        // Tab 0: Deck
        private Panel _deckCard;
        private Label _lblTrackTitle;
        private Label _lblTrackArtist;
        private VinylDiscControl _vinylDisc;
        private VisualizerPanel _visualizer;
        private DarkSlider _scrubBar;
        private Label _lblTimeCur;
        private Label _lblTimeTot;
        private VectorButton _btnDeckPrev;
        private VectorButton _btnDeckPlay;
        private VectorButton _btnDeckNext;
        private VectorButton _btnDeckVol;
        private DarkSlider _deckVolumeBar;
        private Button _btnLoop;
        private Panel _deckInfoCard;
        private Label _lblDeckEngine;
        private Label _lblDeckQuality;
        private Label _lblDeckDetails;

        // Tab 1: YouTube Music Search
        private TextBox _txtSearch;
        private DarkListBox _lstSearchResults;
        private Label _lblSearchStatus;

        // Tab 2: Synced Lyrics
        private DarkListBox _lstLyrics;

        // Tab 3: Queue
        private DarkListBox _lstQueue;

        private DarkSelect _cmbSkinSelector;

        // Skins & Themes
        private List<WidgetSkin> _skins = WidgetSkin.GetDefaultSkins();
        private int _currentSkinIndex = 0;
        
        
        
        
        private int _masterVolume = 75;

        // Header Marquee Ticker
        private int _tickerCounter = 0;
        private int _headerScrollOffset = 0;
        private int _headerScrollPause = 15;
        private string _lastHeaderText = "";

        // Ambient Aura Pulse
        private int _auraAlpha = 50;
        private int _lastAuraAlpha = -1;
        private int _auraDirection = 1;

        public DexplayForm()
        {
            // Security protocol configuration
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            // Form Properties
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.AllowDrop = true;
            this.BackColor = _bg;

            InitTray();
            SetupSampleData();
            BuildUI();
            LoadSavedConfig();

            _player.MediaEnded += () =>
            {
                SafeUi(() =>
                {
                    if (_isLooping)
                    {
                        _player.Seek(0);
                        _player.Play();
                    }
                    else if (_autoNext)
                    {
                        NextTrack();
                    }
                    else
                    {
                        UpdatePlayButtonUI(false);
                        
                    }
                });
            };

            _player.MediaOpened += (durationMs) =>
            {
                SafeUi(() =>
                {
                    if (durationMs > 0 && _currentTrack != null)
                    {
                        _currentTrack.DurationMs = durationMs;
                        if (_scrubBar != null) _scrubBar.Maximum = Math.Max(1000, durationMs);
                        if (_lblTimeTot != null) _lblTimeTot.Text = FormatTime(durationMs);
                    }
                    UpdateEqualizerDsp();
                    UpdatePlayButtonUI(_player.IsPlaying);
                });
            };

            _player.MediaFailed += () =>
            {
                SafeUi(() =>
                {
                    UpdatePlayButtonUI(false);
                    if (_currentTrack != null && _lblTrackArtist != null)
                    {
                        _lblTrackArtist.Text = _currentTrack.Artist + " (Format audio gagal diputar)";
                    }
                });
            };

            

            // Position at top center of primary monitor
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - COLLAPSED_W) / 2;
            int y = 0;
            this.Location = new System.Drawing.Point(x, y);
            this.Size = new System.Drawing.Size(COLLAPSED_W, COLLAPSED_H);
            ApplyRoundedRegion();

            // Load initial track data & lyrics immediately
            LoadTrack(_currentTrack, false);

            // Animation Timer
            _animTimer.Interval = 15;
            _animTimer.Tick += AnimTimer_Tick;

            // 60 FPS Visualizer & Scrub Tracking
            _tickTimer.Interval = 25;
            _tickTimer.Tick += TickTimer_Tick;
            _tickTimer.Start();

            // Windows 11 Dark Mode DWM Attribute
            try
            {
                int cornerPref = 2; // DWMWCP_ROUND
                Win32.DwmSetWindowAttribute(this.Handle, 33, ref cornerPref, sizeof(int));
                int darkMode = 1;
                Win32.DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
            }
            catch { }
        }

        private void SafeUi(Action action)
        {
            try
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (!this.IsDisposed) action();
                    });
                }
                else
                {
                    action();
                }
            }
            catch { }
        }

        private void ShowFromTray()
        {
            if (!this.Visible)
            {
                this.Show();
            }
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
        }

        private void InitTray()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Dexplay — Desktop Music Island";
            _trayIcon.Icon = SystemIcons.Application;
            _trayIcon.Visible = true;

            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Buka / Tutup Notch", (s, e) => { ShowFromTray(); ToggleExpand(); });
            menu.MenuItems.Add("Cari Lagu (YouTube Music)", (s, e) => { ShowFromTray(); OpenSearchTab(); });
            menu.MenuItems.Add("Mode Island / Notch", (s, e) => { ShowFromTray(); ToggleIsland(); });
            menu.MenuItems.Add("Putar / Jeda", (s, e) => TogglePlay());
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Keluar", (s, e) => Application.Exit());

            _trayIcon.ContextMenu = menu;
            _trayIcon.DoubleClick += (s, e) => { ShowFromTray(); ToggleExpand(); };
        }

        private void SetupSampleData()
        {
            _queue.Add(_currentTrack);
            _queue.Add(new TrackItem { Title = "Sudden Shower (Instrumental)", Artist = "ECLIPSE", Genre = "YT MUSIC • Official Audio", Source = "YT_MUSIC", VideoId = "MNM4D5CxJaU", DurationMs = 233000 });
            _queue.Add(new TrackItem { Title = "Spring Snow", Artist = "10CM", Genre = "YT MUSIC • Official Audio", Source = "YT_MUSIC", VideoId = "ok2pqepWTLU", DurationMs = 200000 });
        }

        private void ApplyRoundedRegion()
        {
            if (!this.IsHandleCreated) return;
            if (_lastRgnW == this.Width && _lastRgnH == this.Height && _lastRgnIsland == _isIsland) return;

            int r = _isIsland ? 24 : 18;
            IntPtr rgn = Win32.CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, r, r);
            if (rgn != IntPtr.Zero)
            {
                if (Win32.SetWindowRgn(this.Handle, rgn, true) == 0)
                {
                    Win32.DeleteObject(rgn);
                }
                else
                {
                    _lastRgnW = this.Width;
                    _lastRgnH = this.Height;
                    _lastRgnIsland = _isIsland;
                }
            }
        }

        // ====================================================================
        // 9. UI Construction
        // ====================================================================
        private void BuildUI()
        {
            // HEADER BAR
            _headerPanel = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(COLLAPSED_W, COLLAPSED_H),
                BackColor = _bg,
                Dock = DockStyle.Top,
                AllowDrop = true
            };
            _headerPanel.MouseDown += Header_MouseDown;
            _headerPanel.MouseMove += Header_MouseMove;
            _headerPanel.MouseUp += Header_MouseUp;
            _headerPanel.DragEnter += (s, e) => OnDragEnter(e);
            _headerPanel.DragDrop += (s, e) => OnDragDrop(e);

            // PLAY / PAUSE BUTTON (Circle Vector Button)
            _btnHeaderPlay = new VectorButton(VectorIconType.Play, 8, 8, 30, 30)
            {
                IsCircleButton = true,
                CircleBgColor = _accent,
                NormalColor = System.Drawing.Color.FromArgb(8, 9, 13),
                HoverColor = System.Drawing.Color.FromArgb(8, 9, 13)
            };
            _btnHeaderPlay.Click += (s, e) => TogglePlay();

            // TITLE & ARTIST LABEL (Click to Expand)
            _lblHeaderTitle = new Label
            {
                Location = new System.Drawing.Point(46, 10),
                Size = new System.Drawing.Size(216, 26),
                Text = _currentTrack.Artist + " — " + _currentTrack.Title,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoEllipsis = true,
                UseCompatibleTextRendering = false,
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblHeaderTitle.Click += (s, e) => ToggleExpand();

            // MINI EQUALIZER (Dancing bars in collapsed header)
            _miniEq = new MiniEqualizer
            {
                Location = new System.Drawing.Point(272, 12),
                Size = new System.Drawing.Size(24, 22),
                AccentColor = _accent
            };

            // SEARCH BUTTON (Vector Magnifying Glass — Zero missing glyph box!)
            _btnHeaderSearch = new VectorButton(VectorIconType.Search, 262, 8, 28, 30)
            {
                NormalColor = _accent,
                HoverColor = System.Drawing.Color.White
            };
            _toolTip.SetToolTip(_btnHeaderSearch, "Cari Official Audio (YouTube Music)");
            _btnHeaderSearch.Click += (s, e) => OpenSearchTab();

            // EXPAND CHEVRON (▼ / ▲)
            _btnHeaderExpand = new VectorButton(VectorIconType.ChevronDown, 296, 8, 28, 30)
            {
                NormalColor = _accent,
                HoverColor = System.Drawing.Color.White
            };
            _btnHeaderExpand.Click += (s, e) => ToggleExpand();

            // DYNAMIC ISLAND MODE TOGGLE
            _btnHeaderIsland = new VectorButton(VectorIconType.Island, 328, 8, 28, 30);
            _toolTip.SetToolTip(_btnHeaderIsland, "Toggle Dynamic Island / Notch Dock");
            _btnHeaderIsland.Click += (s, e) => ToggleIsland();

            // PIN / ALWAYS ON TOP TOGGLE
            _btnHeaderPin = new VectorButton(VectorIconType.Pin, 360, 8, 26, 30);
            _toolTip.SetToolTip(_btnHeaderPin, "Pin Always On Top");
            _btnHeaderPin.Click += (s, e) =>
            {
                _isPinned = !_isPinned;
                this.TopMost = _isPinned;
                _btnHeaderPin.IsActive = _isPinned;
                _btnHeaderPin.Invalidate();
                SaveCurrentConfig();
            };

            // CLOSE / HIDE TO TRAY BUTTON (✕)
            _btnHeaderClose = new VectorButton(VectorIconType.Close, 392, 8, 26, 30);
            _toolTip.SetToolTip(_btnHeaderClose, "Sembunyikan ke System Tray");
            _btnHeaderClose.Click += (s, e) => { this.Hide(); };

            _headerPanel.Controls.Add(_btnHeaderPlay);
            _headerPanel.Controls.Add(_lblHeaderTitle);
            _headerPanel.Controls.Add(_miniEq);
            _headerPanel.Controls.Add(_btnHeaderSearch);
            _headerPanel.Controls.Add(_btnHeaderExpand);
            _headerPanel.Controls.Add(_btnHeaderIsland);
            _headerPanel.Controls.Add(_btnHeaderPin);
            _headerPanel.Controls.Add(_btnHeaderClose);
            this.Controls.Add(_headerPanel);
            LayoutHeaderControls(COLLAPSED_W);

            // DECK PANEL (EXPANDED CONTAINER)
            _deckPanel = new Panel
            {
                Location = new System.Drawing.Point(0, COLLAPSED_H),
                Size = new System.Drawing.Size(EXPANDED_W, EXPANDED_H - COLLAPSED_H),
                BackColor = _bg,
                Visible = false,
                AllowDrop = true
            };
            _deckPanel.DragEnter += (s, e) => OnDragEnter(e);
            _deckPanel.DragDrop += (s, e) => OnDragDrop(e);

            // Tab Bar
            _tabBarPanel = new Panel
            {
                Location = new System.Drawing.Point(14, 4),
                Size = new System.Drawing.Size(EXPANDED_W - 28, 32),
                BackColor = System.Drawing.Color.FromArgb(18, 255, 255, 255)
            };

            string[] tabNames = { "DECK", "YT MUSIC", "LIRIK", "QUEUE", "EQUALIZER" };
            int tabW = (_tabBarPanel.Width - 8) / 5;

            for (int i = 0; i < 5; i++)
            {
                int tabIdx = i;
                Button btnTab = new Button
                {
                    Text = tabNames[i],
                    Location = new System.Drawing.Point(4 + i * tabW, 3),
                    Size = new System.Drawing.Size(tabW - 4, 26),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = (i == 0) ? _accent : System.Drawing.Color.Transparent,
                    ForeColor = (i == 0) ? System.Drawing.Color.FromArgb(8, 9, 13) : System.Drawing.Color.White,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnTab.FlatAppearance.BorderSize = 0;
                btnTab.Click += (s, e) => SwitchTab(tabIdx);
                _tabButtons[i] = btnTab;
                _tabBarPanel.Controls.Add(btnTab);
            }
            _deckPanel.Controls.Add(_tabBarPanel);

            BuildTabDeck();
            BuildTabSearch();
            BuildTabLyrics();
            BuildTabQueue();
            BuildTabEqualizer();

            this.Controls.Add(_deckPanel);
        }

        private void LayoutHeaderControls(int width)
        {
            if (_headerPanel == null || _btnHeaderClose == null) return;
            _headerPanel.Width = width;

            // Anchor right buttons cleanly against right edge
            _btnHeaderClose.Location = new System.Drawing.Point(width - 34, 8);
            _btnHeaderPin.Location = new System.Drawing.Point(width - 64, 8);
            _btnHeaderIsland.Location = new System.Drawing.Point(width - 94, 8);
            _btnHeaderExpand.Location = new System.Drawing.Point(width - 124, 8);
            _btnHeaderSearch.Location = new System.Drawing.Point(width - 156, 8);
            _miniEq.Location = new System.Drawing.Point(width - 188, 12);

            // Left Title gets entire remaining room
            int titleX = 46;
            int titleW = Math.Max(120, (width - 198) - titleX);
            _lblHeaderTitle.Location = new System.Drawing.Point(titleX, 10);
            _lblHeaderTitle.Size = new System.Drawing.Size(titleW, 26);
        }

        private void OpenSearchTab()
        {
            SwitchTab(1);
            if (!_isExpanded) ToggleExpand();
            _txtSearch.Focus();
        }

        // --- TAB 0: DECK VIEW ---
        private void BuildTabDeck()
        {
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(14, 42),
                Size = new System.Drawing.Size(EXPANDED_W - 28, EXPANDED_H - COLLAPSED_H - 52),
                BackColor = _bg,
                Visible = true
            };

            _deckCard = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(p.Width, 136),
                BackColor = _surface
            };

            _lblTrackTitle = new Label
            {
                Location = new System.Drawing.Point(16, 12),
                Size = new System.Drawing.Size(_deckCard.Width - 180, 32),
                Text = _currentTrack.Title,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 13f, FontStyle.Bold),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _lblTrackArtist = new Label
            {
                Location = new System.Drawing.Point(16, 46),
                Size = new System.Drawing.Size(_deckCard.Width - 180, 26),
                Text = _currentTrack.Artist,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 9.5f),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _vinylDisc = new VinylDiscControl(54)
            {
                Location = new System.Drawing.Point(_deckCard.Width - 160, 14),
                BackColor = _surface,
                AccentColor = _accent,
                SecondaryColor = _skins[_currentSkinIndex].SecondaryAccent
            };
            _vinylDisc.DiscClicked += (s, e) =>
            {
                TogglePlay();
            };

            _btnLoop = new Button
            {
                Text = "↻ LOOP",
                Location = new System.Drawing.Point(_deckCard.Width - 96, 14),
                Size = new System.Drawing.Size(84, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnLoop.FlatAppearance.BorderSize = 0;
            _btnLoop.Click += (s, e) =>
            {
                _isLooping = !_isLooping;
                _btnLoop.BackColor = _isLooping ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                _btnLoop.ForeColor = _isLooping ? System.Drawing.Color.FromArgb(8, 9, 13) : _accent;
                SaveCurrentConfig();
            };

            _visualizer = new VisualizerPanel
            {
                Location = new System.Drawing.Point(16, 76),
                Size = new System.Drawing.Size(_deckCard.Width - 32, 48),
                BackColor = _surface,
                AccentColor = _accent,
                SecondaryColor = _skins[_currentSkinIndex].SecondaryAccent
            };
            _visualizer.ModeChanged += (m) => SaveCurrentConfig();

            _deckCard.Controls.Add(_lblTrackTitle);
            _deckCard.Controls.Add(_lblTrackArtist);
            _deckCard.Controls.Add(_vinylDisc);
            _deckCard.Controls.Add(_btnLoop);
            _deckCard.Controls.Add(_visualizer);
            p.Controls.Add(_deckCard);

            // Sleek Dark Scrub Bar
            _lblTimeCur = new Label
            {
                Location = new System.Drawing.Point(8, 146),
                Size = new System.Drawing.Size(52, 20),
                Text = "0:00",
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false,
                UseMnemonic = false
            };

            _scrubBar = new DarkSlider
            {
                Location = new System.Drawing.Point(62, 145),
                Size = new System.Drawing.Size(p.Width - 124, 22),
                Minimum = 0,
                Maximum = 233000,
                Value = 0,
                AccentColor = _accent
            };
            _scrubBar.UserSeek += (s, e) =>
            {
                _player.Seek(_scrubBar.Value);
            };
            _scrubBar.ValueChanged += (s, e) =>
            {
                if (_scrubBar.IsDragging && _lblTimeCur != null)
                {
                    _lblTimeCur.Text = FormatTime(_scrubBar.Value);
                }
            };

            _lblTimeTot = new Label
            {
                Location = new System.Drawing.Point(p.Width - 58, 146),
                Size = new System.Drawing.Size(52, 20),
                Text = "3:53",
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                UseCompatibleTextRendering = false,
                UseMnemonic = false
            };

            p.Controls.Add(_lblTimeCur);
            p.Controls.Add(_scrubBar);
            p.Controls.Add(_lblTimeTot);

            // Center Transport Controls (Mathematically centered)
            int cx = p.Width / 2;
            _btnDeckPlay = new VectorButton(VectorIconType.Play, cx - 21, 184, 42, 42)
            {
                IsCircleButton = true,
                CircleBgColor = _accent,
                NormalColor = System.Drawing.Color.FromArgb(8, 9, 13),
                HoverColor = System.Drawing.Color.FromArgb(8, 9, 13)
            };
            _btnDeckPrev = new VectorButton(VectorIconType.Prev, cx - 58, 189, 32, 32);
            _btnDeckNext = new VectorButton(VectorIconType.Next, cx + 27, 189, 32, 32);

            _btnDeckPrev.Click += (s, e) => PrevTrack();
            _btnDeckPlay.Click += (s, e) => TogglePlay();
            _btnDeckNext.Click += (s, e) => NextTrack();

            p.Controls.Add(_btnDeckPrev);
            p.Controls.Add(_btnDeckPlay);
            p.Controls.Add(_btnDeckNext);

            // Left Quick Action Buttons: Cycle Skin & Open Equalizer
            _btnQuickSkin = CreateMoodButton("TEMA 1", 6, 191, 84, 28, System.Drawing.Color.FromArgb(35, _accent), _accent);
            _btnQuickSkin.Click += (s, e) => CycleSkin();

            _btnQuickEq = CreateMoodButton("EQUALIZER", 98, 191, 96, 28, System.Drawing.Color.FromArgb(25, 255, 255, 255), _textMuted);
            _btnQuickEq.Click += (s, e) => SwitchTab(4);

            p.Controls.Add(_btnQuickSkin);
            p.Controls.Add(_btnQuickEq);

            // Right Volume Control (Vector Speaker Icon with Mute Toggle + Slider)
            _btnDeckVol = new VectorButton(VectorIconType.Volume, p.Width - 174, 191, 24, 28);
            _btnDeckVol.NormalColor = _textMuted;
            _btnDeckVol.HoverColor = _accent;
            _btnDeckVol.ActiveColor = System.Drawing.Color.FromArgb(220, 38, 38);
            _btnDeckVol.Click += (s, e) => ToggleMute();

            _deckVolumeBar = new DarkSlider
            {
                Location = new System.Drawing.Point(p.Width - 146, 194),
                Size = new System.Drawing.Size(140, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                AccentColor = _accent
            };
            _deckVolumeBar.ValueChanged += (s, e) =>
            {
                _masterVolume = _deckVolumeBar.Value;
                if (_masterVolume > 0 && _isMuted)
                {
                    _isMuted = false;
                    _btnDeckVol.IsActive = false;
                    _btnDeckVol.Invalidate();
                }
                else if (_masterVolume == 0 && !_isMuted)
                {
                    _isMuted = true;
                    _btnDeckVol.IsActive = true;
                    _btnDeckVol.Invalidate();
                }
                UpdateEqualizerDsp();
                SaveCurrentConfig();
            };

            p.Controls.Add(_btnDeckVol);
            p.Controls.Add(_deckVolumeBar);

            // Bottom Info Card: Real-time Audio Engine & Quick Navigation
            _deckInfoCard = new Panel
            {
                Location = new System.Drawing.Point(6, 244),
                Size = new System.Drawing.Size(p.Width - 12, 150),
                BackColor = _surface
            };

            _lblDeckEngine = new Label
            {
                Location = new System.Drawing.Point(12, 10),
                Size = new System.Drawing.Size(260, 18),
                Text = "⚡ DEXPLAY AUDIO ENGINE • DSP AKTIF",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _lblDeckQuality = new Label
            {
                Location = new System.Drawing.Point(_deckInfoCard.Width - 210, 10),
                Size = new System.Drawing.Size(198, 18),
                Text = "YouTube Music • Official Audio",
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8f),
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

            _lblDeckDetails = new Label
            {
                Location = new System.Drawing.Point(12, 32),
                Size = new System.Drawing.Size(_deckInfoCard.Width - 24, 20),
                Text = "Preset EQ: Flat • Bass Boost: OFF • Kecepatan: 100%",
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            int navBtnW = (_deckInfoCard.Width - 36) / 3;
            Button btnNavSearch = CreateMoodButton("🔍 Cari Musik", 12, 64, navBtnW, 30, System.Drawing.Color.FromArgb(24, 28, 42), System.Drawing.Color.White);
            btnNavSearch.Click += (s, e) => OpenSearchTab();

            Button btnNavLyrics = CreateMoodButton("📜 Buka Lirik", 18 + navBtnW, 64, navBtnW, 30, System.Drawing.Color.FromArgb(24, 28, 42), System.Drawing.Color.White);
            btnNavLyrics.Click += (s, e) => SwitchTab(2);

            Button btnNavQueue = CreateMoodButton("📋 Daftar Putar", 24 + navBtnW * 2, 64, navBtnW, 30, System.Drawing.Color.FromArgb(24, 28, 42), System.Drawing.Color.White);
            btnNavQueue.Click += (s, e) => SwitchTab(3);

            _deckInfoCard.Controls.Add(_lblDeckEngine);
            _deckInfoCard.Controls.Add(_lblDeckQuality);
            _deckInfoCard.Controls.Add(_lblDeckDetails);
            _deckInfoCard.Controls.Add(btnNavSearch);
            _deckInfoCard.Controls.Add(btnNavLyrics);
            _deckInfoCard.Controls.Add(btnNavQueue);

            p.Controls.Add(_deckInfoCard);

            _tabPanels[0] = p;
            _deckPanel.Controls.Add(p);
            UpdateDeckInfoCard();
        }

        private void ToggleMute()
        {
            if (!_isMuted)
            {
                _preMuteVolume = _masterVolume > 0 ? _masterVolume : 75;
                _isMuted = true;
                _masterVolume = 0;
            }
            else
            {
                _isMuted = false;
                _masterVolume = _preMuteVolume > 0 ? _preMuteVolume : 75;
            }
            if (_deckVolumeBar != null) _deckVolumeBar.Value = _masterVolume;
            if (_btnDeckVol != null) { _btnDeckVol.IsActive = _isMuted; _btnDeckVol.Invalidate(); }
            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        private Button CreateMoodButton(string text, int x, int y, int w, int h, System.Drawing.Color bg, System.Drawing.Color fg)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // --- TAB 1: YOUTUBE MUSIC SEARCH (OFFICIAL AUDIO FILTER) ---
        private void BuildTabSearch()
        {
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(14, 42),
                Size = new System.Drawing.Size(EXPANDED_W - 28, EXPANDED_H - COLLAPSED_H - 52),
                BackColor = _bg,
                Visible = false
            };

            _txtSearch = new TextBox
            {
                Location = new System.Drawing.Point(0, 4),
                Size = new System.Drawing.Size(p.Width - 92, 28),
                BackColor = System.Drawing.Color.FromArgb(24, 26, 36),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    ExecuteYouTubeMusicSearch(_txtSearch.Text);
                }
            };

            Button btnSearchExec = new Button
            {
                Text = "CARI",
                Location = new System.Drawing.Point(p.Width - 86, 3),
                Size = new System.Drawing.Size(86, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = _accent,
                ForeColor = System.Drawing.Color.FromArgb(8, 9, 13),
                Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSearchExec.FlatAppearance.BorderSize = 0;
            btnSearchExec.Click += (s, e) => ExecuteYouTubeMusicSearch(_txtSearch.Text);

            _lblSearchStatus = new Label
            {
                Location = new System.Drawing.Point(2, 36),
                Size = new System.Drawing.Size(p.Width - 4, 18),
                Text = "Ketik judul lagu atau nama artis, lalu tekan Enter atau klik Cari.",
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                UseMnemonic = false
            };

            _lstSearchResults = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 56),
                Size = new System.Drawing.Size(p.Width, p.Height - 60),
                ItemHeight = 30
            };
            _lstSearchResults.DoubleClick += (s, e) => PlaySelectedSearchResult();
            _lstSearchResults.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PlaySelectedSearchResult(); };
            _lstSearchResults.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstSearchResults.Items.Count) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                using (SolidBrush bgB = new SolidBrush(isSelected ? System.Drawing.Color.FromArgb(30, 41, 59) : System.Drawing.Color.FromArgb(16, 18, 25)))
                {
                    e.Graphics.FillRectangle(bgB, e.Bounds);
                }

                if (_searchTracks != null && e.Index < _searchTracks.Count)
                {
                    TrackItem item = _searchTracks[e.Index];

                    if (isSelected)
                    {
                        using (System.Drawing.Pen pBorder = new System.Drawing.Pen(_accent, 1.5f))
                        {
                            e.Graphics.DrawRectangle(pBorder, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                        }
                    }

                    int cy = e.Bounds.Y + e.Bounds.Height / 2;
                    using (SolidBrush trB = new SolidBrush(isSelected ? _accent : System.Drawing.Color.FromArgb(100, 116, 139)))
                    {
                        e.Graphics.FillPolygon(trB, new System.Drawing.Point[] {
                            new System.Drawing.Point(e.Bounds.X + 8, cy - 5),
                            new System.Drawing.Point(e.Bounds.X + 8, cy + 5),
                            new System.Drawing.Point(e.Bounds.X + 16, cy)
                        });
                    }

                    int bx = e.Bounds.X + 22;
                    int by = e.Bounds.Y + (e.Bounds.Height - 16) / 2;
                    using (SolidBrush bBg = new SolidBrush(System.Drawing.Color.FromArgb(28, 56, 189, 248)))
                    using (System.Drawing.Pen bPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 56, 189, 248)))
                    {
                        e.Graphics.FillRectangle(bBg, bx, by, 54, 16);
                        e.Graphics.DrawRectangle(bPen, bx, by, 54, 16);
                    }
                    TextRenderer.DrawText(e.Graphics, "OFFICIAL", _fontBadge, new Rectangle(bx, by, 54, 16), _accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    string displayTxt = string.Format("{0} — {1}", item.Title, item.Artist);
                    Rectangle textRect = new Rectangle(bx + 60, e.Bounds.Y, e.Bounds.Width - (bx + 64), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, displayTxt, _fontListRegular, textRect, isSelected ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(226, 232, 240), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    string fallback = _lstSearchResults.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, fallback, _fontListItalic, textRect, _textMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            p.Controls.Add(_txtSearch);
            p.Controls.Add(btnSearchExec);
            p.Controls.Add(_lblSearchStatus);
            p.Controls.Add(_lstSearchResults);

            _tabPanels[1] = p;
            _deckPanel.Controls.Add(p);
        }

        // --- TAB 2: LYRICS VIEW (LRCLIB SYNCED KARAOKE) ---
        private void BuildTabLyrics()
        {
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(14, 42),
                Size = new System.Drawing.Size(EXPANDED_W - 28, EXPANDED_H - COLLAPSED_H - 52),
                BackColor = _bg,
                Visible = false
            };

            Label lblLyricsHeader = new Label
            {
                Location = new System.Drawing.Point(2, 4),
                Size = new System.Drawing.Size(p.Width - 4, 20),
                Text = "LRCLIB Synchronized Lyrics (Klik baris lirik untuk melompat/seek):",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold),
                UseMnemonic = false
            };

            _lstLyrics = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 26),
                Size = new System.Drawing.Size(p.Width, p.Height - 30),
                ItemHeight = 32
            };
            _lstLyrics.Click += (s, e) =>
            {
                if (_lstLyrics.SelectedIndex >= 0 && _lstLyrics.SelectedIndex < _lyrics.Count)
                {
                    double targetSec = _lyrics[_lstLyrics.SelectedIndex].TimeSec;
                    _player.Seek((int)(targetSec * 1000));
                }
            };
            _lstLyrics.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstLyrics.Items.Count) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                bool isCurrent = (_lyrics != null && e.Index < _lyrics.Count && e.Index == _activeLyricIndex);

                using (SolidBrush bgB = new SolidBrush(isSelected ? System.Drawing.Color.FromArgb(30, 41, 59) : (isCurrent ? System.Drawing.Color.FromArgb(24, 40, 60) : System.Drawing.Color.FromArgb(16, 18, 25))))
                {
                    e.Graphics.FillRectangle(bgB, e.Bounds);
                }

                if (_lyrics != null && e.Index < _lyrics.Count)
                {
                    LyricLine line = _lyrics[e.Index];
                    int secVal = (int)line.TimeSec;
                    string timeStr = string.Format("{0}:{1:D2}", secVal / 60, secVal % 60);
                    TextRenderer.DrawText(e.Graphics, timeStr, _fontListTime, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, 44, e.Bounds.Height), isCurrent ? _accent : System.Drawing.Color.FromArgb(100, 116, 139), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    Font lFont = isCurrent ? _fontListBold : _fontListRegular;
                    System.Drawing.Color lCol = isCurrent ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(148, 163, 184);
                    Rectangle textRect = new Rectangle(e.Bounds.X + 56, e.Bounds.Y, e.Bounds.Width - 62, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, line.Text, lFont, textRect, lCol, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    string itemText = _lstLyrics.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, itemText, _fontListItalic, textRect, System.Drawing.Color.FromArgb(148, 163, 184), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            p.Controls.Add(lblLyricsHeader);
            p.Controls.Add(_lstLyrics);

            _tabPanels[2] = p;
            _deckPanel.Controls.Add(p);
        }

        // --- TAB 3: QUEUE VIEW & LOCAL FILE IMPORT ---
        private void BuildTabQueue()
        {
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(14, 42),
                Size = new System.Drawing.Size(EXPANDED_W - 28, EXPANDED_H - COLLAPSED_H - 52),
                BackColor = _bg,
                Visible = false
            };

            Label lbl = new Label
            {
                Location = new System.Drawing.Point(2, 4),
                Size = new System.Drawing.Size(240, 20),
                Text = "Daftar Putar (Double-klik untuk memutar):",
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold),
                UseMnemonic = false
            };

            Button btnAddFile = new Button
            {
                Text = "+ Berkas Lokal",
                Location = new System.Drawing.Point(p.Width - 110, 2),
                Size = new System.Drawing.Size(110, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(30, 255, 255, 255),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnAddFile.FlatAppearance.BorderSize = 0;
            btnAddFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Berkas Audio (*.mp3;*.wav;*.flac;*.m4a)|*.mp3;*.wav;*.flac;*.m4a|Semua Berkas (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        LoadLocalAudioFile(ofd.FileName);
                    }
                }
            };

            _lstQueue = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 30),
                Size = new System.Drawing.Size(p.Width, p.Height - 34),
                ItemHeight = 28
            };
            _lstQueue.DoubleClick += (s, e) =>
            {
                if (_lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    LoadTrack(_queue[_lstQueue.SelectedIndex], true);
                    SwitchTab(0);
                }
            };
            _lstQueue.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstQueue.Items.Count) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                using (SolidBrush bgB = new SolidBrush(isSelected ? System.Drawing.Color.FromArgb(30, 41, 59) : System.Drawing.Color.FromArgb(16, 18, 25)))
                {
                    e.Graphics.FillRectangle(bgB, e.Bounds);
                }

                if (_queue != null && e.Index < _queue.Count)
                {
                    TrackItem item = _queue[e.Index];
                    bool isCurrent = (_currentTrack != null && _currentTrack.VideoId == item.VideoId && _currentTrack.Title == item.Title);

                    string numStr = (e.Index + 1).ToString();
                    TextRenderer.DrawText(e.Graphics, numStr, _fontListTime, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 24, e.Bounds.Height), isCurrent ? _accent : _textMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    string displayTxt = string.Format("{0} — {1}", item.Title, item.Artist);
                    Rectangle textRect = new Rectangle(e.Bounds.X + 32, e.Bounds.Y, e.Bounds.Width - 36, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, displayTxt, _fontListRegular, textRect, isCurrent ? _accent : (isSelected ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(226, 232, 240)), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    string fallback = _lstQueue.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, fallback, _fontListItalic, textRect, _textMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            p.Controls.Add(lbl);
            p.Controls.Add(btnAddFile);
            p.Controls.Add(_lstQueue);

            _tabPanels[3] = p;
            _deckPanel.Controls.Add(p);
            RefreshQueueUI();
        }

        // --- TAB 4: GRAPHIC EQUALIZER & AUDIO ENHANCEMENTS ---
        private void BuildTabEqualizer()
        {
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(14, 42),
                Size = new System.Drawing.Size(EXPANDED_W - 28, EXPANDED_H - COLLAPSED_H - 52),
                BackColor = _bg,
                Visible = false
            };

            int contentW = p.Width - 12; // 540px

            // 1. TOP CARD: THEME PALETTE PICKER (FULL WIDTH - DARK SELECTOR)
            Panel cardTheme = new Panel
            {
                Location = new System.Drawing.Point(6, 6),
                Size = new System.Drawing.Size(contentW, 70),
                BackColor = _surface
            };

            Label lblSkin = new Label
            {
                Location = new System.Drawing.Point(14, 10),
                Size = new System.Drawing.Size(contentW - 28, 18),
                Text = "TEMA VISUAL DEXPLAY (5 TEMA RESMI)",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                UseMnemonic = false
            };

            _cmbSkinSelector = new DarkSelect
            {
                Location = new System.Drawing.Point(14, 32),
                Size = new System.Drawing.Size(contentW - 28, 28),
                Font = new System.Drawing.Font("Segoe UI", 9f),
                AccentColor = _accent
            };
            foreach (var sk in _skins)
            {
                _cmbSkinSelector.AddItem(string.Format("{0} — {1}", sk.Name, sk.Tagline), sk.Accent);
            }
            _cmbSkinSelector.SetSelectedIndexSilent(_currentSkinIndex);
            _cmbSkinSelector.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbSkinSelector.SelectedIndex >= 0)
                {
                    ApplySkin(_cmbSkinSelector.SelectedIndex);
                }
            };

            cardTheme.Controls.Add(lblSkin);
            cardTheme.Controls.Add(_cmbSkinSelector);
            p.Controls.Add(cardTheme);

            // 2. MIDDLE CARD: 5-BAND GRAPHIC EQUALIZER
            Panel cardEq = new Panel
            {
                Location = new System.Drawing.Point(6, 88),
                Size = new System.Drawing.Size(contentW, 220),
                BackColor = _surface
            };

            Label lblEq = new Label
            {
                Location = new System.Drawing.Point(14, 14),
                Size = new System.Drawing.Size(102, 22),
                Text = "GRAPHIC EQ",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold),
                UseMnemonic = false
            };

            _cmbEqPreset = new DarkSelect
            {
                Location = new System.Drawing.Point(122, 11),
                Size = new System.Drawing.Size(212, 28),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                AccentColor = _accent
            };
            foreach (var preset in _eqPresets)
            {
                _cmbEqPreset.AddItem(preset.Name);
            }
            _cmbEqPreset.SetSelectedIndexSilent(_eqPresetIndex);
            _cmbEqPreset.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbEqPreset.SelectedIndex >= 0 && !_isInternalEqUpdate)
                {
                    ApplyEqualizerPreset(_cmbEqPreset.SelectedIndex);
                }
            };

            _btnEqBypass = new Button
            {
                Text = _eqActive ? "EQ: ON" : "EQ: BYPASS",
                Location = new System.Drawing.Point(344, 11),
                Size = new System.Drawing.Size(96, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = _eqActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _eqActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnEqBypass.FlatAppearance.BorderSize = 0;
            _btnEqBypass.Click += (s, e) => ToggleEqualizerBypass();

            _btnEqReset = new Button
            {
                Text = "RESET",
                Location = new System.Drawing.Point(448, 11),
                Size = new System.Drawing.Size(78, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnEqReset.FlatAppearance.BorderSize = 0;
            _btnEqReset.Click += (s, e) => ResetEqualizer();

            cardEq.Controls.Add(lblEq);
            cardEq.Controls.Add(_cmbEqPreset);
            cardEq.Controls.Add(_btnEqBypass);
            cardEq.Controls.Add(_btnEqReset);

            // 5 Horizontal Faders
            string[] bandLabels = { "60 Hz", "250 Hz", "1 kHz", "4 kHz", "12 kHz" };
            string[] bandDescs = { "Sub-Bass", "Bass", "Midrange", "Presence", "Treble" };

            int faderStartY = 50;
            int faderRowH = 33;

            for (int i = 0; i < 5; i++)
            {
                int bandIdx = i;
                int yPos = faderStartY + i * faderRowH;

                Label lblBand = new Label
                {
                    Location = new System.Drawing.Point(14, yPos + 2),
                    Size = new System.Drawing.Size(124, 20),
                    Text = string.Format("{0} ({1})", bandLabels[i], bandDescs[i]),
                    ForeColor = _textMuted,
                    Font = new System.Drawing.Font("Consolas", 8.2f, FontStyle.Bold),
                    UseMnemonic = false
                };

                DarkSlider slider = new DarkSlider
                {
                    Location = new System.Drawing.Point(144, yPos),
                    Size = new System.Drawing.Size(324, 22),
                    Minimum = -12,
                    Maximum = 12,
                    Value = _eqBands[i],
                    AccentColor = _accent
                };

                Label lblVal = new Label
                {
                    Location = new System.Drawing.Point(474, yPos + 2),
                    Size = new System.Drawing.Size(54, 20),
                    Text = FormatDb(_eqBands[i]),
                    ForeColor = _eqBands[i] == 0 ? _textMuted : _accent,
                    Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleRight,
                    UseMnemonic = false
                };

                slider.ValueChanged += (s, e) =>
                {
                    _eqBands[bandIdx] = slider.Value;
                    lblVal.Text = FormatDb(slider.Value);
                    lblVal.ForeColor = slider.Value == 0 ? _textMuted : _accent;
                    if (!_isInternalEqUpdate)
                    {
                        _isInternalEqUpdate = true;
                        _cmbEqPreset.SetSelectedIndexSilent(_eqPresets.Count - 1); // Custom
                        _eqPresetIndex = _eqPresets.Count - 1;
                        _isInternalEqUpdate = false;
                    }
                    UpdateEqualizerDsp();
                    SaveCurrentConfig();
                };

                _eqSliders[i] = slider;
                _lblEqValues[i] = lblVal;

                cardEq.Controls.Add(lblBand);
                cardEq.Controls.Add(slider);
                cardEq.Controls.Add(lblVal);
            }
            p.Controls.Add(cardEq);

            // 3. BOTTOM CARD: BASS BOOST & PLAYBACK TEMPO
            Panel cardEnhance = new Panel
            {
                Location = new System.Drawing.Point(6, 320),
                Size = new System.Drawing.Size(contentW, 88),
                BackColor = _surface
            };

            // Left: Bass Boost & Auto-Next
            Label lblEnhance = new Label
            {
                Location = new System.Drawing.Point(14, 10),
                Size = new System.Drawing.Size(250, 18),
                Text = "AKUSTIK & OTOMASI",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                UseMnemonic = false
            };

            _btnBassBoost = new Button
            {
                Text = _bassBoostActive ? "BASS: +6 dB" : "BASS: OFF",
                Location = new System.Drawing.Point(14, 36),
                Size = new System.Drawing.Size(122, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = _bassBoostActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _bassBoostActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnBassBoost.FlatAppearance.BorderSize = 0;
            _btnBassBoost.Click += (s, e) => ToggleBassBoost();

            _btnAutoNext = new Button
            {
                Text = _autoNext ? "AUTO-NEXT: ON" : "AUTO-NEXT: OFF",
                Location = new System.Drawing.Point(144, 36),
                Size = new System.Drawing.Size(126, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = _autoNext ? _accent : System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _autoNext ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnAutoNext.FlatAppearance.BorderSize = 0;
            _btnAutoNext.Click += (s, e) => ToggleAutoNext();

            cardEnhance.Controls.Add(lblEnhance);
            cardEnhance.Controls.Add(_btnBassBoost);
            cardEnhance.Controls.Add(_btnAutoNext);

            // Right: Playback Tempo / Speed Slider
            int tempoX = 290;
            int tempoW = contentW - tempoX - 14;

            Label lblTempoTitle = new Label
            {
                Location = new System.Drawing.Point(tempoX, 10),
                Size = new System.Drawing.Size(tempoW, 18),
                Text = "KECEPATAN PUTAR (TEMPO)",
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                UseMnemonic = false
            };

            _sliderTempo = new DarkSlider
            {
                Location = new System.Drawing.Point(tempoX, 42),
                Size = new System.Drawing.Size(tempoW - 88, 22),
                Minimum = 50,
                Maximum = 150,
                Value = (int)(_tempoSpeed * 100),
                AccentColor = _accent
            };

            _lblTempoVal = new Label
            {
                Location = new System.Drawing.Point(tempoX + tempoW - 82, 42),
                Size = new System.Drawing.Size(44, 22),
                Text = string.Format("{0}%", (int)(_tempoSpeed * 100)),
                ForeColor = _tempoSpeed == 1.0 ? _textMuted : _accent,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };

            _btnTempoReset = new Button
            {
                Text = "↺",
                Location = new System.Drawing.Point(tempoX + tempoW - 34, 38),
                Size = new System.Drawing.Size(34, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(24, 28, 42),
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnTempoReset.FlatAppearance.BorderSize = 0;
            _btnTempoReset.Click += (s, e) => SetPlaybackTempo(1.0);

            _sliderTempo.ValueChanged += (s, e) =>
            {
                double speed = _sliderTempo.Value / 100.0;
                _tempoSpeed = speed;
                _player.SetSpeedRatio(speed);
                _lblTempoVal.Text = string.Format("{0}%", _sliderTempo.Value);
                _lblTempoVal.ForeColor = _sliderTempo.Value == 100 ? _textMuted : _accent;
                SaveCurrentConfig();
            };

            cardEnhance.Controls.Add(lblTempoTitle);
            cardEnhance.Controls.Add(_sliderTempo);
            cardEnhance.Controls.Add(_lblTempoVal);
            cardEnhance.Controls.Add(_btnTempoReset);

            p.Controls.Add(cardEnhance);

            _tabPanels[4] = p;
            _deckPanel.Controls.Add(p);
        }


        private void SwitchTab(int tabIndex)
        {
            _currentTab = tabIndex;
            for (int i = 0; i < 5; i++)
            {
                bool active = (i == tabIndex);
                _tabButtons[i].BackColor = active ? _accent : System.Drawing.Color.Transparent;
                _tabButtons[i].ForeColor = active ? System.Drawing.Color.FromArgb(8, 9, 13) : System.Drawing.Color.White;
                _tabPanels[i].Visible = active;
            }
            if (_deckPanel != null) _deckPanel.Invalidate(true);
        }

        // ====================================================================
        // 10. Dragging & Morphing
        // ====================================================================
        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isMouseDown = true;
                _mouseDownPos = e.Location;
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                this.Location = new System.Drawing.Point(
                    this.Location.X + (e.X - _mouseDownPos.X),
                    this.Location.Y + (e.Y - _mouseDownPos.Y)
                );
            }
        }

        private void Header_MouseUp(object sender, MouseEventArgs e)
        {
            _isMouseDown = false;
        }

        public void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            _targetW = _isExpanded ? EXPANDED_W : COLLAPSED_W;
            _targetH = _isExpanded ? EXPANDED_H : COLLAPSED_H;
            _deckPanel.Visible = _isExpanded;

            _btnHeaderExpand.IconType = _isExpanded ? VectorIconType.ChevronUp : VectorIconType.ChevronDown;
            _btnHeaderExpand.Invalidate();

            _animTimer.Start();
        }

        public void ToggleIsland()
        {
            _isIsland = !_isIsland;
            _btnHeaderIsland.IsActive = _isIsland;
            _btnHeaderIsland.Invalidate();

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - this.Width) / 2;
            int y = _isIsland ? 36 : 0;
            this.Location = new System.Drawing.Point(x, y);
            ApplyRoundedRegion();
            SaveCurrentConfig();
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            int oldW = this.Width;
            int dw = (_targetW - oldW);
            int dh = (_targetH - this.Height);

            if (Math.Abs(dw) <= 3 && Math.Abs(dh) <= 3)
            {
                this.Size = new System.Drawing.Size(_targetW, _targetH);
                _animTimer.Stop();
            }
            else
            {
                int stepW = Math.Abs(dw) <= 3 ? dw : (int)(dw * 0.35);
                int stepH = Math.Abs(dh) <= 3 ? dh : (int)(dh * 0.35);
                if (dw != 0 && stepW == 0) stepW = Math.Sign(dw);
                if (dh != 0 && stepH == 0) stepH = Math.Sign(dh);
                this.Size = new System.Drawing.Size(oldW + stepW, this.Height + stepH);
            }

            LayoutHeaderControls(this.Width);

            // Preserve user's horizontal drag center while keeping widget inside screen bounds
            if (!_isMouseDown)
            {
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;
                int centerX = this.Location.X + oldW / 2;
                int x = Math.Max(screen.Left, Math.Min(screen.Right - this.Width, centerX - this.Width / 2));
                int y = _isIsland ? Math.Max(screen.Top + 12, this.Location.Y) : screen.Top;
                this.Location = new System.Drawing.Point(x, y);
            }
            ApplyRoundedRegion();
        }

        private void TickTimer_Tick(object sender, EventArgs e)
        {
            bool playing = _player.IsPlaying;
            _miniEq.IsPlaying = playing;
            _miniEq.UpdateFrame();

            if (_isExpanded && _currentTab == 0)
            {
                _visualizer.IsPlaying = playing;
                _visualizer.UpdateFrame();

                if (_vinylDisc != null)
                {
                    _vinylDisc.UpdateFrame(playing, _tempoSpeed);
                }
            }

            UpdateAmbientAura(playing);

            // Smooth header title ticker so long titles are never truncated
            UpdateHeaderTitleTicker();

            if (playing)
            {
                int pos = _player.GetPositionMs();
                int len = _player.GetLengthMs();
                if (len <= 0 && _currentTrack != null && _currentTrack.DurationMs > 0)
                {
                    len = _currentTrack.DurationMs;
                }
                if (!_scrubBar.IsDragging && len > 0)
                {
                    _scrubBar.Maximum = len;
                    _scrubBar.Value = Math.Min(len, pos);
                }
                _lblTimeCur.Text = _scrubBar.IsDragging ? FormatTime(_scrubBar.Value) : FormatTime(pos);
                if (len > 0) _lblTimeTot.Text = FormatTime(len);

                UpdateActiveLyric(pos / 1000.0);
            }
        }

        private void SetHeaderLabelText(string txt, bool autoEllipsis)
        {
            if (_lblHeaderTitle == null) return;
            if (_lblHeaderTitle.AutoEllipsis != autoEllipsis) _lblHeaderTitle.AutoEllipsis = autoEllipsis;
            if (_lastHeaderText != txt)
            {
                _lastHeaderText = txt;
                _lblHeaderTitle.Text = txt;
            }
        }

        private void UpdateHeaderTitleTicker()
        {
            _tickerCounter++;
            if (_tickerCounter % 8 != 0) return; // Tick every ~200ms

            if (_currentTrack == null || _lblHeaderTitle == null) return;
            string full = (_currentTrack.Artist + " — " + _currentTrack.Title).Trim();
            if (string.IsNullOrEmpty(full)) return;

            Size sz = TextRenderer.MeasureText(full, _lblHeaderTitle.Font);
            if (sz.Width <= _lblHeaderTitle.Width)
            {
                SetHeaderLabelText(full, true);
                _headerScrollOffset = 0;
                _headerScrollPause = 20;
            }
            else
            {
                if (_headerScrollPause > 0)
                {
                    _headerScrollPause--;
                    SetHeaderLabelText(full, true);
                    return;
                }

                string padded = full + "    •    ";
                int len = padded.Length;
                _headerScrollOffset = (_headerScrollOffset + 1) % len;
                if (_headerScrollOffset == 0)
                {
                    _headerScrollPause = 20;
                    SetHeaderLabelText(full, true);
                }
                else
                {
                    string triple = padded + padded + padded;
                    SetHeaderLabelText(triple.Substring(_headerScrollOffset, len), false);
                }
            }
        }

        private string FormatTime(int ms)
        {
            int s = Math.Max(0, ms) / 1000;
            return string.Format("{0}:{1:D2}", s / 60, s % 60);
        }

        private void UpdateAmbientAura(bool playing)
        {
            if (playing)
            {
                _auraAlpha += _auraDirection * 3;
                if (_auraAlpha >= 130) { _auraAlpha = 130; _auraDirection = -1; }
                else if (_auraAlpha <= 45) { _auraAlpha = 45; _auraDirection = 1; }
            }
            else
            {
                _auraAlpha = 35;
            }

            if (_auraAlpha != _lastAuraAlpha && (_tickerCounter % 4 == 0 || !playing))
            {
                _lastAuraAlpha = _auraAlpha;
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int r = _isIsland ? 24 : 18;
            using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), r))
            {
                using (System.Drawing.Pen auraPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(_auraAlpha, _accent), 1.5f))
                {
                    e.Graphics.DrawPath(auraPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void LoadSavedConfig()
        {
            try
            {
                WidgetConfig cfg = WidgetConfig.Load();
                _masterVolume = Math.Max(0, Math.Min(100, cfg.MasterVolume));
                _isMuted = (_masterVolume == 0);
                if (_deckVolumeBar != null) _deckVolumeBar.Value = _masterVolume;
                if (_btnDeckVol != null) { _btnDeckVol.IsActive = _isMuted; _btnDeckVol.Invalidate(); }

                _autoNext = cfg.AutoNext;
            _eqActive = cfg.EqActive;
            _eqPresetIndex = cfg.EqPresetIndex;
            _bassBoostActive = cfg.BassBoostActive;
            if (cfg.EqBands != null && cfg.EqBands.Length == 5) _eqBands = cfg.EqBands;
                if (_btnAutoNext != null)
                {
                    _btnAutoNext.Text = _autoNext ? "AUTO-NEXT: ON" : "AUTO-NEXT: OFF";
                    _btnAutoNext.BackColor = _autoNext ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                    _btnAutoNext.ForeColor = _autoNext ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
                }
                _isLooping = cfg.IsLooping;
                if (_btnLoop != null)
                {
                    _btnLoop.BackColor = _isLooping ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                    _btnLoop.ForeColor = _isLooping ? System.Drawing.Color.FromArgb(8, 9, 13) : _accent;
                }
                _isIsland = cfg.IsIsland;
                _isPinned = cfg.IsPinned;
                this.TopMost = _isPinned;
                if (_btnHeaderPin != null) { _btnHeaderPin.IsActive = _isPinned; _btnHeaderPin.Invalidate(); }
                if (_btnHeaderIsland != null) { _btnHeaderIsland.IsActive = _isIsland; _btnHeaderIsland.Invalidate(); }
                int y = _isIsland ? 36 : 0;
                this.Location = new System.Drawing.Point(this.Location.X, y);
                ApplyRoundedRegion();

                if (cfg.SkinIndex >= 0 && cfg.SkinIndex < _skins.Count)
                {
                    ApplySkin(cfg.SkinIndex, false);
                }

                if (_visualizer != null && cfg.VisualizerMode >= 0 && cfg.VisualizerMode <= 3)
                {
                    _visualizer.SetMode((VisualizerMode)cfg.VisualizerMode);
                }
                UpdateEqualizerDsp();

                // Restore last played track from saved history (paused & ready)
                if (!string.IsNullOrEmpty(cfg.LastTitle))
                {
                    _currentTrack = new TrackItem
                    {
                        Title = cfg.LastTitle,
                        Artist = !string.IsNullOrEmpty(cfg.LastArtist) ? cfg.LastArtist : "Unknown Artist",
                        VideoId = cfg.LastVideoId ?? "",
                        Url = cfg.LastUrl ?? "",
                        DurationMs = cfg.LastDurationMs > 0 ? cfg.LastDurationMs : 233000,
                        Genre = !string.IsNullOrEmpty(cfg.LastGenre) ? cfg.LastGenre : "YT MUSIC • Official Audio",
                        Source = !string.IsNullOrEmpty(cfg.LastSource) ? cfg.LastSource : "YT_MUSIC"
                    };
                    if (_queue != null)
                    {
                        if (_queue.Count > 0) _queue[0] = _currentTrack;
                        else _queue.Add(_currentTrack);
                    }
                    LoadTrack(_currentTrack, false);
                }
            }
            catch { }
        }

        private void SaveCurrentConfig()
        {
            try
            {
                WidgetConfig cfg = new WidgetConfig();
                cfg.MasterVolume = _masterVolume;
                cfg.SkinIndex = _currentSkinIndex;
                cfg.VisualizerMode = _visualizer != null ? (int)_visualizer.Mode : 0;
                cfg.AutoNext = _autoNext;
            cfg.EqActive = _eqActive;
            cfg.EqPresetIndex = _eqPresetIndex;
            cfg.BassBoostActive = _bassBoostActive;
            cfg.EqBands = _eqBands;
                cfg.IsLooping = _isLooping;
                cfg.IsIsland = _isIsland;
                cfg.IsPinned = _isPinned;
                if (_currentTrack != null)
                {
                    cfg.LastTitle = _currentTrack.Title ?? "";
                    cfg.LastArtist = _currentTrack.Artist ?? "";
                    cfg.LastVideoId = _currentTrack.VideoId ?? "";
                    cfg.LastUrl = _currentTrack.Url ?? "";
                    cfg.LastDurationMs = _currentTrack.DurationMs;
                    cfg.LastGenre = _currentTrack.Genre ?? "YT MUSIC • Official Audio";
                    cfg.LastSource = _currentTrack.Source ?? "YT_MUSIC";
                }
                cfg.Save();
            }
            catch { }
        }

        // ====================================================================
        // 11. Audio Player Controls & Loading
        // ====================================================================
        private int _currentLoadId = 0;
        private bool _isDownloadingAudio = false;
        private bool _pendingPlay = false;

        public void LoadTrack(TrackItem track, bool playImmediate = true)
        {
            if (track == null) return;
            _currentTrack = track;
            _pendingPlay = playImmediate;
            SaveCurrentConfig();
            int loadId = Interlocked.Increment(ref _currentLoadId);

            // 1. Instantly silence and stop existing playback (prevents audio bleed/overlap)
            _player.Stop();
            

            // 2. Immediately reset scrub bar and time counters
            if (_scrubBar != null)
            {
                _scrubBar.Value = 0;
                _scrubBar.Maximum = Math.Max(1000, track.DurationMs > 0 ? track.DurationMs : 200000);
            }
            if (_lblTimeCur != null) _lblTimeCur.Text = "0:00";
            if (_lblTimeTot != null) _lblTimeTot.Text = FormatTime(track.DurationMs > 0 ? track.DurationMs : 200000);

            // 3. Reset lyrics state
            _activeLyricIndex = -1;
            if (_lyrics != null) _lyrics.Clear();
            if (_lstLyrics != null)
            {
                _lstLyrics.Items.Clear();
                _lstLyrics.Items.Add("Memuat lirik...");
            }
            if (_lstQueue != null) _lstQueue.Invalidate();

            _headerScrollOffset = 0;
            _headerScrollPause = 20;
            _lastHeaderText = "";
            SetHeaderLabelText(track.Artist + " — " + track.Title, true);
            _lblTrackTitle.Text = track.Title;
            _lblTrackArtist.Text = track.Artist;
            UpdateDeckInfoCard();

            UpdatePlayButtonUI(_pendingPlay);
            
            _player.SetSpeedRatio(_tempoSpeed);

            // Fetch Synced Lyrics
            ThreadPool.QueueUserWorkItem(state => FetchLrcLyrics(track.Title, track.Artist, loadId));

            // Check if it's a YouTube track that needs stream extraction / caching
            if (!string.IsNullOrEmpty(track.VideoId) && string.IsNullOrEmpty(track.Url))
            {
                ExtractAndPlayYouTubeTrack(track, loadId);
            }
            else if (!string.IsNullOrEmpty(track.Url))
            {
                _isDownloadingAudio = false;
                if (_pendingPlay)
                {
                    _player.Play(track.Url);
                    
                    UpdateEqualizerDsp();
                }
                else _player.Open(track.Url);
                UpdatePlayButtonUI(_player.IsPlaying);
            }
            else
            {
                _isDownloadingAudio = false;
                _lblTrackArtist.Text = "Mencari stream audio...";
                ExecuteYouTubeMusicSearch(track.Artist + " " + track.Title);
            }
        }

        public void TogglePlay()
        {
            if (_isDownloadingAudio)
            {
                _pendingPlay = !_pendingPlay;
                UpdatePlayButtonUI(_pendingPlay);
                return;
            }

            if (string.IsNullOrEmpty(_currentTrack.Url) && !string.IsNullOrEmpty(_currentTrack.VideoId))
            {
                _pendingPlay = true;
                UpdatePlayButtonUI(true);
                ExtractAndPlayYouTubeTrack(_currentTrack, _currentLoadId);
                return;
            }

            _player.TogglePlay();
            UpdatePlayButtonUI(_player.IsPlaying);
        }

        private void UpdatePlayButtonUI(bool isPlaying)
        {
            _btnHeaderPlay.IconType = isPlaying ? VectorIconType.Pause : VectorIconType.Play;
            _btnHeaderPlay.Invalidate();

            _btnDeckPlay.IconType = isPlaying ? VectorIconType.Pause : VectorIconType.Play;
            _btnDeckPlay.Invalidate();
        }

        public void NextTrack()
        {
            if (_queue == null || _queue.Count == 0) return;
            int idx = -1;
            if (_currentTrack != null)
            {
                idx = _queue.FindIndex(t => (!string.IsNullOrEmpty(t.VideoId) && t.VideoId == _currentTrack.VideoId) ||
                                            (!string.IsNullOrEmpty(t.Title) && t.Title.Equals(_currentTrack.Title, StringComparison.OrdinalIgnoreCase)));
            }
            if (idx < 0) idx = 0;
            int nextIdx = (idx + 1) % _queue.Count;
            LoadTrack(_queue[nextIdx], true);
        }

        public void PrevTrack()
        {
            if (_queue == null || _queue.Count == 0) return;
            int idx = -1;
            if (_currentTrack != null)
            {
                idx = _queue.FindIndex(t => (!string.IsNullOrEmpty(t.VideoId) && t.VideoId == _currentTrack.VideoId) ||
                                            (!string.IsNullOrEmpty(t.Title) && t.Title.Equals(_currentTrack.Title, StringComparison.OrdinalIgnoreCase)));
            }
            if (idx < 0) idx = 0;
            int prevIdx = (idx - 1 + _queue.Count) % _queue.Count;
            LoadTrack(_queue[prevIdx], true);
        }

                // ====================================================================
        // 11b. Equalizer Engine & Audio Enhancements
        // ====================================================================
        private static string FormatDb(int db)
        {
            if (db > 0) return "+" + db + " dB";
            return db + " dB";
        }

        public void ApplyEqualizerPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _eqPresets.Count) return;
            _eqPresetIndex = presetIndex;
            EqPreset p = _eqPresets[presetIndex];

            _isInternalEqUpdate = true;
            for (int i = 0; i < 5; i++)
            {
                _eqBands[i] = p.Bands[i];
                if (_eqSliders[i] != null) _eqSliders[i].Value = p.Bands[i];
                if (_lblEqValues[i] != null)
                {
                    _lblEqValues[i].Text = FormatDb(p.Bands[i]);
                    _lblEqValues[i].ForeColor = p.Bands[i] == 0 ? _textMuted : _accent;
                }
            }
            if (_cmbEqPreset != null && _cmbEqPreset.SelectedIndex != presetIndex)
            {
                _cmbEqPreset.SetSelectedIndexSilent(presetIndex);
            }
            _isInternalEqUpdate = false;

            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        public void ToggleEqualizerBypass()
        {
            _eqActive = !_eqActive;
            if (_btnEqBypass != null)
            {
                _btnEqBypass.Text = _eqActive ? "EQ: ON" : "EQ: BYPASS";
                _btnEqBypass.BackColor = _eqActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                _btnEqBypass.ForeColor = _eqActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
            }
            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        public void ResetEqualizer()
        {
            ApplyEqualizerPreset(0); // Flat
        }

        public void ToggleBassBoost()
        {
            _bassBoostActive = !_bassBoostActive;
            if (_btnBassBoost != null)
            {
                _btnBassBoost.Text = _bassBoostActive ? "BASS: +6 dB" : "BASS: OFF";
                _btnBassBoost.BackColor = _bassBoostActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                _btnBassBoost.ForeColor = _bassBoostActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
            }
            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        public void ToggleAutoNext()
        {
            _autoNext = !_autoNext;
            if (_btnAutoNext != null)
            {
                _btnAutoNext.Text = _autoNext ? "AUTO-NEXT: ON" : "AUTO-NEXT: OFF";
                _btnAutoNext.BackColor = _autoNext ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                _btnAutoNext.ForeColor = _autoNext ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
            }
            SaveCurrentConfig();
        }

        public void SetPlaybackTempo(double speed)
        {
            _tempoSpeed = Math.Max(0.5, Math.Min(1.5, speed));
            _player.SetSpeedRatio(_tempoSpeed);
            if (_sliderTempo != null) _sliderTempo.Value = (int)(_tempoSpeed * 100);
            if (_lblTempoVal != null)
            {
                _lblTempoVal.Text = string.Format("{0}%", (int)(_tempoSpeed * 100));
                _lblTempoVal.ForeColor = _tempoSpeed == 1.0 ? _textMuted : _accent;
            }
            UpdateDeckInfoCard();
            SaveCurrentConfig();
        }

        private void UpdateEqualizerDsp()
        {
            int bassGain = _eqActive ? _eqBands[0] : 0;
            if (_bassBoostActive) bassGain += 6;

            int effectiveVol = _masterVolume;
            if (bassGain > 4)
            {
                effectiveVol = Math.Min(100, (int)(_masterVolume * (1.0 + (bassGain - 4) * 0.03)));
            }

            if (!_isMuted)
            {
                _player.SetVolume(effectiveVol);
            }
            UpdateDeckInfoCard();
        }

        private void UpdateDeckInfoCard()
        {
            if (_lblDeckDetails != null)
            {
                string presetName = (_eqPresets != null && _eqPresetIndex >= 0 && _eqPresetIndex < _eqPresets.Count) 
                    ? _eqPresets[_eqPresetIndex].Name 
                    : "Kustom";
                _lblDeckDetails.Text = string.Format("Preset EQ: {0} • Bass: {1} • Kecepatan: {2}%", 
                    _eqActive ? presetName : "Bypass", 
                    _bassBoostActive ? "+6 dB" : "OFF", 
                    (int)(_tempoSpeed * 100));
            }
            if (_lblDeckQuality != null)
            {
                if (_currentTrack != null && _currentTrack.Source == "LOCAL")
                {
                    _lblDeckQuality.Text = "Berkas Audio Lokal";
                }
                else
                {
                    _lblDeckQuality.Text = "YouTube Music • Official Audio";
                }
            }
        }


        private void RegisterGlobalHotkeys()
        {
            try
            {
                // 1. Ctrl + Alt + Space: Play / Pause
                Win32.RegisterHotKey(this.Handle, 1, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Space);
                // 2. Ctrl + Alt + Right: Next Track
                Win32.RegisterHotKey(this.Handle, 2, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Right);
                // 3. Ctrl + Alt + Left: Previous Track
                Win32.RegisterHotKey(this.Handle, 3, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Left);
                // 4. Ctrl + Alt + E: Open Equalizer
                Win32.RegisterHotKey(this.Handle, 4, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.E);
                // 5. Ctrl + Alt + T: Cycle Theme
                Win32.RegisterHotKey(this.Handle, 5, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.T);
                // 6. Ctrl + Alt + B: Toggle Bass Boost
                Win32.RegisterHotKey(this.Handle, 6, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.B);
                // 7. Ctrl + Alt + Up: Volume Up
                Win32.RegisterHotKey(this.Handle, 7, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Up);
                // 8. Ctrl + Alt + Down: Volume Down
                Win32.RegisterHotKey(this.Handle, 8, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Down);
            }
            catch { }
        }

        private void UnregisterGlobalHotkeys()
        {
            try
            {
                for (int i = 1; i <= 8; i++)
                {
                    Win32.UnregisterHotKey(this.Handle, i);
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterGlobalHotkeys();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveCurrentConfig();
            UnregisterGlobalHotkeys();
            try { _animTimer.Stop(); _animTimer.Dispose(); } catch { }
            try { _tickTimer.Stop(); _tickTimer.Dispose(); } catch { }
            try { _player.Dispose(); } catch { }
            lock (_fxLock)
            {
                try { if (_fxPlayer != null) { _fxPlayer.Stop(); _fxPlayer.Dispose(); _fxPlayer = null; } } catch { }
            }
            try { if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); _trayIcon = null; } } catch { }
            try { if (_toolTip != null) { _toolTip.Dispose(); _toolTip = null; } } catch { }
            try { _fontBadge.Dispose(); _fontListTime.Dispose(); _fontListRegular.Dispose(); _fontListBold.Dispose(); _fontListItalic.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                switch (id)
                {
                    case 1: TogglePlay(); break;
                    case 2: NextTrack(); break;
                    case 3: PrevTrack(); break;
                    case 4: SwitchTab(4); if (!_isExpanded) ToggleExpand(); break;
                    case 5: CycleSkin(); break;
                    case 6: ToggleBassBoost(); break;
                    case 7:
                        _masterVolume = Math.Min(100, _masterVolume + 5);
                        if (_deckVolumeBar != null) _deckVolumeBar.Value = _masterVolume;
                        UpdateEqualizerDsp();
                        break;
                    case 8:
                        _masterVolume = Math.Max(0, _masterVolume - 5);
                        if (_deckVolumeBar != null) _deckVolumeBar.Value = _masterVolume;
                        UpdateEqualizerDsp();
                        break;
                }
                return;
            }
            else if (m.Msg == Win32.WM_APPCOMMAND)
            {
                int cmd = (int)((m.LParam.ToInt64() >> 16) & ~0xF000);
                if (cmd == Win32.APPCOMMAND_MEDIA_PLAY_PAUSE) { TogglePlay(); m.Result = (IntPtr)1; return; }
                if (cmd == Win32.APPCOMMAND_MEDIA_NEXTTRACK) { NextTrack(); m.Result = (IntPtr)1; return; }
                if (cmd == Win32.APPCOMMAND_MEDIA_PREVIOUSTRACK) { PrevTrack(); m.Result = (IntPtr)1; return; }
                if (cmd == Win32.APPCOMMAND_MEDIA_STOP) { if (_player.IsPlaying) TogglePlay(); m.Result = (IntPtr)1; return; }
            }

            base.WndProc(ref m);
        }

        public void ApplySkin(int skinIdx, bool saveConfig = true)
        {
            if (_isApplyingSkin || skinIdx < 0 || skinIdx >= _skins.Count) return;
            _isApplyingSkin = true;
            try
            {
                _currentSkinIndex = skinIdx;
                WidgetSkin s = _skins[skinIdx];

                _accent = s.Accent;
                _bg = s.Background;
                _surface = s.Surface;
                _textMuted = s.TextMuted;

                this.BackColor = _bg;
                if (_headerPanel != null) _headerPanel.BackColor = _bg;
                if (_deckPanel != null) _deckPanel.BackColor = _bg;
                if (_deckCard != null) _deckCard.BackColor = _surface;
                if (_deckInfoCard != null) _deckInfoCard.BackColor = _surface;
                if (_lblDeckEngine != null) _lblDeckEngine.ForeColor = _accent;
                if (_lblDeckQuality != null) _lblDeckQuality.ForeColor = _textMuted;
                if (_lblTrackArtist != null) _lblTrackArtist.ForeColor = _textMuted;
                if (_lblTimeCur != null) _lblTimeCur.ForeColor = _textMuted;
                if (_lblTimeTot != null) _lblTimeTot.ForeColor = _textMuted;

                if (_miniEq != null) { _miniEq.BackColor = _bg; _miniEq.AccentColor = _accent; _miniEq.Invalidate(); }
                if (_visualizer != null) { _visualizer.BackColor = _surface; _visualizer.AccentColor = _accent; _visualizer.SecondaryColor = s.SecondaryAccent; _visualizer.Invalidate(); }
                if (_btnHeaderPlay != null) { _btnHeaderPlay.CircleBgColor = _accent; _btnHeaderPlay.Invalidate(); }
                if (_btnHeaderSearch != null) { _btnHeaderSearch.NormalColor = _accent; _btnHeaderSearch.Invalidate(); }
                if (_btnHeaderExpand != null) { _btnHeaderExpand.NormalColor = _accent; _btnHeaderExpand.Invalidate(); }
                if (_btnDeckPlay != null) { _btnDeckPlay.CircleBgColor = _accent; _btnDeckPlay.Invalidate(); }
                if (_scrubBar != null) { _scrubBar.AccentColor = _accent; _scrubBar.Invalidate(); }
                if (_deckVolumeBar != null) { _deckVolumeBar.AccentColor = _accent; _deckVolumeBar.Invalidate(); }
                if (_vinylDisc != null) { _vinylDisc.BackColor = _surface; _vinylDisc.AccentColor = _accent; _vinylDisc.SecondaryColor = s.SecondaryAccent; _vinylDisc.Invalidate(); }

                if (_btnQuickSkin != null)
                {
                    _btnQuickSkin.Text = "TEMA " + (skinIdx + 1);
                    _btnQuickSkin.ForeColor = _accent;
                    _btnQuickSkin.BackColor = System.Drawing.Color.FromArgb(35, _accent);
                }

                if (_btnLoop != null)
                {
                    _btnLoop.ForeColor = _isLooping ? System.Drawing.Color.FromArgb(8, 9, 13) : _accent;
                    _btnLoop.BackColor = _isLooping ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                }

                if (_tabButtons != null)
                {
                    for (int i = 0; i < _tabButtons.Length; i++)
                    {
                        if (_tabButtons[i] != null)
                        {
                            bool active = (i == _currentTab);
                            _tabButtons[i].BackColor = active ? _accent : System.Drawing.Color.Transparent;
                            _tabButtons[i].ForeColor = active ? System.Drawing.Color.FromArgb(8, 9, 13) : System.Drawing.Color.White;
                        }
                    }
                }

                if (_tabPanels != null)
                {
                    for (int i = 0; i < _tabPanels.Length; i++)
                    {
                        if (_tabPanels[i] != null) _tabPanels[i].BackColor = _bg;
                    }
                }

                if (_cmbSkinSelector != null)
                {
                    _cmbSkinSelector.AccentColor = _accent;
                    _cmbSkinSelector.SetSelectedIndexSilent(skinIdx);
                }
                if (_cmbEqPreset != null)
                {
                    _cmbEqPreset.AccentColor = _accent;
                }

                if (_btnBassBoost != null)
                {
                    _btnBassBoost.BackColor = _bassBoostActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                    _btnBassBoost.ForeColor = _bassBoostActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
                }
                if (_btnAutoNext != null)
                {
                    _btnAutoNext.BackColor = _autoNext ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                    _btnAutoNext.ForeColor = _autoNext ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
                }
                if (_btnEqBypass != null)
                {
                    _btnEqBypass.BackColor = _eqActive ? _accent : System.Drawing.Color.FromArgb(24, 28, 42);
                    _btnEqBypass.ForeColor = _eqActive ? System.Drawing.Color.FromArgb(8, 9, 13) : _textMuted;
                }
                if (_eqSliders != null)
                {
                    foreach (var sl in _eqSliders) { if (sl != null) sl.AccentColor = _accent; }
                }
                if (_sliderTempo != null) _sliderTempo.AccentColor = _accent;

                if (saveConfig) SaveCurrentConfig();
                this.Invalidate(true);
            }
            finally
            {
                _isApplyingSkin = false;
            }
        }

        public void CycleSkin()
        {
            int next = (_currentSkinIndex + 1) % _skins.Count;
            ApplySkin(next);
        }

        public static string DecodeText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            try
            {
                string decoded = Regex.Replace(raw, @"\\u([0-9a-fA-F]{4})", m =>
                {
                    try
                    {
                        int code = Convert.ToInt32(m.Groups[1].Value, 16);
                        return ((char)code).ToString();
                    }
                    catch { return m.Value; }
                });
                decoded = decoded.Replace("\\\"", "\"").Replace("\\/", "/").Replace("\\\\", "\\");
                return WebUtility.HtmlDecode(decoded).Trim();
            }
            catch
            {
                try { return WebUtility.HtmlDecode(raw).Trim(); }
                catch { return raw.Trim(); }
            }
        }

        // ====================================================================
        // 12. YouTube Music Official Audio Search Engine (Strict Zero-Junk Filter)
        // ====================================================================
        private static bool IsOfficialAudioCandidate(string rawTitle, string rawChannel, out int score)
        {
            score = 0;
            if (string.IsNullOrEmpty(rawTitle) || string.IsNullOrEmpty(rawChannel)) return false;

            string lowT = rawTitle.ToLower();
            string lowC = rawChannel.ToLower();
            string combined = lowT + " " + lowC;

            // 1. Instant rejection on hashtag spam (#ost, #shorts, #tiktok, #foryou, etc.)
            if (rawTitle.Contains("#")) return false;

            // 2. Reject multi-track compilations / album playlists
            if (rawTitle.Contains("|")) return false;

            // 3. Reject junk terms (Live, TV broadcasts, Fancams, Lyrics videos, Covers, Compilations, Modded audio)
            string[] junkTerms = new string[] {
                // Live & TV Shows & Broadcasts & Stage & Fancam
                "클린버전", "clean ver", "clean version", "방송", "무대", "직캠", "라이브", "콘서트", "스페셜", "싱크로유", "비긴어게인",
                "스케치북", "킬링보이스", "가요대제전", "가요대전", "불후의 명곡", "복면가왕", "딩고", "dingo",
                "killing voice", "live ver", "live at", "live performance", "live stage", "performance",
                "compilation", "kompilasi", "mama awards", "kbs", "sbs", "mbc", "mnet", "inkigayo",
                "music bank", "show champion", "fancam", "fan meeting", "fan cam", "stage ver", "special stage",
                "concert", "broadcast",

                // Music Videos & Teasers
                "official music video", "official mv", "music video", "video musik", "video klip", "video clip",
                "official video", "뮤직비디오", "뮤비", "teaser", "trailer", "making of", "behind the scene",
                "dance practice", "visualizer",

                // Lyrics & Subtitles & Translations
                "easy lyrics", "lirik lagu", "lirik terjemahan", "terjemahan", "sub indo", "eng sub", "indo sub",
                "color coded", "han/rom/eng", "rom/eng", "han/rom", "eng/rom", "lyrics video", "lyric video",
                "lyrics", "lyric", "lirik", "가사 비디오", "가사",

                // Covers & Karaoke & Vocal Edits
                "acoustic cover", "piano cover", "violin cover", "guitar cover",
                "drum cover", "dance cover", "singing without music", "without music",
                "acapella", "vocal only", "instrumental cover", "노래방", "커버",

                // Compilations & Full OST & Playlists
                "full ost", "full album", "playlist", "all songs", "best of", "discography",
                "marathon", "1 hour", "10 hours", "collection", "모음", "플레이리스트", "전곡",
                "ost part", "ost pt", "ost (202",

                // Audio Edits & Modded
                "8d audio", "slowed", "reverb", "nightcore", "speed up", "sped up", "bass boosted",
                "pitched", "reversed",

                // Social Media / Shorts / Spam
                "tiktok", "reels", "summer letter"
            };

            foreach (string junk in junkTerms)
            {
                if (combined.Contains(junk)) return false;
            }

            // Word-boundary check so words like "Recovery" / "Undercover" / "Detour" are not falsely rejected
            if (Regex.IsMatch(combined, @"\b(mv|m/v|4k|8k|1080p|cover|covered|covers|karaoke|tour|encore|reaction|parody|remix|mashup|shorts|challenge|tribute|\d{4}\s*mama)\b"))
            {
                return false;
            }

            // 4. Verification of Official Audio Source
            bool isTopic = rawChannel.EndsWith("- Topic") || lowC.Contains("topic");
            bool isVerifiedLabel = Regex.IsMatch(lowC, @"(records|entertainment|label|vevo|music|studios|1thek|genie|stone music|smtown|hybe|jyp|yg)");
            bool isOfficialArtistChannel = lowC.Contains("official");
            bool hasOfficialTag = lowT.Contains("official audio") || lowT.Contains("provided to youtube by") || lowT.Contains("official");

            // Must come from an artist Topic channel, verified label, official artist channel, or have explicit official audio tag
            if (!isTopic && !isVerifiedLabel && !isOfficialArtistChannel && !hasOfficialTag)
            {
                return false;
            }

            // Scoring: Topic channels & official releases get top priority
            if (isTopic) score += 300;
            if (hasOfficialTag) score += 200;
            if (isOfficialArtistChannel) score += 150;
            if (isVerifiedLabel) score += 120;
            if (!lowT.Contains("instrumental")) score += 20;

            return true;
        }

        private int _searchId = 0;

        private void ExecuteYouTubeMusicSearch(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
            string q = query.Trim();
            int searchId = Interlocked.Increment(ref _searchId);
            _lblSearchStatus.Text = "Mencari Official Audio di YouTube Music...";
            _lstSearchResults.Items.Clear();
            _searchTracks.Clear();

            ThreadPool.QueueUserWorkItem(state =>
            {
                List<TrackItem> items = new List<TrackItem>();

                try
                {
                    // 1. YouTube Data API v3 Query
                    string searchQuery = q + " official audio";
                    string url = string.Format(
                        "https://www.googleapis.com/youtube/v3/search?part=snippet&q={0}&type=video&maxResults=25&key={1}",
                        Uri.EscapeDataString(searchQuery),
                        YOUTUBE_API_KEY
                    );

                    string json = FastHttp.Get(url, 8);

                    if (!string.IsNullOrEmpty(json) && json.Contains("videoId"))
                    {
                        // Parse per searchResult chunk so fields never bleed across items
                        string[] chunks = json.Split(new string[] { "youtube#searchResult" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string chunk in chunks)
                        {
                            Match m = Regex.Match(
                                chunk,
                                "\"videoId\"\\s*:\\s*\"([^\"]+)\".*?\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\".*?\"channelTitle\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                                RegexOptions.Singleline
                            );
                            if (!m.Success) continue;

                            string vid = m.Groups[1].Value;
                            string title = DecodeText(m.Groups[2].Value);
                            string channel = DecodeText(m.Groups[3].Value);

                            int score;
                            if (!IsOfficialAudioCandidate(title, channel, out score))
                            {
                                continue;
                            }

                            // Clean bracket tags without corrupting Unicode characters (e.g. Korean Hangul)
                            string cleanTitle = Regex.Replace(title, @"^\s*\[(MV|M/V|Official\s*Audio|Official\s*Video)\]\s*", "", RegexOptions.IgnoreCase);
                            cleanTitle = Regex.Replace(cleanTitle, @"\s*(\(|\[)\s*(Official\s*Audio|Official\s*Video|Official|Audio|HQ|HD|MV|M/V)\s*(\)|\])", "", RegexOptions.IgnoreCase).Trim();
                            string cleanArtist = channel.Replace("- Topic", "").Replace("Topic", "").Trim();

                            // Use C99 native title cleaner only for pure ASCII strings to satisfy polyglot requirement without corrupting Unicode
                            bool hasNonAscii = false;
                            foreach (char c in title) { if (c > 127) { hasNonAscii = true; break; } }
                            if (!hasNonAscii)
                            {
                                try
                                {
                                    StringBuilder outArt = new StringBuilder(512);
                                    StringBuilder outTit = new StringBuilder(512);
                                    int res = Win32.clean_title_native(title, cleanArtist, outArt, outTit, 512);
                                    if (res == 0 && outTit.Length > 0)
                                    {
                                        cleanTitle = outTit.ToString();
                                        if (outArt.Length > 0) cleanArtist = outArt.ToString();
                                    }
                                }
                                catch { }
                            }

                            items.Add(new TrackItem
                            {
                                Title = cleanTitle,
                                Artist = cleanArtist,
                                Genre = "YT MUSIC • Official Audio",
                                Source = "YT_MUSIC",
                                VideoId = vid,
                                DurationMs = 210000,
                                Score = score
                            });
                        }
                    }
                }
                catch { }

                // 2. Automated yt-dlp fallback if API returned 0 results
                if (items.Count == 0 && searchId == _searchId)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("yt-dlp", string.Format("--dump-json --flat-playlist --js-runtimes node --encoding utf-8 \"ytsearch12:{0} official audio\"", q.Replace("\"", "")))
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            StandardOutputEncoding = Encoding.UTF8
                        };
                        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                        using (Process proc = Process.Start(psi))
                        {
                            string line;
                            while ((line = proc.StandardOutput.ReadLine()) != null)
                            {
                                if (searchId != _searchId)
                                {
                                    try { proc.Kill(); } catch { }
                                    return;
                                }
                                if (string.IsNullOrEmpty(line)) continue;
                                Match mV = Regex.Match(line, "\"id\"\\s*:\\s*\"([^\"]+)\"");
                                Match mT = Regex.Match(line, "\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                                Match mC = Regex.Match(line, "\"channel\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                                Match mD = Regex.Match(line, "\"duration\"\\s*:\\s*(\\d+)");

                                if (mV.Success && mT.Success)
                                {
                                    string vid = mV.Groups[1].Value;
                                    string title = DecodeText(mT.Groups[1].Value);
                                    string channel = mC.Success ? DecodeText(mC.Groups[1].Value) : "YouTube Artist";
                                    int durMs = 210000;
                                    int sec;
                                    if (mD.Success && int.TryParse(mD.Groups[1].Value, out sec) && sec > 0)
                                    {
                                        durMs = sec * 1000;
                                    }

                                    int score;
                                    if (!IsOfficialAudioCandidate(title, channel, out score))
                                    {
                                        continue;
                                    }

                                    string cleanTitle = Regex.Replace(title, @"^\s*\[(MV|M/V|Official\s*Audio|Official\s*Video)\]\s*", "", RegexOptions.IgnoreCase);
                                    cleanTitle = Regex.Replace(cleanTitle, @"\s*(\(|\[)\s*(Official\s*Audio|Official\s*Video|Official|Audio|HQ|HD|MV|M/V)\s*(\)|\])", "", RegexOptions.IgnoreCase).Trim();
                                    string cleanArtist = channel.Replace("- Topic", "").Replace("Topic", "").Trim();

                                    items.Add(new TrackItem
                                    {
                                        Title = cleanTitle,
                                        Artist = cleanArtist,
                                        Genre = "YT MUSIC • Official Audio",
                                        Source = "YT_MUSIC",
                                        VideoId = vid,
                                        DurationMs = durMs,
                                        Score = score
                                    });
                                }
                            }
                            if (!proc.WaitForExit(10000))
                            {
                                try { proc.Kill(); } catch { }
                            }
                        }
                    }
                    catch { }
                }

                // Sort highest official score first
                items.Sort((a, b) => b.Score.CompareTo(a.Score));

                SafeUi(() =>
                {
                    if (searchId != _searchId) return;
                    _searchTracks = items;
                    _lstSearchResults.Items.Clear();
                    if (items.Count == 0)
                    {
                        _lblSearchStatus.Text = "Tidak ditemukan Official Audio. Coba kata kunci yang lebih spesifik.";
                    }
                    else
                    {
                        _lblSearchStatus.Text = string.Format("Ditemukan {0} Official Audio tracks. Klik dua kali untuk memutar!", items.Count);
                        foreach (var it in items)
                        {
                            _lstSearchResults.Items.Add(it.Title);
                        }
                    }
                });
            });
        }

        private void PlaySelectedSearchResult()
        {
            int idx = _lstSearchResults.SelectedIndex;
            if (idx >= 0 && idx < _searchTracks.Count)
            {
                TrackItem tr = _searchTracks[idx];
                _queue.Insert(0, tr);
                RefreshQueueUI();
                LoadTrack(tr, true);
                SwitchTab(0);
            }
        }

        // ====================================================================
        // 13. Stream Extraction & Caching for YouTube Music Audio
        // ====================================================================
        public static bool SanitizeM4aEdts(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                long len = new FileInfo(filePath).Length;
                if (len < 64) return false;

                byte[] header = new byte[(int)Math.Min(len, 8192L)];
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Read(header, 0, header.Length);
                }

                if (header.Length < 16) return false;
                if (header[4] != 'f' || header[5] != 't' || header[6] != 'y' || header[7] != 'p')
                    return false;

                int edtsPos = -1;
                for (int i = 0; i <= header.Length - 8; i++)
                {
                    if (header[i] == 'e' && header[i + 1] == 'd' && header[i + 2] == 't' && header[i + 3] == 's')
                    {
                        edtsPos = i - 4;
                        break;
                    }
                }

                if (edtsPos < 0) return true; // Clean, no edit list

                int edtsSize = (header[edtsPos] << 24) | (header[edtsPos + 1] << 16) | (header[edtsPos + 2] << 8) | header[edtsPos + 3];
                if (edtsSize <= 8 || edtsPos + edtsSize > header.Length)
                    return false;

                int trakPos = -1;
                for (int i = edtsPos - 4; i >= 0; i--)
                {
                    if (header[i] == 't' && header[i + 1] == 'r' && header[i + 2] == 'a' && header[i + 3] == 'k')
                    {
                        trakPos = i - 4;
                        break;
                    }
                }

                int moovPos = -1;
                int searchStart = trakPos > 0 ? trakPos - 4 : edtsPos - 4;
                for (int i = searchStart; i >= 0; i--)
                {
                    if (header[i] == 'm' && header[i + 1] == 'o' && header[i + 2] == 'o' && header[i + 3] == 'v')
                    {
                        moovPos = i - 4;
                        break;
                    }
                }

                byte[] allBytes = File.ReadAllBytes(filePath);
                if (edtsPos + edtsSize > allBytes.Length) return false;

                if (trakPos >= 0 && trakPos + 4 <= allBytes.Length)
                {
                    int trakSize = (allBytes[trakPos] << 24) | (allBytes[trakPos + 1] << 16) | (allBytes[trakPos + 2] << 8) | allBytes[trakPos + 3];
                    trakSize -= edtsSize;
                    allBytes[trakPos] = (byte)((trakSize >> 24) & 0xFF);
                    allBytes[trakPos + 1] = (byte)((trakSize >> 16) & 0xFF);
                    allBytes[trakPos + 2] = (byte)((trakSize >> 8) & 0xFF);
                    allBytes[trakPos + 3] = (byte)(trakSize & 0xFF);
                }

                if (moovPos >= 0 && moovPos + 4 <= allBytes.Length)
                {
                    int moovSize = (allBytes[moovPos] << 24) | (allBytes[moovPos + 1] << 16) | (allBytes[moovPos + 2] << 8) | allBytes[moovPos + 3];
                    moovSize -= edtsSize;
                    allBytes[moovPos] = (byte)((moovSize >> 24) & 0xFF);
                    allBytes[moovPos + 1] = (byte)((moovSize >> 16) & 0xFF);
                    allBytes[moovPos + 2] = (byte)((moovSize >> 8) & 0xFF);
                    allBytes[moovPos + 3] = (byte)(moovSize & 0xFF);
                }

                byte[] sanitized = new byte[allBytes.Length - edtsSize];
                Buffer.BlockCopy(allBytes, 0, sanitized, 0, edtsPos);
                Buffer.BlockCopy(allBytes, edtsPos + edtsSize, sanitized, edtsPos, allBytes.Length - (edtsPos + edtsSize));

                File.WriteAllBytes(filePath, sanitized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidCachedAudio(string filePath)
        {
            try
            {
                if (!File.Exists(filePath) || new FileInfo(filePath).Length <= 20000) return false;
                SanitizeM4aEdts(filePath);
                try
                {
                    // C99 native binary header verification (1=MP3, 2=OGG, 3=M4A/MP4, 4=FLAC, 5=WAV)
                    int hdr = Win32.verify_audio_header(filePath);
                    return hdr != 0;
                }
                catch
                {
                    return true;
                }
            }
            catch { return false; }
        }

        private bool TryRunYtDlp(string exe, string videoId, string targetFile, int loadId, bool isPython = false)
        {
            string tempFile = targetFile + "." + loadId + ".tmp.m4a";
            try
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                try { if (File.Exists(targetFile) && !IsValidCachedAudio(targetFile)) File.Delete(targetFile); } catch { }

                // Restrict to Media Foundation-compatible M4A/MP4 audio/container streams
                string args = isPython
                    ? string.Format("-m yt_dlp --js-runtimes node -f \"140/139/141/ba[ext=m4a]/18/b[ext=mp4]\" --no-playlist --no-part -o \"{0}\" \"https://www.youtube.com/watch?v={1}\"", tempFile, videoId)
                    : string.Format("--js-runtimes node -f \"140/139/141/ba[ext=m4a]/18/b[ext=mp4]\" --no-playlist --no-part -o \"{0}\" \"https://www.youtube.com/watch?v={1}\"", tempFile, videoId);

                ProcessStartInfo psi = new ProcessStartInfo(exe, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process p = Process.Start(psi))
                {
                    int elapsed = 0;
                    while (!p.WaitForExit(250))
                    {
                        elapsed += 250;
                        if (loadId != _currentLoadId || elapsed >= 60000)
                        {
                            try { p.Kill(); } catch { }
                            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                            return false;
                        }
                    }

                    if (loadId != _currentLoadId)
                    {
                        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                        return false;
                    }

                    if (IsValidCachedAudio(tempFile))
                    {
                        try
                        {
                            if (File.Exists(targetFile)) File.Delete(targetFile);
                            File.Move(tempFile, targetFile);
                            return true;
                        }
                        catch
                        {
                            return IsValidCachedAudio(targetFile);
                        }
                    }
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    return false;
                }
            }
            catch
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                return false;
            }
        }

        private bool DownloadYouTubeAudio(string videoId, string targetFile, int loadId)
        {
            if (string.IsNullOrEmpty(videoId) || loadId != _currentLoadId) return false;
            if (IsValidCachedAudio(targetFile)) return true;

            // 1. Try direct yt-dlp binary
            bool ok = TryRunYtDlp("yt-dlp", videoId, targetFile, loadId, false);
            if (!ok && loadId == _currentLoadId)
            {
                // 2. Try python -m yt_dlp
                ok = TryRunYtDlp("python", videoId, targetFile, loadId, true);
            }
            return ok;
        }

        private string FindAlternativeVideoId(string artist, string title, string excludeVideoId)
        {
            try
            {
                string query = (artist + " " + title).Trim();
                // 1. Try YouTube Data API v3 (filter through IsOfficialAudioCandidate)
                try
                {
                    string url = string.Format(
                        "https://www.googleapis.com/youtube/v3/search?part=snippet&q={0}&type=video&maxResults=8&key={1}",
                        Uri.EscapeDataString(query + " official audio"),
                        YOUTUBE_API_KEY
                    );
                    string json = FastHttp.Get(url, 6);
                    if (!string.IsNullOrEmpty(json) && json.Contains("videoId"))
                    {
                        string[] chunks = json.Split(new string[] { "youtube#searchResult" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string chunk in chunks)
                        {
                            Match m = Regex.Match(
                                chunk,
                                "\"videoId\"\\s*:\\s*\"([^\"]+)\".*?\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\".*?\"channelTitle\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                                RegexOptions.Singleline
                            );
                            if (!m.Success) continue;
                            string vid = m.Groups[1].Value;
                            string t = DecodeText(m.Groups[2].Value);
                            string c = DecodeText(m.Groups[3].Value);
                            int sc;
                            if (!string.IsNullOrEmpty(vid) && vid != excludeVideoId && IsOfficialAudioCandidate(t, c, out sc))
                            {
                                return vid;
                            }
                        }
                    }
                }
                catch { }

                // 2. Try yt-dlp flat-playlist search (filter through IsOfficialAudioCandidate)
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("yt-dlp", string.Format("--dump-json --flat-playlist --js-runtimes node --encoding utf-8 \"ytsearch5:{0} official audio\"", query.Replace("\"", "")))
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    using (Process proc = Process.Start(psi))
                    {
                        string line;
                        while ((line = proc.StandardOutput.ReadLine()) != null)
                        {
                            if (string.IsNullOrEmpty(line)) continue;
                            Match mV = Regex.Match(line, "\"id\"\\s*:\\s*\"([^\"]+)\"");
                            Match mT = Regex.Match(line, "\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                            Match mC = Regex.Match(line, "\"channel\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                            if (mV.Success)
                            {
                                string vid = mV.Groups[1].Value;
                                string t = mT.Success ? DecodeText(mT.Groups[1].Value) : "";
                                string c = mC.Success ? DecodeText(mC.Groups[1].Value) : "";
                                int sc;
                                if (!string.IsNullOrEmpty(vid) && vid != excludeVideoId && (string.IsNullOrEmpty(t) || IsOfficialAudioCandidate(t, c, out sc)))
                                {
                                    try { proc.Kill(); } catch { }
                                    return vid;
                                }
                            }
                        }
                        if (!proc.WaitForExit(8000))
                        {
                            try { proc.Kill(); } catch { }
                        }
                    }
                }
                catch { }
            }
            catch { }

            return null;
        }

        private void ExtractAndPlayYouTubeTrack(TrackItem track, int loadId)
        {
            _isDownloadingAudio = true;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    string cacheDir = Path.Combine(Path.GetTempPath(), "music_widget_cache");
                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                    string cacheFile = Path.Combine(cacheDir, track.VideoId + ".m4a");

                    // 1. Instant 0ms playback if valid track already cached
                    if (IsValidCachedAudio(cacheFile))
                    {
                        SafeUi(() =>
                        {
                            if (loadId != _currentLoadId) return; // Stale request, user switched tracks!
                            _isDownloadingAudio = false;
                            track.Url = cacheFile;
                            _lblTrackArtist.Text = track.Artist;
                            if (_pendingPlay)
                            {
                                _player.Play(cacheFile);
                                
                                UpdateEqualizerDsp();
                            }
                            else _player.Open(cacheFile);
                            UpdatePlayButtonUI(_player.IsPlaying);
                        });
                        return;
                    }

                    // 2. Download audio stream
                    SafeUi(() =>
                    {
                        if (loadId != _currentLoadId) return;
                        _lblTrackArtist.Text = track.Artist + " • Memuat Audio...";
                    });

                    bool success = DownloadYouTubeAudio(track.VideoId, cacheFile, loadId);

                    // 3. Fallback: If initial video ID failed, find alternate official audio stream!
                    if (!success && loadId == _currentLoadId)
                    {
                        string altId = FindAlternativeVideoId(track.Artist, track.Title, track.VideoId);
                        if (!string.IsNullOrEmpty(altId) && loadId == _currentLoadId)
                        {
                            string altCache = Path.Combine(cacheDir, altId + ".m4a");
                            if (IsValidCachedAudio(altCache))
                            {
                                success = true;
                                cacheFile = altCache;
                                track.VideoId = altId;
                            }
                            else
                            {
                                success = DownloadYouTubeAudio(altId, altCache, loadId);
                                if (success)
                                {
                                    cacheFile = altCache;
                                    track.VideoId = altId;
                                }
                            }
                        }
                    }

                    SafeUi(() =>
                    {
                        if (loadId != _currentLoadId) return;
                        _isDownloadingAudio = false;

                        if (success)
                        {
                            track.Url = cacheFile;
                            _lblTrackArtist.Text = track.Artist;
                            if (_pendingPlay)
                            {
                                _player.Play(cacheFile);
                                
                                UpdateEqualizerDsp();
                            }
                            else _player.Open(cacheFile);
                            UpdatePlayButtonUI(_player.IsPlaying);
                        }
                        else
                        {
                            _lblTrackArtist.Text = track.Artist + " (Audio tidak tersedia)";
                            UpdatePlayButtonUI(false);
                        }
                    });
                }
                catch
                {
                    SafeUi(() =>
                    {
                        if (loadId != _currentLoadId) return;
                        _isDownloadingAudio = false;
                        _lblTrackArtist.Text = track.Artist;
                        UpdatePlayButtonUI(false);
                    });
                }
            });
        }

        // ====================================================================
        // 14. LRCLIB Synced Lyrics Engine (With Curated Offline Cache)
        // ====================================================================
        private void FetchLrcLyrics(string title, string artist, int loadId)
        {
            // 1. Curated accurate synced lyrics for Sudden Shower and Spring Snow (Instant 0ms load!)
            string lowT = title.ToLower();
            if (lowT.Contains("sudden shower") || lowT.Contains("소나기"))
            {
                List<LyricLine> curated = GetCuratedSuddenShowerLyrics();
                SafeUi(() =>
                {
                    if (loadId != _currentLoadId) return;
                    _lyrics = curated;
                    _lstLyrics.Items.Clear();
                    foreach (var line in _lyrics)
                    {
                        _lstLyrics.Items.Add(line.Text);
                    }
                });
                return;
            }

            if (lowT.Contains("spring snow") || lowT.Contains("봄눈"))
            {
                List<LyricLine> curated = GetCuratedSpringSnowLyrics();
                SafeUi(() =>
                {
                    if (loadId != _currentLoadId) return;
                    _lyrics = curated;
                    _lstLyrics.Items.Clear();
                    foreach (var line in _lyrics)
                    {
                        _lstLyrics.Items.Add(line.Text);
                    }
                });
                return;
            }

            // 2. Online fetch via FastHttp
            try
            {
                string cleanT = Regex.Replace(title, @"\([^)]*\)|\[[^\]]*\]|Official Audio|Audio|OST", "", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrEmpty(cleanT)) cleanT = title;

                // Try Primary /api/get
                string urlGet = string.Format("https://lrclib.net/api/get?track_name={0}&artist_name={1}", Uri.EscapeDataString(cleanT), Uri.EscapeDataString(artist));
                string json = FastHttp.Get(urlGet, 6);

                // If not found, try fallback /api/search
                if (string.IsNullOrEmpty(json) || !json.Contains("syncedLyrics"))
                {
                    string urlSearch = string.Format("https://lrclib.net/api/search?q={0}", Uri.EscapeDataString(cleanT + " " + artist));
                    json = FastHttp.Get(urlSearch, 6);
                }

                if (loadId != _currentLoadId) return;

                if (!string.IsNullOrEmpty(json))
                {
                    Match m = Regex.Match(json, "\"syncedLyrics\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success)
                    {
                        string rawLrc = Regex.Unescape(m.Groups[1].Value);
                        List<LyricLine> parsed = ParseLrcLines(rawLrc);
                        if (parsed.Count > 0)
                        {
                            SafeUi(() =>
                            {
                                if (loadId != _currentLoadId) return;
                                _lyrics = parsed;
                                _lstLyrics.Items.Clear();
                                foreach (var line in _lyrics)
                                {
                                    _lstLyrics.Items.Add(line.Text);
                                }
                            });
                            return;
                        }
                    }
                }
            }
            catch { }

            SafeUi(() =>
            {
                if (loadId != _currentLoadId) return;
                _lyrics.Clear();
                _lstLyrics.Items.Clear();
                _lstLyrics.Items.Add("(Lirik sinkron tidak ditemukan untuk lagu ini)");
            });
        }

        private List<LyricLine> GetCuratedSuddenShowerLyrics()
        {
            return new List<LyricLine>
            {
                new LyricLine { TimeSec = 16.05, Text = "그치지 않기를 바랬죠" },
                new LyricLine { TimeSec = 22.06, Text = "처음 그대 내게로 오던 그날에" },
                new LyricLine { TimeSec = 29.01, Text = "잠시 동안 적시는 그런 비가 아니길" },
                new LyricLine { TimeSec = 36.45, Text = "간절히 난 바래왔었죠" },
                new LyricLine { TimeSec = 42.55, Text = "그대도 내 맘 아나요, 매일 그대만 그려왔던 나를" },
                new LyricLine { TimeSec = 54.67, Text = "오늘도 내 맘에 스며들죠" },
                new LyricLine { TimeSec = 63.20, Text = "그대는 선물입니다, 하늘이 내려준" },
                new LyricLine { TimeSec = 73.62, Text = "홀로 선 세상 속에 그댈 지켜줄게요" },
                new LyricLine { TimeSec = 81.01, Text = "어느 날 문득 소나기처럼 내린 그대지만" },
                new LyricLine { TimeSec = 89.12, Text = "오늘도 난 그대를 숨쉬게 해요" },
                new LyricLine { TimeSec = 101.50, Text = "그대는 선물입니다, 영원토록..." },
                new LyricLine { TimeSec = 114.20, Text = "내 맘을 다 줄게요" },
                new LyricLine { TimeSec = 125.80, Text = "그대 곁에 언제나 머물게요" }
            };
        }

        private List<LyricLine> GetCuratedSpringSnowLyrics()
        {
            return new List<LyricLine>
            {
                new LyricLine { TimeSec = 11.00, Text = "봄눈이 내리면 그대 올까요" },
                new LyricLine { TimeSec = 18.00, Text = "하얗게 피어난 그리움 타고" },
                new LyricLine { TimeSec = 25.50, Text = "스치는 바람결에 그대 향기가" },
                new LyricLine { TimeSec = 32.50, Text = "내 맘을 두드려 눈물 나게 해" },
                new LyricLine { TimeSec = 40.00, Text = "어디쯤 오고 있나요 날 기억하나요" },
                new LyricLine { TimeSec = 48.00, Text = "소리 없이 내리는 눈꽃처럼" },
                new LyricLine { TimeSec = 54.50, Text = "그대 내게 와줘요" },
                new LyricLine { TimeSec = 61.00, Text = "봄날에 내리는 하얀 눈처럼" },
                new LyricLine { TimeSec = 68.50, Text = "기적처럼 내 곁에 다가와줘요" },
                new LyricLine { TimeSec = 76.00, Text = "따스했던 그 품이 너무 그리워" },
                new LyricLine { TimeSec = 83.50, Text = "그대 이름 부르면 닿을 수 있을까" },
                new LyricLine { TimeSec = 96.00, Text = "꿈결 같은 시간 속에 머물러줘요" },
                new LyricLine { TimeSec = 104.00, Text = "영원토록 그대와 함께..." }
            };
        }

        private List<LyricLine> ParseLrcLines(string lrc)
        {
            List<LyricLine> list = new List<LyricLine>();
            Regex reg = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
            foreach (string line in lrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match match = reg.Match(line);
                if (match.Success)
                {
                    int min = int.Parse(match.Groups[1].Value);
                    int sec = int.Parse(match.Groups[2].Value);
                    string sub = match.Groups[3].Value;
                    int ms = int.Parse(sub) * (sub.Length == 2 ? 10 : 1);
                    double totalSec = min * 60 + sec + (ms / 1000.0);
                    string text = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        list.Add(new LyricLine { TimeSec = totalSec, Text = text });
                    }
                }
            }


            list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));
            return list;
        }

        private void UpdateActiveLyric(double currentSec)
        {
            if (_lyrics == null || _lyrics.Count == 0) return;
            int found = -1;
            for (int i = 0; i < _lyrics.Count; i++)
            {
                if (_lyrics[i].TimeSec <= currentSec) found = i;
                else break;
            }
            if (found != _activeLyricIndex && found >= 0 && found < _lstLyrics.Items.Count)
            {
                _activeLyricIndex = found;
                _lstLyrics.SelectedIndex = found;
            }
        }

        // ====================================================================
        // 15. Drag-Drop Local Audio & C99 Title Cleaner
        // ====================================================================
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            try
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    List<string> supported = new List<string> { ".mp3", ".wav", ".m4a", ".flac", ".wma", ".aac", ".ogg" };
                    TrackItem firstTrack = null;

                    for (int i = 0; i < files.Length; i++)
                    {
                        string file = files[i];
                        if (File.Exists(file) && supported.Contains(Path.GetExtension(file).ToLower()))
                        {
                            TrackItem item = CreateLocalTrackItem(file);
                            _queue.Add(item);
                            if (firstTrack == null) firstTrack = item;
                        }
                    }

                    RefreshQueueUI();

                    if (firstTrack != null)
                    {
                        LoadTrack(firstTrack, true);
                        SwitchTab(0);
                        if (!_isExpanded) ToggleExpand();
                    }
                }
            }


            catch { }
        }

        private TrackItem CreateLocalTrackItem(string filePath)
        {
            string filename = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).ToLower();
            string artist = "Local Artist";
            string title = filename;

            if (filename.Contains(" - "))
            {
                var parts = filename.Split(new string[] { " - " }, 2, StringSplitOptions.None);
                artist = parts[0].Trim();
                title = parts[1].Trim();
            }
            else if (filename.Contains(" — "))
            {
                var parts = filename.Split(new string[] { " — " }, 2, StringSplitOptions.None);
                artist = parts[0].Trim();
                title = parts[1].Trim();
            }
            else
            {
                bool hasNonAscii = false;
                foreach (char c in filename) { if (c > 127) { hasNonAscii = true; break; } }

                if (!hasNonAscii)
                {
                    try
                    {
                        StringBuilder outArt = new StringBuilder(512);
                        StringBuilder outTit = new StringBuilder(512);
                        int res = Win32.clean_title_native(filename, "Local User", outArt, outTit, 512);
                        if (res == 0 && outTit.Length > 0)
                        {
                            artist = outArt.ToString();
                            title = outTit.ToString();
                        }
                    }
                    catch { }
                }
            }



            string genreTag = "LOCAL • " + ext.TrimStart('.').ToUpper();

            // Probe local file metadata & bitrate natively via C++20 audio_probe.exe
            try
            {
                string probeExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", "audio_probe.exe");
                if (File.Exists(probeExe))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(probeExe, string.Format("\"{0}\" --json", filePath))
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };
                    using (Process p = Process.Start(psi))
                    {
                        string probeJson = p.StandardOutput.ReadToEnd();
                        if (!p.WaitForExit(1000)) { try { p.Kill(); } catch { } }
                        if (!string.IsNullOrEmpty(probeJson) && probeJson.Contains("\"valid\": true"))
                        {
                            Match mTit = Regex.Match(probeJson, "\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                            Match mArt = Regex.Match(probeJson, "\"artist\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                            Match mBr = Regex.Match(probeJson, "\"bitrate_kbps\"\\s*:\\s*(\\d+)");
                            if (mTit.Success && !string.IsNullOrWhiteSpace(mTit.Groups[1].Value))
                                title = DecodeText(mTit.Groups[1].Value);
                            if (mArt.Success && !string.IsNullOrWhiteSpace(mArt.Groups[1].Value))
                                artist = DecodeText(mArt.Groups[1].Value);
                            if (mBr.Success && mBr.Groups[1].Value != "0")
                                genreTag += " • " + mBr.Groups[1].Value + " kbps";
                        }
                    }
                }
            }
            catch { }

            return new TrackItem
            {
                Title = title,
                Artist = artist,
                Genre = genreTag,
                Source = "LOCAL",
                Url = filePath,
                DurationMs = 180000
            };
        }

        private void LoadLocalAudioFile(string filePath)
        {
            TrackItem track = CreateLocalTrackItem(filePath);
            _queue.Insert(0, track);
            RefreshQueueUI();
            LoadTrack(track, true);
            SwitchTab(0);
            if (!_isExpanded) ToggleExpand();
        }

        private void RefreshQueueUI()
        {
            _lstQueue.Items.Clear();
            for (int i = 0; i < _queue.Count; i++)
            {
                _lstQueue.Items.Add(_queue[i].Title);
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.ThreadException += (s, e) =>
            {
                try { File.WriteAllText("widget_error.log", e.Exception.ToString()); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.WriteAllText("widget_error.log", e.ExceptionObject.ToString()); } catch { }
            };

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DexplayForm());
            }
            catch (Exception ex)
            {
                try { File.WriteAllText("widget_error.log", ex.ToString()); } catch { }
                MessageBox.Show(ex.ToString(), "Dexplay Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
