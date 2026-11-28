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
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;

[assembly: AssemblyTitle("Dexplay")]
[assembly: AssemblyDescription("Crystal Clear Desktop Dynamic Music Island & Audio Player")]
[assembly: AssemblyCompany("Dexplay")]
[assembly: AssemblyProduct("Dexplay Desktop Music Island")]
[assembly: AssemblyCopyright("Copyright © 2026 Dexplay")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace Dexplay
{
    // ========================================================================
    // 0. Crystal Clear Glassmorphic Graphics Helpers
    // ========================================================================
    public static class GlassGfx
    {
        public static void ApplyCrisp(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath CreateRoundRect(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            float r = Math.Max(0.5f, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));
            float d = r * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static System.Drawing.Color Blend(System.Drawing.Color a, System.Drawing.Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int alpha = (int)(a.A + (b.A - a.A) * t);
            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bl = (int)(a.B + (b.B - a.B) * t);
            return System.Drawing.Color.FromArgb(alpha, r, g, bl);
        }

        public static void PaintGlassCard(Graphics g, Rectangle bounds, int radius,
            System.Drawing.Color topColor, System.Drawing.Color bottomColor,
            System.Drawing.Color borderColor, System.Drawing.Color topHighlight,
            System.Drawing.Color accentGlow)
        {
            if (bounds.Width <= 2 || bounds.Height <= 2) return;
            ApplyCrisp(g);
            RectangleF rect = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1f, bounds.Height - 1f);
            using (GraphicsPath path = CreateRoundRect(rect, radius))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(bounds, topColor, bottomColor, 90f))
                {
                    g.FillPath(fill, path);
                }

                // Soft upper glass sheen
                Rectangle sheenRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Max(16, bounds.Height / 2));
                using (GraphicsPath sheenPath = CreateRoundRect(new RectangleF(bounds.X + 1f, bounds.Y + 1f, bounds.Width - 2f, sheenRect.Height), Math.Max(4, radius - 1)))
                using (LinearGradientBrush sheen = new LinearGradientBrush(sheenRect,
                    System.Drawing.Color.FromArgb(20, 255, 255, 255),
                    System.Drawing.Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    g.FillPath(sheen, sheenPath);
                }

                // Subtle accent inner tint at top-left
                if (accentGlow.A > 0)
                {
                    using (LinearGradientBrush glow = new LinearGradientBrush(bounds,
                        System.Drawing.Color.FromArgb(Math.Min(36, (int)accentGlow.A), accentGlow),
                        System.Drawing.Color.FromArgb(0, accentGlow), 35f))
                    {
                        g.FillPath(glow, path);
                    }
                }

                // Luminous 1px glass rim (brighter on top edge, softer on bottom)
                using (LinearGradientBrush rimBrush = new LinearGradientBrush(bounds, topHighlight, borderColor, 90f))
                using (System.Drawing.Pen rimPen = new System.Drawing.Pen(rimBrush, 1f))
                {
                    g.DrawPath(rimPen, path);
                }
            }
        }
    }
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
    // 2. High-Fidelity Crystal Vector Button
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
        Volume,
        Repeat,
        Clear
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
        public System.Drawing.Color SecondaryColor { get; set; }
        private bool _isHovered = false;
        private bool _isPressed = false;

        public VectorButton(VectorIconType type, int x, int y, int w, int h)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.IconType = type;
            this.Location = new System.Drawing.Point(x, y);
            this.Size = new System.Drawing.Size(w, h);
            this.BackColor = System.Drawing.Color.Transparent;
            this.NormalColor = System.Drawing.Color.FromArgb(182, 198, 220);
            this.HoverColor = System.Drawing.Color.White;
            this.ActiveColor = System.Drawing.Color.FromArgb(103, 232, 249);
            this.CircleBgColor = System.Drawing.Color.Transparent;
            this.SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
            this.IsCircleButton = false;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;

            this.MouseEnter += (s, e) => { _isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; _isPressed = false; this.Invalidate(); };
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isPressed = true; this.Invalidate(); } };
            this.MouseUp += (s, e) => { _isPressed = false; this.Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            if (IsCircleButton && CircleBgColor != System.Drawing.Color.Transparent)
            {
                float pad = _isPressed ? 2.5f : 1.5f;
                RectangleF orbRect = new RectangleF(pad, pad, this.Width - pad * 2f - 1f, this.Height - pad * 2f - 1f);
                System.Drawing.Color topCol = _isHovered ? GlassGfx.Blend(CircleBgColor, System.Drawing.Color.White, 0.35f) : GlassGfx.Blend(CircleBgColor, SecondaryColor, 0.25f);
                System.Drawing.Color botCol = _isHovered ? CircleBgColor : GlassGfx.Blend(CircleBgColor, System.Drawing.Color.Black, 0.16f);

                if (_isHovered)
                {
                    using (SolidBrush halo = new SolidBrush(System.Drawing.Color.FromArgb(46, CircleBgColor)))
                    {
                        g.FillEllipse(halo, 0f, 0f, this.Width - 1f, this.Height - 1f);
                    }
                }

                using (LinearGradientBrush orbBrush = new LinearGradientBrush(orbRect, topCol, botCol, 90f))
                {
                    g.FillEllipse(orbBrush, orbRect);
                }

                // Specular glass highlight on top half of orb
                RectangleF shineRect = new RectangleF(orbRect.X + 3f, orbRect.Y + 1.5f, orbRect.Width - 6f, orbRect.Height * 0.48f);
                if (shineRect.Width > 2f && shineRect.Height > 2f)
                {
                    using (LinearGradientBrush shineBrush = new LinearGradientBrush(shineRect,
                        System.Drawing.Color.FromArgb(115, 255, 255, 255),
                        System.Drawing.Color.FromArgb(0, 255, 255, 255), 90f))
                    {
                        g.FillEllipse(shineBrush, shineRect);
                    }
                }

                using (System.Drawing.Pen rimPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 255, 255, 255), 1f))
                {
                    g.DrawEllipse(rimPen, orbRect);
                }
            }
            else if (_isHovered || IsActive)
            {
                RectangleF capRect = new RectangleF(1.5f, 1.5f, this.Width - 3f, this.Height - 3f);
                using (GraphicsPath capPath = GlassGfx.CreateRoundRect(capRect, Math.Min(this.Width, this.Height) / 2f))
                {
                    System.Drawing.Color fillCol = IsActive
                        ? System.Drawing.Color.FromArgb(_isHovered ? 48 : 34, ActiveColor)
                        : System.Drawing.Color.FromArgb(_isPressed ? 42 : 28, 255, 255, 255);
                    using (SolidBrush bgB = new SolidBrush(fillCol))
                    {
                        g.FillPath(bgB, capPath);
                    }
                    using (System.Drawing.Pen rimB = new System.Drawing.Pen(IsActive
                        ? System.Drawing.Color.FromArgb(110, ActiveColor)
                        : System.Drawing.Color.FromArgb(65, 255, 255, 255), 1f))
                    {
                        g.DrawPath(rimB, capPath);
                    }
                }
            }

            System.Drawing.Color icCol;
            if (IsCircleButton && CircleBgColor != System.Drawing.Color.Transparent)
            {
                icCol = System.Drawing.Color.FromArgb(10, 14, 22);
            }
            else
            {
                icCol = IsActive ? ActiveColor : (_isHovered ? HoverColor : NormalColor);
            }

            using (System.Drawing.Pen pen = new System.Drawing.Pen(icCol, 1.8f))
            using (SolidBrush brush = new SolidBrush(icCol))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float cx = this.Width / 2f;
                float cy = this.Height / 2f;

                switch (IconType)
                {
                    case VectorIconType.Play:
                        float po = IsCircleButton ? 1f : 0.5f;
                        float ps = IsCircleButton && this.Width >= 40 ? 7f : 5.5f;
                        g.FillPolygon(brush, new PointF[] {
                            new PointF(cx - ps * 0.65f + po, cy - ps),
                            new PointF(cx - ps * 0.65f + po, cy + ps),
                            new PointF(cx + ps * 0.95f + po, cy)
                        });
                        break;
                    case VectorIconType.Pause:
                        float ph = (IsCircleButton && this.Width >= 40) ? 14f : 11f;
                        float pw = (IsCircleButton && this.Width >= 40) ? 3.5f : 2.8f;
                        g.FillRectangle(brush, cx - pw - 1.5f, cy - ph / 2f, pw, ph);
                        g.FillRectangle(brush, cx + 1.5f, cy - ph / 2f, pw, ph);
                        break;
                    case VectorIconType.Search:
                        pen.Width = 1.8f;
                        g.DrawEllipse(pen, cx - 5.5f, cy - 6f, 8.5f, 8.5f);
                        g.DrawLine(pen, cx + 1.5f, cy + 1f, cx + 5.5f, cy + 5f);
                        break;
                    case VectorIconType.ChevronDown:
                        pen.Width = 1.9f;
                        g.DrawLines(pen, new PointF[] {
                            new PointF(cx - 4.5f, cy - 2f),
                            new PointF(cx, cy + 2.8f),
                            new PointF(cx + 4.5f, cy - 2f)
                        });
                        break;
                    case VectorIconType.ChevronUp:
                        pen.Width = 1.9f;
                        g.DrawLines(pen, new PointF[] {
                            new PointF(cx - 4.5f, cy + 2f),
                            new PointF(cx, cy - 2.8f),
                            new PointF(cx + 4.5f, cy + 2f)
                        });
                        break;
                    case VectorIconType.Pin:
                        pen.Width = 1.6f;
                        g.DrawLine(pen, cx - 3.5f, cy - 5f, cx + 3.5f, cy - 5f);
                        g.DrawLine(pen, cx - 2.5f, cy - 5f, cx - 2.5f, cy);
                        g.DrawLine(pen, cx + 2.5f, cy - 5f, cx + 2.5f, cy);
                        g.DrawLine(pen, cx - 4.5f, cy, cx + 4.5f, cy);
                        g.DrawLine(pen, cx, cy, cx, cy + 5.5f);
                        if (IsActive) g.FillRectangle(brush, cx - 2.5f, cy - 5f, 5f, 5f);
                        break;
                    case VectorIconType.Island:
                        pen.Width = 1.6f;
                        using (GraphicsPath path = GlassGfx.CreateRoundRect(new RectangleF(cx - 7f, cy - 4f, 14f, 8f), 4f))
                        {
                            if (IsActive) g.FillPath(brush, path);
                            else g.DrawPath(pen, path);
                        }
                        break;
                    case VectorIconType.Close:
                        pen.Width = 1.8f;
                        g.DrawLine(pen, cx - 4f, cy - 4f, cx + 4f, cy + 4f);
                        g.DrawLine(pen, cx + 4f, cy - 4f, cx - 4f, cy + 4f);
                        break;
                    case VectorIconType.Clear:
                        pen.Width = 1.6f;
                        g.DrawLine(pen, cx - 3.5f, cy - 3.5f, cx + 3.5f, cy + 3.5f);
                        g.DrawLine(pen, cx + 3.5f, cy - 3.5f, cx - 3.5f, cy + 3.5f);
                        break;
                    case VectorIconType.Prev:
                        g.FillPolygon(brush, new PointF[] { new PointF(cx + 0.5f, cy - 5.5f), new PointF(cx + 0.5f, cy + 5.5f), new PointF(cx - 5f, cy) });
                        g.FillPolygon(brush, new PointF[] { new PointF(cx + 5.5f, cy - 5.5f), new PointF(cx + 5.5f, cy + 5.5f), new PointF(cx, cy) });
                        break;
                    case VectorIconType.Next:
                        g.FillPolygon(brush, new PointF[] { new PointF(cx - 5.5f, cy - 5.5f), new PointF(cx - 5.5f, cy + 5.5f), new PointF(cx, cy) });
                        g.FillPolygon(brush, new PointF[] { new PointF(cx - 0.5f, cy - 5.5f), new PointF(cx - 0.5f, cy + 5.5f), new PointF(cx + 5f, cy) });
                        break;
                    case VectorIconType.Volume:
                        pen.Width = 1.6f;
                        g.FillPolygon(brush, new PointF[] {
                            new PointF(cx - 5.5f, cy - 2.5f),
                            new PointF(cx - 2.5f, cy - 2.5f),
                            new PointF(cx + 1.5f, cy - 5.5f),
                            new PointF(cx + 1.5f, cy + 5.5f),
                            new PointF(cx - 2.5f, cy + 2.5f),
                            new PointF(cx - 5.5f, cy + 2.5f)
                        });
                        if (IsActive)
                        {
                            pen.Color = System.Drawing.Color.FromArgb(248, 113, 113);
                            g.DrawLine(pen, cx + 3.5f, cy - 2.5f, cx + 7.5f, cy + 2.5f);
                            g.DrawLine(pen, cx + 7.5f, cy - 2.5f, cx + 3.5f, cy + 2.5f);
                        }
                        else
                        {
                            g.DrawArc(pen, cx + 0.5f, cy - 3.5f, 5.5f, 7f, -55, 110);
                        }
                        break;
                    case VectorIconType.Repeat:
                        pen.Width = 1.6f;
                        g.DrawArc(pen, cx - 5.5f, cy - 4.5f, 11f, 9f, 200, 145);
                        g.DrawArc(pen, cx - 5.5f, cy - 4.5f, 11f, 9f, 20, 145);
                        break;
                }
            }
        }
    }

    // ========================================================================
    // 3. Mini Header Audio Equalizer Indicator (Crystal Capsule Bars)
    // ========================================================================
    public class MiniEqualizer : Panel
    {
        private float[] _bars = new float[4] { 4f, 7f, 5f, 3f };
        private double _phase = 0;
        private bool _wasPlaying = false;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
        public bool IsPlaying = false;

        public MiniEqualizer()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.Transparent;
        }

        public void UpdateFrame()
        {
            if (!IsPlaying && !_wasPlaying) return;
            _wasPlaying = IsPlaying;

            _phase += 0.24;
            for (int i = 0; i < _bars.Length; i++)
            {
                if (IsPlaying)
                    _bars[i] = (float)((Math.Sin(_phase * 2.1 + i * 0.95) * 0.5 + 0.5) * 12f + 3f);
                else
                    _bars[i] = 3.5f;
            }
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            int h = this.Height;
            float barW = 2.6f;
            float gap = 2.6f;
            float startX = (this.Width - (_bars.Length * (barW + gap) - gap)) / 2f;

            for (int i = 0; i < _bars.Length; i++)
            {
                float bh = Math.Min(h - 3f, Math.Max(3f, _bars[i]));
                float bx = startX + i * (barW + gap);
                float by = (h - bh) / 2f;
                RectangleF r = new RectangleF(bx, by, barW, bh);
                using (GraphicsPath p = GlassGfx.CreateRoundRect(r, barW / 2f))
                using (LinearGradientBrush b = new LinearGradientBrush(r, SecondaryColor, AccentColor, 90f))
                {
                    g.FillPath(b, p);
                }
            }
        }
    }

    // ========================================================================
    // 4. Crystal Glass Card, Pill Buttons, Segmented Bar, Sliders & Search
    // ========================================================================
    public class GlassCard : Panel
    {
        public int CornerRadius { get; set; }
        public System.Drawing.Color TopColor { get; set; }
        public System.Drawing.Color BottomColor { get; set; }
        public System.Drawing.Color BorderColor { get; set; }
        public System.Drawing.Color TopHighlightColor { get; set; }
        public System.Drawing.Color AccentGlowColor { get; set; }

        public GlassCard()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.BackColor = System.Drawing.Color.Transparent;
            this.CornerRadius = 14;
            this.TopColor = System.Drawing.Color.FromArgb(24, 34, 54);
            this.BottomColor = System.Drawing.Color.FromArgb(14, 21, 35);
            this.BorderColor = System.Drawing.Color.FromArgb(36, 255, 255, 255);
            this.TopHighlightColor = System.Drawing.Color.FromArgb(90, 255, 255, 255);
            this.AccentGlowColor = System.Drawing.Color.FromArgb(24, 103, 232, 249);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            GlassGfx.PaintGlassCard(e.Graphics, this.ClientRectangle, CornerRadius,
                TopColor, BottomColor, BorderColor, TopHighlightColor, AccentGlowColor);
        }
    }

    public class GlassButton : Control
    {
        public int CornerRadius { get; set; }
        public bool IsPill { get; set; }
        public bool IsActive { get; set; }
        public System.Drawing.Color AccentColor { get; set; }
        public System.Drawing.Color SecondaryColor { get; set; }
        public System.Drawing.Color IdleTopColor { get; set; }
        public System.Drawing.Color IdleBottomColor { get; set; }
        public System.Drawing.Color IdleTextColor { get; set; }
        public System.Drawing.Color ActiveTextColor { get; set; }

        private bool _isHovered = false;
        private bool _isPressed = false;

        public GlassButton()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold);
            this.CornerRadius = 9;
            this.IsPill = false;
            this.AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
            this.SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
            this.IdleTopColor = System.Drawing.Color.FromArgb(32, 44, 68);
            this.IdleBottomColor = System.Drawing.Color.FromArgb(20, 29, 46);
            this.IdleTextColor = System.Drawing.Color.FromArgb(215, 230, 245);
            this.ActiveTextColor = System.Drawing.Color.FromArgb(10, 14, 22);

            this.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; _isPressed = false; Invalidate(); };
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } };
            this.MouseUp += (s, e) => { _isPressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);
            if (this.Width <= 2 || this.Height <= 2) return;

            float radius = IsPill ? (this.Height - 2f) / 2f : CornerRadius;
            RectangleF rect = new RectangleF(0.5f, 0.5f, this.Width - 1f, this.Height - 1f);

            using (GraphicsPath path = GlassGfx.CreateRoundRect(rect, radius))
            {
                System.Drawing.Color topCol;
                System.Drawing.Color botCol;
                System.Drawing.Color borderTop;
                System.Drawing.Color borderBot;

                if (IsActive)
                {
                    topCol = _isHovered ? GlassGfx.Blend(AccentColor, System.Drawing.Color.White, 0.25f) : GlassGfx.Blend(AccentColor, SecondaryColor, 0.22f);
                    botCol = _isPressed ? GlassGfx.Blend(AccentColor, System.Drawing.Color.Black, 0.18f) : AccentColor;
                    borderTop = System.Drawing.Color.FromArgb(180, 255, 255, 255);
                    borderBot = System.Drawing.Color.FromArgb(120, AccentColor);
                }
                else
                {
                    float lift = _isPressed ? 0.02f : (_isHovered ? 0.16f : 0f);
                    topCol = GlassGfx.Blend(IdleTopColor, System.Drawing.Color.White, lift);
                    botCol = GlassGfx.Blend(IdleBottomColor, System.Drawing.Color.White, lift * 0.6f);
                    borderTop = _isHovered ? System.Drawing.Color.FromArgb(145, AccentColor) : System.Drawing.Color.FromArgb(85, 255, 255, 255);
                    borderBot = _isHovered ? System.Drawing.Color.FromArgb(80, AccentColor) : System.Drawing.Color.FromArgb(30, 255, 255, 255);
                }

                using (LinearGradientBrush bg = new LinearGradientBrush(this.ClientRectangle, topCol, botCol, 90f))
                {
                    g.FillPath(bg, path);
                }

                // Upper glass specular sheen
                RectangleF sheenR = new RectangleF(1.5f, 1f, this.Width - 3f, Math.Max(6f, this.Height * 0.45f));
                using (GraphicsPath sheenP = GlassGfx.CreateRoundRect(sheenR, Math.Max(3f, radius - 1f)))
                using (LinearGradientBrush sheen = new LinearGradientBrush(sheenR,
                    System.Drawing.Color.FromArgb(IsActive ? 70 : 28, 255, 255, 255),
                    System.Drawing.Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    g.FillPath(sheen, sheenP);
                }

                using (LinearGradientBrush rim = new LinearGradientBrush(this.ClientRectangle, borderTop, borderBot, 90f))
                using (System.Drawing.Pen pen = new System.Drawing.Pen(rim, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            System.Drawing.Color fg = IsActive ? ActiveTextColor : (_isHovered ? System.Drawing.Color.White : IdleTextColor);
            TextRenderer.DrawText(g, this.Text, this.Font, this.ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    public class CrystalSegmentedBar : Control
    {
        private string[] _labels = new string[] { "DECK", "SEARCH", "LYRICS", "QUEUE", "MASHUP", "STUDIO" };
        private int _selectedIndex = 0;
        private int _hoverIndex = -1;

        public System.Drawing.Color AccentColor { get; set; }
        public System.Drawing.Color SecondaryColor { get; set; }
        public System.Drawing.Color ContainerTop { get; set; }
        public System.Drawing.Color ContainerBottom { get; set; }

        public event Action<int> TabSelected;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _labels.Length && _selectedIndex != value)
                {
                    _selectedIndex = value;
                    Invalidate();
                }
            }
        }

        public CrystalSegmentedBar()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold);
            this.AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
            this.SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
            this.ContainerTop = System.Drawing.Color.FromArgb(24, 34, 54);
            this.ContainerBottom = System.Drawing.Color.FromArgb(15, 22, 36);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.X);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                int idx = HitTest(e.X);
                if (idx >= 0 && idx < _labels.Length)
                {
                    _selectedIndex = idx;
                    Invalidate();
                    if (TabSelected != null) TabSelected(idx);
                }
            }
        }

        private int HitTest(int x)
        {
            int pad = 4;
            float segW = (this.Width - pad * 2f) / _labels.Length;
            if (segW <= 0) return -1;
            int idx = (int)((x - pad) / segW);
            if (idx < 0 || idx >= _labels.Length) return -1;
            return idx;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);
            if (this.Width <= 4 || this.Height <= 4) return;

            GlassGfx.PaintGlassCard(g, this.ClientRectangle, 12,
                ContainerTop, ContainerBottom,
                System.Drawing.Color.FromArgb(35, 255, 255, 255),
                System.Drawing.Color.FromArgb(95, 255, 255, 255),
                System.Drawing.Color.FromArgb(14, AccentColor));

            int pad = 4;
            float segW = (this.Width - pad * 2f) / _labels.Length;
            float segH = this.Height - pad * 2f;

            for (int i = 0; i < _labels.Length; i++)
            {
                float sx = pad + i * segW + 1f;
                RectangleF segRect = new RectangleF(sx, pad, segW - 2f, segH);

                if (i == _selectedIndex)
                {
                    using (GraphicsPath p = GlassGfx.CreateRoundRect(segRect, 9f))
                    using (LinearGradientBrush fill = new LinearGradientBrush(segRect,
                        GlassGfx.Blend(AccentColor, SecondaryColor, 0.25f), AccentColor, 90f))
                    using (System.Drawing.Pen rim = new System.Drawing.Pen(System.Drawing.Color.FromArgb(175, 255, 255, 255), 1f))
                    {
                        g.FillPath(fill, p);
                        g.DrawPath(rim, p);
                    }
                    TextRenderer.DrawText(g, _labels[i], this.Font, Rectangle.Round(segRect),
                        System.Drawing.Color.FromArgb(10, 14, 22),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    if (i == _hoverIndex)
                    {
                        using (GraphicsPath p = GlassGfx.CreateRoundRect(segRect, 9f))
                        using (SolidBrush hoverB = new SolidBrush(System.Drawing.Color.FromArgb(24, 255, 255, 255)))
                        {
                            g.FillPath(hoverB, p);
                        }
                    }
                    System.Drawing.Color txtCol = (i == _hoverIndex)
                        ? System.Drawing.Color.White
                        : System.Drawing.Color.FromArgb(178, 196, 218);
                    TextRenderer.DrawText(g, _labels[i], this.Font, Rectangle.Round(segRect), txtCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }

    // ========================================================================
    // 4b. Crystal Pill Slider Control (Gradient Fill, Halo Thumb & Center Zero)
    // ========================================================================
    public class DarkSlider : Control
    {
        private int _min = 0;
        private int _max = 100;
        private int _val = 0;
        private bool _isDragging = false;
        private bool _isHovered = false;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
        public System.Drawing.Color TrackColor = System.Drawing.Color.FromArgb(40, 255, 255, 255);
        public System.Drawing.Color ThumbColor = System.Drawing.Color.White;
        public bool IsCenterZero = false;

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
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.Transparent;
            this.Cursor = Cursors.Hand;

            this.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
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
            int pad = 9;
            int trackW = this.Width - pad * 2;
            if (trackW <= 0) return;
            float pct = Math.Max(0f, Math.Min(1f, (float)(mouseX - pad) / trackW));
            this.Value = (int)Math.Round(_min + pct * (_max - _min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            int pad = 9;
            float trackW = Math.Max(4f, this.Width - pad * 2f);
            float trackH = (_isHovered || _isDragging) ? 6f : 4.5f;
            float cy = this.Height / 2f;
            float trackY = cy - trackH / 2f;

            // Glass Track Pill
            RectangleF trackRect = new RectangleF(pad, trackY, trackW, trackH);
            using (GraphicsPath trackPath = GlassGfx.CreateRoundRect(trackRect, trackH / 2f))
            using (SolidBrush bgB = new SolidBrush(System.Drawing.Color.FromArgb(42, 255, 255, 255)))
            using (System.Drawing.Pen rimB = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 255, 255, 255), 0.8f))
            {
                g.FillPath(bgB, trackPath);
                g.DrawPath(rimB, trackPath);
            }

            float pct = (_max > _min) ? (float)(_val - _min) / (_max - _min) : 0f;
            float thumbX = pad + pct * trackW;

            if (IsCenterZero && _min < 0 && _max > 0)
            {
                float zeroPct = (float)(0 - _min) / (_max - _min);
                float zeroX = pad + zeroPct * trackW;

                // Subtle center zero tick mark
                using (System.Drawing.Pen zPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 255, 255, 255), 1.2f))
                {
                    g.DrawLine(zPen, zeroX, cy - 6f, zeroX, cy + 6f);
                }

                float fillStart = Math.Min(zeroX, thumbX);
                float fillWidth = Math.Abs(thumbX - zeroX);
                if (fillWidth > 1f)
                {
                    RectangleF fillRect = new RectangleF(fillStart, trackY, fillWidth, trackH);
                    using (GraphicsPath fillPath = GlassGfx.CreateRoundRect(fillRect, trackH / 2f))
                    using (LinearGradientBrush actB = new LinearGradientBrush(trackRect, AccentColor, SecondaryColor, 0f))
                    {
                        g.FillPath(actB, fillPath);
                    }
                }
            }
            else
            {
                float fillW = pct * trackW;
                if (fillW > 1f)
                {
                    RectangleF fillRect = new RectangleF(pad, trackY, fillW, trackH);
                    using (GraphicsPath fillPath = GlassGfx.CreateRoundRect(fillRect, trackH / 2f))
                    using (LinearGradientBrush actB = new LinearGradientBrush(trackRect, AccentColor, SecondaryColor, 0f))
                    {
                        g.FillPath(actB, fillPath);
                    }
                }
            }

            // Luminous Crystal Thumb
            float thumbR = (_isDragging || _isHovered) ? 6f : 4.8f;
            if (_isDragging || _isHovered)
            {
                using (SolidBrush haloB = new SolidBrush(System.Drawing.Color.FromArgb(_isDragging ? 95 : 55, AccentColor)))
                {
                    g.FillEllipse(haloB, thumbX - thumbR - 3.5f, cy - thumbR - 3.5f, (thumbR + 3.5f) * 2f, (thumbR + 3.5f) * 2f);
                }
            }

            using (SolidBrush thB = new SolidBrush(ThumbColor))
            using (System.Drawing.Pen thRim = new System.Drawing.Pen(AccentColor, 1.3f))
            {
                g.FillEllipse(thB, thumbX - thumbR, cy - thumbR, thumbR * 2f, thumbR * 2f);
                g.DrawEllipse(thRim, thumbX - thumbR, cy - thumbR, thumbR * 2f, thumbR * 2f);
            }
        }
    }

    // ========================================================================
    // 4c. Dark Mode ListBox with Native Dark Scrollbars & Double Buffering
    // ========================================================================
    public class DarkListBox : ListBox
    {
        public DarkListBox()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.BorderStyle = BorderStyle.None;
            this.BackColor = System.Drawing.Color.FromArgb(14, 21, 35);
            this.ForeColor = System.Drawing.Color.White;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Win32.SetWindowTheme(this.Handle, "DarkMode_Explorer", null); } catch { }
        }
    }

    // ========================================================================
    // 4d. Crystal Glass Dropdown Selector
    // ========================================================================
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private DarkSelect _owner;
        public DarkMenuRenderer(DarkSelect owner) : base(new DarkColorTable()) { _owner = owner; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            GlassGfx.ApplyCrisp(e.Graphics);
            using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(95, 255, 255, 255), 1f))
            {
                e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            GlassGfx.ApplyCrisp(e.Graphics);
            RectangleF rc = new RectangleF(4f, 2f, e.Item.Width - 8f, e.Item.Height - 4f);
            bool isCurrent = (e.Item.Tag is int) && ((int)e.Item.Tag == _owner.SelectedIndex);
            System.Drawing.Color bg = e.Item.Selected
                ? _owner.AccentColor
                : (isCurrent ? System.Drawing.Color.FromArgb(32, 48, 76) : System.Drawing.Color.FromArgb(18, 25, 40));
            using (GraphicsPath p = GlassGfx.CreateRoundRect(rc, 6f))
            using (SolidBrush b = new SolidBrush(bg))
            {
                e.Graphics.FillPath(b, p);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool isCurrent = (e.Item.Tag is int) && ((int)e.Item.Tag == _owner.SelectedIndex);
            System.Drawing.Color fg = e.Item.Selected
                ? System.Drawing.Color.FromArgb(10, 14, 22)
                : (isCurrent ? _owner.AccentColor : System.Drawing.Color.FromArgb(232, 240, 250));
            Rectangle rect = new Rectangle(14, 0, e.Item.Width - 22, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground { get { return System.Drawing.Color.FromArgb(18, 25, 40); } }
        public override System.Drawing.Color MenuBorder { get { return System.Drawing.Color.FromArgb(70, 95, 130); } }
        public override System.Drawing.Color ImageMarginGradientBegin { get { return System.Drawing.Color.FromArgb(18, 25, 40); } }
        public override System.Drawing.Color ImageMarginGradientMiddle { get { return System.Drawing.Color.FromArgb(18, 25, 40); } }
        public override System.Drawing.Color ImageMarginGradientEnd { get { return System.Drawing.Color.FromArgb(18, 25, 40); } }
    }

    public class DarkSelect : Panel
    {
        private List<string> _items = new List<string>();
        private List<System.Drawing.Color> _itemDots = new List<System.Drawing.Color>();
        private int _selectedIndex = -1;
        private bool _isHovered = false;
        private System.Drawing.Color _accentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        private ContextMenuStrip _menu;

        public string PlaceholderText { get; set; }
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
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Cursor = Cursors.Hand;
            this.BackColor = System.Drawing.Color.Transparent;
            this.ForeColor = System.Drawing.Color.White;
            this.Font = new System.Drawing.Font("Segoe UI", 9f);

            _menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = System.Drawing.Color.FromArgb(18, 25, 40),
                ForeColor = System.Drawing.Color.White,
                Renderer = new DarkMenuRenderer(this)
            };

            this.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            this.Click += (s, e) => ShowDropdown();
        }

        public void ClearItems()
        {
            _items.Clear();
            _itemDots.Clear();
            _selectedIndex = -1;
            Invalidate();
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
                    Width = Math.Max(200, this.Width),
                    Height = 30
                };
                mi.Click += (s, e) => { SelectedIndex = idx; };
                _menu.Items.Add(mi);
            }
            _menu.Show(this, new System.Drawing.Point(0, this.Height + 3));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            GlassGfx.PaintGlassCard(g, this.ClientRectangle, 9,
                System.Drawing.Color.FromArgb(30, 42, 66),
                System.Drawing.Color.FromArgb(20, 28, 46),
                _isHovered ? System.Drawing.Color.FromArgb(120, _accentColor) : System.Drawing.Color.FromArgb(40, 255, 255, 255),
                _isHovered ? System.Drawing.Color.FromArgb(190, _accentColor) : System.Drawing.Color.FromArgb(95, 255, 255, 255),
                System.Drawing.Color.FromArgb(18, _accentColor));

            int textStartX = 12;
            if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            {
                System.Drawing.Color dot = _itemDots[_selectedIndex];
                if (!dot.IsEmpty)
                {
                    using (SolidBrush halo = new SolidBrush(System.Drawing.Color.FromArgb(65, dot)))
                    using (SolidBrush dotB = new SolidBrush(dot))
                    {
                        g.FillEllipse(halo, 10, (this.Height - 14) / 2, 14, 14);
                        g.FillEllipse(dotB, 12, (this.Height - 10) / 2, 10, 10);
                    }
                    textStartX = 30;
                }
                Rectangle textRect = new Rectangle(textStartX, 0, this.Width - textStartX - 30, this.Height);
                TextRenderer.DrawText(g, _items[_selectedIndex], this.Font, textRect, this.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else if (!string.IsNullOrEmpty(PlaceholderText))
            {
                Rectangle textRect = new Rectangle(textStartX, 0, this.Width - textStartX - 30, this.Height);
                TextRenderer.DrawText(g, PlaceholderText, this.Font, textRect, System.Drawing.Color.FromArgb(150, 185, 215),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            int ax = this.Width - 16;
            int ay = this.Height / 2 - 2;
            using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(_isHovered ? _accentColor : System.Drawing.Color.FromArgb(170, 190, 215), 1.7f))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                g.DrawLine(arrowPen, ax - 4, ay, ax, ay + 4);
                g.DrawLine(arrowPen, ax, ay + 4, ax + 4, ay);
            }
        }
    }

    // ========================================================================
    // 4e. Crystal Glass Search Input Pill
    // ========================================================================
    public class CrystalSearchBox : GlassCard
    {
        public TextBox InnerTextBox { get; private set; }
        public VectorButton ClearButton { get; private set; }
        public System.Drawing.Color AccentColor { get; set; }
        public string Placeholder { get; set; }
        private bool _focused = false;

        public CrystalSearchBox()
        {
            this.CornerRadius = 11;
            this.AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
            this.Placeholder = "Search songs, artists, or official audio...";
            this.TopColor = System.Drawing.Color.FromArgb(28, 39, 60);
            this.BottomColor = System.Drawing.Color.FromArgb(18, 26, 42);

            this.InnerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = System.Drawing.Color.FromArgb(22, 31, 49),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };

            this.ClearButton = new VectorButton(VectorIconType.Clear, 0, 0, 24, 24)
            {
                Visible = false,
                NormalColor = System.Drawing.Color.FromArgb(148, 163, 184),
                HoverColor = System.Drawing.Color.White
            };
            this.ClearButton.Click += (s, e) =>
            {
                this.InnerTextBox.Text = "";
                this.InnerTextBox.Focus();
            };

            this.InnerTextBox.GotFocus += (s, e) => { _focused = true; UpdateBorderTint(); };
            this.InnerTextBox.LostFocus += (s, e) => { _focused = false; UpdateBorderTint(); };
            this.InnerTextBox.TextChanged += (s, e) =>
            {
                this.ClearButton.Visible = !string.IsNullOrEmpty(this.InnerTextBox.Text);
                this.Invalidate();
            };

            this.Controls.Add(this.InnerTextBox);
            this.Controls.Add(this.ClearButton);
            this.Click += (s, e) => this.InnerTextBox.Focus();
        }

        private void UpdateBorderTint()
        {
            this.TopHighlightColor = _focused
                ? System.Drawing.Color.FromArgb(185, AccentColor)
                : System.Drawing.Color.FromArgb(90, 255, 255, 255);
            this.BorderColor = _focused
                ? System.Drawing.Color.FromArgb(115, AccentColor)
                : System.Drawing.Color.FromArgb(36, 255, 255, 255);
            this.Invalidate();
        }

        public void SyncColors(System.Drawing.Color surface, System.Drawing.Color accent)
        {
            this.AccentColor = accent;
            System.Drawing.Color innerBg = GlassGfx.Blend(surface, System.Drawing.Color.White, 0.04f);
            this.TopColor = GlassGfx.Blend(surface, System.Drawing.Color.White, 0.06f);
            this.BottomColor = surface;
            this.InnerTextBox.BackColor = innerBg;
            this.AccentGlowColor = System.Drawing.Color.FromArgb(24, accent);
            UpdateBorderTint();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.InnerTextBox != null)
            {
                int tbY = Math.Max(4, (this.Height - this.InnerTextBox.Height) / 2);
                this.InnerTextBox.Location = new System.Drawing.Point(34, tbY);
                this.InnerTextBox.Width = Math.Max(40, this.Width - 66);
            }
            if (this.ClearButton != null)
            {
                this.ClearButton.Location = new System.Drawing.Point(this.Width - 30, (this.Height - 24) / 2);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            // Left magnifying glass icon
            float cx = 18f;
            float cy = this.Height / 2f;
            using (System.Drawing.Pen pen = new System.Drawing.Pen(_focused ? AccentColor : System.Drawing.Color.FromArgb(150, 175, 205), 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - 5f, cy - 5.5f, 8f, 8f);
                g.DrawLine(pen, cx + 1.5f, cy + 1f, cx + 5f, cy + 4.5f);
            }

            if (string.IsNullOrEmpty(this.InnerTextBox.Text) && !this.InnerTextBox.Focused)
            {
                Rectangle phRect = new Rectangle(34, 0, Math.Max(20, this.Width - 46), this.Height);
                TextRenderer.DrawText(g, Placeholder, this.InnerTextBox.Font, phRect,
                    System.Drawing.Color.FromArgb(135, 155, 185),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
        private bool _pauseOnOpen = false;
        private bool _hasOpenedMedia = false;
        private DateTime _playbackStartTime = DateTime.MinValue;

        public event Action MediaEnded;
        public event Action<int> MediaOpened;
        public event Action MediaFailed;

        public bool IsPlaying { get { return _isPlaying; } }
        public bool HasOpenedMedia { get { return _hasOpenedMedia; } }
        public double SpeedRatio { get { return _speedRatio; } }

        public NativeAudioPlayer()
        {
            _player.Volume = 0.75;
            _player.SpeedRatio = 1.0;

            _player.MediaOpened += (s, e) =>
            {
                if (_pauseOnOpen)
                {
                    try
                    {
                        _player.Pause();
                        _player.Position = TimeSpan.Zero;
                    }
                    catch { }
                    _isPlaying = false;
                    _suppressMediaEnded = true;
                }
                else
                {
                    _suppressMediaEnded = false;
                    _playbackStartTime = DateTime.UtcNow;
                }
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
                _hasOpenedMedia = false;
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
                _pauseOnOpen = false;
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
                _pauseOnOpen = true;
                _suppressMediaEnded = true;
                _isPlaying = false;
                _hasOpenedMedia = true;
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
                try { _player.Pause(); } catch { }
            }
            catch { }
        }

        public void Play(string pathOrUrl)
        {
            try
            {
                _pauseOnOpen = false;
                _suppressMediaEnded = true;
                _hasOpenedMedia = true;
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
                _pauseOnOpen = false;
                _suppressMediaEnded = true;
                _isPlaying = false;
                _hasOpenedMedia = false;
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
                    _pauseOnOpen = false;
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

        public void SetVolumeExact(double volume01)
        {
            try
            {
                _player.Volume = Math.Max(0.0, Math.Min(1.0, volume01));
            }
            catch { }
        }

        public void SetBalance(double balance)
        {
            try
            {
                _player.Balance = Math.Max(-1.0, Math.Min(1.0, balance));
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
    // 5b. Native Windows Media Foundation PCM Decoder & Pro BPM / Cue Analyzer
    // ========================================================================
    public static class MediaFoundationDecoder
    {
        private const int MF_VERSION = 0x00020070;
        private const int MF_SOURCE_READER_FIRST_AUDIO_STREAM = unchecked((int)0xFFFFFFFD);
        private const int MF_SOURCE_READERF_ENDOFSTREAM = 0x00000002;

        private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00AA00389B71");
        private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00aa00389b71");
        private static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
        private static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
        private static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int dwFlags);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFShutdown();

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int MFCreateSourceReaderFromURL(string pwszURL, IntPtr pAttributes, out IMFSourceReader ppSourceReader);

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555")]
        private interface IMFMediaType
        {
            [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
            [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
            [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
            [PreserveSig] int Compare(IntPtr pTheirs, int matchType, out bool pbResult);
            [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
            [PreserveSig] int GetUINT64(ref Guid guidKey, out long punValue);
            [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
            [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
            [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
            [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, out int pcchLength);
            [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
            [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
            [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize, out int pcbBlobSize);
            [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
            [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid guidKey);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
            [PreserveSig] int SetUINT64(ref Guid guidKey, long unValue);
            [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
            [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
            [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
            [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize);
            [PreserveSig] int SetUnknown(ref Guid guidKey, IntPtr pUnknown);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int pcItems);
            [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
            [PreserveSig] int CopyAllItems(IntPtr pDest);
            [PreserveSig] int GetMajorType(out Guid pguidMajorType);
            [PreserveSig] int IsCompressedFormat(out bool pfCompressed);
            [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out int pdwFlags);
            [PreserveSig] int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
            [PreserveSig] int FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
        private interface IMFSample
        {
            [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
            [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
            [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
            [PreserveSig] int Compare(IntPtr pTheirs, int matchType, out bool pbResult);
            [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
            [PreserveSig] int GetUINT64(ref Guid guidKey, out long punValue);
            [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
            [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
            [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
            [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, out int pcchLength);
            [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
            [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
            [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize, out int pcbBlobSize);
            [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
            [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid guidKey);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
            [PreserveSig] int SetUINT64(ref Guid guidKey, long unValue);
            [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
            [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
            [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
            [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize);
            [PreserveSig] int SetUnknown(ref Guid guidKey, IntPtr pUnknown);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int pcItems);
            [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
            [PreserveSig] int CopyAllItems(IntPtr pDest);
            [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
            [PreserveSig] int SetSampleFlags(int dwSampleFlags);
            [PreserveSig] int GetSampleTime(out long phnsSampleTime);
            [PreserveSig] int SetSampleTime(long hnsSampleTime);
            [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
            [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
            [PreserveSig] int GetBufferCount(out int pdwBufferCount);
            [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
            [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
            [PreserveSig] int RemoveBufferByIndex(int dwIndex);
            [PreserveSig] int RemoveAllBuffers();
            [PreserveSig] int GetTotalLength(out int pcbTotalLength);
            [PreserveSig] int CopyToBuffer(IntPtr pBuffer, int cbBuffer);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("045FA593-8799-42b8-BC8D-8968C6453507")]
        private interface IMFMediaBuffer
        {
            [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
            [PreserveSig] int Unlock();
            [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
            [PreserveSig] int SetCurrentLength(int cbCurrentLength);
            [PreserveSig] int GetMaxLength(out int pcbMaxLength);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
        private interface IMFSourceReader
        {
            [PreserveSig] int GetStreamSelection(int dwStreamIndex, out bool pfSelected);
            [PreserveSig] int SetStreamSelection(int dwStreamIndex, bool fSelected);
            [PreserveSig] int GetNativeMediaType(int dwStreamIndex, int dwMediaTypeIndex, out IMFMediaType ppMediaType);
            [PreserveSig] int GetCurrentMediaType(int dwStreamIndex, out IMFMediaType ppMediaType);
            [PreserveSig] int SetCurrentMediaType(int dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);
            [PreserveSig] int SetCurrentPosition(ref Guid guidTimeFormat, IntPtr varPosition);
            [PreserveSig] int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex, out int pdwStreamFlags, out long pllTimestamp, out IMFSample ppSample);
            [PreserveSig] int Flush(int dwStreamIndex);
            [PreserveSig] int GetServiceForStream(int dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
            [PreserveSig] int GetPresentationAttribute(int dwStreamIndex, ref Guid guidKey, IntPtr pvarAttribute);
        }

        /// <summary>
        /// Decodes an audio file into 44.1kHz stereo signed 16-bit interleaved PCM samples.
        /// Caps maxSeconds when > 0 to keep analysis fast and memory-light.
        /// </summary>
        public static short[] DecodeStereoPcm16(string filePath, int maxSeconds = 0, int sampleRate = 44100)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            bool mfStarted = false;
            IMFSourceReader reader = null;
            IMFMediaType mediaType = null;
            try
            {
                int hr = MFStartup(MF_VERSION, 0);
                if (hr >= 0) mfStarted = true;

                hr = MFCreateSourceReaderFromURL(filePath, IntPtr.Zero, out reader);
                if (hr < 0 || reader == null) return TryReadWavFallback(filePath, maxSeconds);

                hr = MFCreateMediaType(out mediaType);
                if (hr < 0 || mediaType == null) return TryReadWavFallback(filePath, maxSeconds);

                Guid gMajor = MFMediaType_Audio;
                Guid gPcm = MFAudioFormat_PCM;
                Guid kMajor = MF_MT_MAJOR_TYPE;
                Guid kSub = MF_MT_SUBTYPE;
                Guid kCh = MF_MT_AUDIO_NUM_CHANNELS;
                Guid kSr = MF_MT_AUDIO_SAMPLES_PER_SECOND;
                Guid kBits = MF_MT_AUDIO_BITS_PER_SAMPLE;

                mediaType.SetGUID(ref kMajor, ref gMajor);
                mediaType.SetGUID(ref kSub, ref gPcm);
                mediaType.SetUINT32(ref kCh, 2);
                mediaType.SetUINT32(ref kSr, sampleRate);
                mediaType.SetUINT32(ref kBits, 16);

                hr = reader.SetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, IntPtr.Zero, mediaType);
                if (hr < 0) return TryReadWavFallback(filePath, maxSeconds);

                int maxSamples = maxSeconds > 0 ? maxSeconds * sampleRate * 2 : int.MaxValue;
                List<short> pcm = new List<short>(maxSeconds > 0 ? Math.Min(maxSamples, sampleRate * 2 * 60) : sampleRate * 2 * 180);
                byte[] byteScratch = null;

                while (pcm.Count < maxSamples)
                {
                    int actualIdx, flags;
                    long ts;
                    IMFSample sample = null;
                    hr = reader.ReadSample(MF_SOURCE_READER_FIRST_AUDIO_STREAM, 0, out actualIdx, out flags, out ts, out sample);
                    if (hr < 0) break;
                    if (sample != null)
                    {
                        IMFMediaBuffer buf = null;
                        try
                        {
                            hr = sample.ConvertToContiguousBuffer(out buf);
                            if (hr >= 0 && buf != null)
                            {
                                IntPtr ptr;
                                int maxLen, curLen;
                                hr = buf.Lock(out ptr, out maxLen, out curLen);
                                if (hr >= 0 && curLen > 1)
                                {
                                    try
                                    {
                                        if (byteScratch == null || byteScratch.Length < curLen)
                                            byteScratch = new byte[curLen];
                                        Marshal.Copy(ptr, byteScratch, 0, curLen);
                                        int count = Math.Min(curLen / 2, maxSamples - pcm.Count);
                                        for (int i = 0; i < count; i++)
                                        {
                                            short s = (short)(byteScratch[i * 2] | (byteScratch[i * 2 + 1] << 8));
                                            pcm.Add(s);
                                        }
                                    }
                                    finally
                                    {
                                        buf.Unlock();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (buf != null) Marshal.ReleaseComObject(buf);
                            Marshal.ReleaseComObject(sample);
                        }
                    }
                    if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                }

                return pcm.Count > 0 ? pcm.ToArray() : TryReadWavFallback(filePath, maxSeconds);
            }
            catch
            {
                return TryReadWavFallback(filePath, maxSeconds);
            }
            finally
            {
                if (mediaType != null) { try { Marshal.ReleaseComObject(mediaType); } catch { } }
                if (reader != null) { try { Marshal.ReleaseComObject(reader); } catch { } }
                if (mfStarted) { try { MFShutdown(); } catch { } }
            }
        }

        private static short[] TryReadWavFallback(string filePath, int maxSeconds)
        {
            try
            {
                if (!Path.GetExtension(filePath).Equals(".wav", StringComparison.OrdinalIgnoreCase)) return null;
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 44) return null;
                if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return null;
                int channels = BitConverter.ToInt16(bytes, 22);
                int sr = BitConverter.ToInt32(bytes, 24);
                int bits = BitConverter.ToInt16(bytes, 34);
                if (bits != 16 || sr <= 0 || channels <= 0) return null;

                int pos = 12;
                while (pos + 8 < bytes.Length)
                {
                    string chunkId = Encoding.ASCII.GetString(bytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                    pos += 8;
                    if (chunkId == "data")
                    {
                        int end = Math.Min(bytes.Length, pos + chunkSize);
                        int maxBytes = maxSeconds > 0 ? Math.Min(end - pos, maxSeconds * sr * channels * 2) : (end - pos);
                        int sampleCount = maxBytes / 2;
                        short[] res = new short[channels == 1 ? sampleCount * 2 : sampleCount];
                        if (channels == 1)
                        {
                            for (int i = 0; i < sampleCount; i++)
                            {
                                short s = BitConverter.ToInt16(bytes, pos + i * 2);
                                res[i * 2] = s;
                                res[i * 2 + 1] = s;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < sampleCount; i++)
                            {
                                res[i] = BitConverter.ToInt16(bytes, pos + i * 2);
                            }
                        }
                        return res;
                    }
                    pos += chunkSize + (chunkSize & 1);
                }
            }
            catch { }
            return null;
        }
    }

    public class AudioAnalysis
    {
        public bool IsReady { get; set; }
        public double Bpm { get; set; }
        public double BpmConfidence { get; set; }
        public int DurationMs { get; set; }
        public int IntroCueMs { get; set; }
        public int DropCueMs { get; set; }
        public int OutroCueMs { get; set; }
        public float[] WaveformPeaks { get; set; }

        public AudioAnalysis()
        {
            WaveformPeaks = new float[140];
            Bpm = 120.0;
        }
    }

    public static class MashupAnalyzer
    {
        public static AudioAnalysis AnalyzeFile(string filePath, int fallbackDurationMs = 180000)
        {
            AudioAnalysis analysis = new AudioAnalysis();
            analysis.DurationMs = fallbackDurationMs;
            try
            {
                const int sr = 44100;
                short[] pcm = MediaFoundationDecoder.DecodeStereoPcm16(filePath, 0, sr);
                if (pcm == null || pcm.Length < sr * 2)
                {
                    analysis.DropCueMs = fallbackDurationMs / 3;
                    analysis.OutroCueMs = Math.Max(0, fallbackDurationMs - 14000);
                    analysis.IsReady = false;
                    return analysis;
                }

                int totalFrames = pcm.Length / 2;
                analysis.DurationMs = (int)((totalFrames * 1000L) / sr);

                // 1. Build 140-bin waveform peak envelope
                int bins = 140;
                float[] peaks = new float[bins];
                int framesPerBin = Math.Max(1, totalFrames / bins);
                float maxPeak = 0.001f;
                for (int b = 0; b < bins; b++)
                {
                    int start = b * framesPerBin;
                    int end = Math.Min(totalFrames, start + framesPerBin);
                    int step = Math.Max(1, (end - start) / 256);
                    float sumSq = 0f;
                    int cnt = 0;
                    for (int f = start; f < end; f += step)
                    {
                        float mono = ((pcm[f * 2] + pcm[f * 2 + 1]) * 0.5f) / 32768f;
                        sumSq += mono * mono;
                        cnt++;
                    }
                    float rms = cnt > 0 ? (float)Math.Sqrt(sumSq / cnt) : 0f;
                    peaks[b] = rms;
                    if (rms > maxPeak) maxPeak = rms;
                }
                for (int b = 0; b < bins; b++)
                {
                    peaks[b] = Math.Max(0.06f, Math.Min(1f, peaks[b] / maxPeak));
                }
                analysis.WaveformPeaks = peaks;

                // 2. Cue Points: Intro onset (>25% peak), Drop Peak (highest 8-bin energy window), Outro start
                int introBin = 0;
                for (int b = 0; b < bins / 2; b++)
                {
                    if (peaks[b] > 0.28f) { introBin = b; break; }
                }
                analysis.IntroCueMs = (int)(((long)introBin * analysis.DurationMs) / bins);

                int bestDropBin = bins / 3;
                float bestWindow = -1f;
                int winStart = Math.Max(4, bins / 8);
                int winEnd = Math.Min(bins - 10, (bins * 3) / 4);
                for (int b = winStart; b < winEnd; b++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 8; k++) sum += peaks[b + k];
                    if (sum > bestWindow)
                    {
                        bestWindow = sum;
                        bestDropBin = b;
                    }
                }
                analysis.DropCueMs = (int)(((long)bestDropBin * analysis.DurationMs) / bins);
                analysis.OutroCueMs = Math.Max(analysis.DropCueMs + 5000, analysis.DurationMs - 14000);

                // 3. Onset-Energy Autocorrelation BPM Detector (72 - 164 BPM)
                int hopFrames = 441; // 100 Hz onset envelope (10ms resolution)
                int startFrame = Math.Min(totalFrames - 1, Math.Max(0, (int)((analysis.IntroCueMs / 1000.0) * sr)));
                int analyzeFrames = Math.Min(totalFrames - startFrame, sr * 55); // up to 55s of active audio
                int envLen = analyzeFrames / hopFrames;

                if (envLen > 400)
                {
                    double[] env = new double[envLen];
                    double prevE = 0;
                    for (int i = 0; i < envLen; i++)
                    {
                        int baseF = startFrame + i * hopFrames;
                        double eSum = 0;
                        for (int k = 0; k < hopFrames; k += 2)
                        {
                            int idx = (baseF + k) * 2;
                            double m = (pcm[idx] + pcm[idx + 1]) * 0.5 / 32768.0;
                            eSum += m * m;
                        }
                        double energy = Math.Sqrt(eSum / (hopFrames / 2));
                        double flux = Math.Max(0.0, energy - prevE);
                        env[i] = flux;
                        prevE = energy * 0.85 + prevE * 0.15;
                    }

                    // Normalize mean
                    double mean = 0;
                    for (int i = 0; i < envLen; i++) mean += env[i];
                    mean /= envLen;
                    for (int i = 0; i < envLen; i++) env[i] -= mean;

                    double bestBpm = 120.0;
                    double bestScore = -1.0;
                    double secondScore = 0.0;
                    double envRate = (double)sr / hopFrames; // ~100 Hz

                    for (int bpm = 72; bpm <= 164; bpm++)
                    {
                        double lag = (60.0 / bpm) * envRate;
                        int lagInt = (int)Math.Round(lag);
                        double ac = 0;
                        int n = envLen - lagInt * 2;
                        for (int i = 0; i < n; i++)
                        {
                            ac += env[i] * env[i + lagInt] + 0.5 * env[i] * env[i + lagInt * 2];
                        }
                        // Slight perceptual weighting toward 90-140 BPM dance/pop range
                        double weight = 1.0 - Math.Abs(bpm - 118.0) * 0.0018;
                        double score = ac * weight;
                        if (score > bestScore)
                        {
                            secondScore = bestScore;
                            bestScore = score;
                            bestBpm = bpm;
                        }
                    }

                    // Refine fractional BPM around bestBpm (-0.8 to +0.8 in 0.2 steps)
                    double refinedBpm = bestBpm;
                    double refinedBest = bestScore;
                    for (double cand = bestBpm - 0.8; cand <= bestBpm + 0.81; cand += 0.2)
                    {
                        int lagInt = (int)Math.Round((60.0 / cand) * envRate);
                        double ac = 0;
                        int n = envLen - lagInt;
                        for (int i = 0; i < n; i++) ac += env[i] * env[i + lagInt];
                        if (ac > refinedBest) { refinedBest = ac; refinedBpm = cand; }
                    }

                    analysis.Bpm = Math.Round(refinedBpm, 1);
                    analysis.BpmConfidence = secondScore > 0 ? Math.Min(1.0, Math.Max(0.2, (bestScore - secondScore) / Math.Abs(bestScore + 1e-6) * 4.0 + 0.5)) : 0.7;
                }

                analysis.IsReady = true;
            }
            catch
            {
                analysis.IsReady = false;
            }
            return analysis;
        }
    }

    public enum CrossfadeCurve
    {
        EqualPower = 0,
        SmoothSCurve = 1,
        HardCut = 2
    }

    public static class MashupDsp
    {
        /// <summary>
        /// Computes gainA and gainB in [0..1] for crossfade position p in [0..1] (0 = full A, 1 = full B).
        /// </summary>
        public static void ComputeCrossfadeGains(double p, CrossfadeCurve curve, out double gainA, out double gainB)
        {
            p = Math.Max(0.0, Math.Min(1.0, p));
            switch (curve)
            {
                case CrossfadeCurve.EqualPower:
                    gainA = Math.Cos(p * Math.PI * 0.5);
                    gainB = Math.Sin(p * Math.PI * 0.5);
                    break;
                case CrossfadeCurve.SmoothSCurve:
                    double s = p * p * (3.0 - 2.0 * p);
                    gainA = Math.Cos(s * Math.PI * 0.5);
                    gainB = Math.Sin(s * Math.PI * 0.5);
                    break;
                case CrossfadeCurve.HardCut:
                    gainA = p < 0.85 ? 1.0 : Math.Max(0.0, (1.0 - p) / 0.15);
                    gainB = p > 0.15 ? 1.0 : Math.Max(0.0, p / 0.15);
                    break;
                default:
                    gainA = 1.0 - p;
                    gainB = p;
                    break;
            }
        }

        /// <summary>
        /// Computes beat-matched SpeedRatio for Deck B to lock onto Deck A's BPM (handles 0.5x / 2x octave ratios).
        /// </summary>
        public static double ComputeBeatSyncRatio(double targetBpm, double sourceBpm)
        {
            if (targetBpm <= 20 || sourceBpm <= 20) return 1.0;
            double ratio = targetBpm / sourceBpm;
            if (ratio < 0.68) ratio *= 2.0;
            else if (ratio > 1.48) ratio *= 0.5;
            return Math.Max(0.75, Math.Min(1.35, Math.Round(ratio, 3)));
        }

        /// <summary>
        /// Renders a studio stereo 44.1kHz 16-bit PCM .wav mashup combining Deck A and Deck B with
        /// tempo resampling, equal-power crossfade automation, bass-ducking headroom, and soft-clip limiter.
        /// </summary>
        public static bool ExportStudioMashupWav(
            string pathA, string pathB,
            double speedA, double speedB,
            int startMsA, int startMsB,
            int blendSec, CrossfadeCurve curve,
            double trimA, double trimB,
            bool enableDucking,
            string outWavPath,
            out string statusMsg)
        {
            statusMsg = "";
            try
            {
                const int sr = 44100;
                short[] pcmA = MediaFoundationDecoder.DecodeStereoPcm16(pathA, 0, sr);
                short[] pcmB = MediaFoundationDecoder.DecodeStereoPcm16(pathB, 0, sr);
                if (pcmA == null || pcmA.Length < sr * 2)
                {
                    statusMsg = "Failed to decode Deck A audio.";
                    return false;
                }
                if (pcmB == null || pcmB.Length < sr * 2)
                {
                    statusMsg = "Failed to decode Deck B audio.";
                    return false;
                }

                int framesA = pcmA.Length / 2;
                int framesB = pcmB.Length / 2;
                double sA = Math.Max(0.6, Math.Min(1.5, speedA));
                double sB = Math.Max(0.6, Math.Min(1.5, speedB));

                int offsetFrameA = Math.Max(0, Math.Min(framesA - sr, (int)((startMsA / 1000.0) * sr)));
                int offsetFrameB = Math.Max(0, Math.Min(framesB - sr, (int)((startMsB / 1000.0) * sr)));

                int remOutFramesA = (int)((framesA - offsetFrameA) / sA);
                int remOutFramesB = (int)((framesB - offsetFrameB) / sB);

                // Studio Mashup Structure:
                // - Lead-in on Deck A (up to 16s or half of A)
                // - Dual-Layer Mashup Blend window (blendSec)
                // - Remainder / Drop on Deck B (up to 60s, total capped safely at 180s)
                int blendFrames = Math.Max(sr * 2, Math.Min(blendSec * sr, Math.Min(remOutFramesA, remOutFramesB)));
                int leadInFrames = Math.Min(sr * 16, Math.Max(sr * 2, remOutFramesA - blendFrames));
                int tailFramesB = Math.Max(0, remOutFramesB - blendFrames);
                int totalFrames = Math.Min(sr * 180, leadInFrames + blendFrames + tailFramesB);

                int blendStart = leadInFrames;
                int blendEnd = leadInFrames + blendFrames;

                using (FileStream fs = new FileStream(outWavPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    int dataBytes = totalFrames * 4;
                    // RIFF WAV Header
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + dataBytes);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1); // PCM
                    bw.Write((short)2); // Stereo
                    bw.Write(sr);
                    bw.Write(sr * 4);
                    bw.Write((short)4);
                    bw.Write((short)16);
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(dataBytes);

                    byte[] chunk = new byte[16384 * 4];
                    int chunkPos = 0;

                    for (int outF = 0; outF < totalFrames; outF++)
                    {
                        double p;
                        if (outF < blendStart) p = 0.0;
                        else if (outF >= blendEnd) p = 1.0;
                        else p = (double)(outF - blendStart) / Math.Max(1, blendFrames);

                        double gA, gB;
                        ComputeCrossfadeGains(p, curve, out gA, out gB);
                        gA *= trimA;
                        gB *= trimB;

                        double lA = 0, rA = 0;
                        double srcPosA = offsetFrameA + outF * sA;
                        int idxA = (int)srcPosA;
                        if (idxA + 1 < framesA && p < 0.999)
                        {
                            double fracA = srcPosA - idxA;
                            lA = (pcmA[idxA * 2] + (pcmA[(idxA + 1) * 2] - pcmA[idxA * 2]) * fracA) / 32768.0;
                            rA = (pcmA[idxA * 2 + 1] + (pcmA[(idxA + 1) * 2 + 1] - pcmA[idxA * 2 + 1]) * fracA) / 32768.0;
                        }

                        double lB = 0, rB = 0;
                        if (outF >= blendStart)
                        {
                            double srcPosB = offsetFrameB + (outF - blendStart) * sB;
                            int idxB = (int)srcPosB;
                            if (idxB + 1 < framesB)
                            {
                                double fracB = srcPosB - idxB;
                                lB = (pcmB[idxB * 2] + (pcmB[(idxB + 1) * 2] - pcmB[idxB * 2]) * fracB) / 32768.0;
                                rB = (pcmB[idxB * 2 + 1] + (pcmB[(idxB + 1) * 2 + 1] - pcmB[idxB * 2 + 1]) * fracB) / 32768.0;
                            }
                        }

                        // Smart dynamic ducking during overlapping blend
                        if (enableDucking && p > 0.05 && p < 0.95)
                        {
                            double energyA = Math.Min(1.0, (Math.Abs(lA) + Math.Abs(rA)) * 0.5);
                            gB *= (1.0 - energyA * 0.28);
                        }

                        double mixL = lA * gA + lB * gB;
                        double mixR = rA * gA + rB * gB;

                        // Smooth Mastering Soft-Clip Limiter (tanh curve)
                        mixL = Math.Tanh(mixL * 0.95) * 0.96;
                        mixR = Math.Tanh(mixR * 0.95) * 0.96;

                        short sL = (short)Math.Max(-32767, Math.Min(32767, (int)(mixL * 32767.0)));
                        short sR = (short)Math.Max(-32767, Math.Min(32767, (int)(mixR * 32767.0)));

                        chunk[chunkPos++] = (byte)(sL & 0xFF);
                        chunk[chunkPos++] = (byte)((sL >> 8) & 0xFF);
                        chunk[chunkPos++] = (byte)(sR & 0xFF);
                        chunk[chunkPos++] = (byte)((sR >> 8) & 0xFF);

                        if (chunkPos >= chunk.Length)
                        {
                            fs.Write(chunk, 0, chunkPos);
                            chunkPos = 0;
                        }
                    }

                    if (chunkPos > 0) fs.Write(chunk, 0, chunkPos);
                }

                statusMsg = string.Format("Exported {0} ({1:0.1} MB)", Path.GetFileName(outWavPath), new FileInfo(outWavPath).Length / (1024.0 * 1024.0));
                return true;
            }
            catch (Exception ex)
            {
                statusMsg = "Export error: " + ex.Message;
                return false;
            }
        }
    }

    // ========================================================================
    // 5c. Interactive Dual-Waveform Control for Mashup Decks
    // ========================================================================
    public class MashupWaveformView : Control
    {
        public float[] Peaks = null;
        public float Progress = 0f;
        public float DropCuePct = -1f;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
        public bool IsAnalyzing = false;

        public event Action<float> SeekRequested;

        public MashupWaveformView()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && this.Width > 0)
            {
                float pct = Math.Max(0f, Math.Min(1f, (float)e.X / this.Width));
                if (SeekRequested != null) SeekRequested(pct);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);
            if (this.Width <= 4 || this.Height <= 4) return;

            // Subtle glass waveform container
            GlassGfx.PaintGlassCard(g, this.ClientRectangle, 8,
                System.Drawing.Color.FromArgb(18, 26, 44),
                System.Drawing.Color.FromArgb(12, 18, 30),
                System.Drawing.Color.FromArgb(28, 255, 255, 255),
                System.Drawing.Color.FromArgb(65, 255, 255, 255),
                System.Drawing.Color.FromArgb(14, AccentColor));

            float padX = 6f;
            float drawW = this.Width - padX * 2f;
            float cy = this.Height / 2f;
            float maxHalfH = (this.Height / 2f) - 6f;

            // Center hairline
            using (System.Drawing.Pen cp = new System.Drawing.Pen(System.Drawing.Color.FromArgb(30, 255, 255, 255), 1f))
            {
                g.DrawLine(cp, padX, cy, this.Width - padX, cy);
            }

            if (IsAnalyzing)
            {
                using (Font f = new Font("Segoe UI", 7.4f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, "ANALYZING BPM & WAVEFORM...", f,
                        this.ClientRectangle, AccentColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                return;
            }

            if (Peaks == null || Peaks.Length == 0)
            {
                using (Font f = new Font("Segoe UI", 7.4f))
                {
                    TextRenderer.DrawText(g, "Load a track to view waveform & BPM", f,
                        this.ClientRectangle, System.Drawing.Color.FromArgb(145, 165, 195), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                return;
            }

            int count = Peaks.Length;
            float step = drawW / count;
            float barW = Math.Max(1.2f, step - 0.8f);
            float playX = padX + Math.Max(0f, Math.Min(1f, Progress)) * drawW;

            using (SolidBrush playedB = new SolidBrush(AccentColor))
            using (SolidBrush unplayedB = new SolidBrush(System.Drawing.Color.FromArgb(75, 255, 255, 255)))
            {
                for (int i = 0; i < count; i++)
                {
                    float bx = padX + i * step;
                    float bh = Math.Max(2f, Peaks[i] * maxHalfH);
                    bool isPlayed = (bx <= playX);
                    g.FillRectangle(isPlayed ? playedB : unplayedB, bx, cy - bh, barW, bh * 2f);
                }
            }

            // Drop Cue Marker
            if (DropCuePct > 0.02f && DropCuePct < 0.98f)
            {
                float dx = padX + DropCuePct * drawW;
                using (System.Drawing.Pen cuePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 251, 191, 36), 1.4f))
                {
                    g.DrawLine(cuePen, dx, 4f, dx, this.Height - 4f);
                }
            }

            // Playhead needle
            using (System.Drawing.Pen headPen = new System.Drawing.Pen(System.Drawing.Color.White, 1.8f))
            {
                g.DrawLine(headPen, playX, 3f, playX, this.Height - 3f);
            }
        }
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
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);
        public bool IsPlaying = false;
        public VisualizerMode Mode = VisualizerMode.SpectrumBars;
        private bool _isHovered = false;

        public event Action<VisualizerMode> ModeChanged;

        public VisualizerPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = System.Drawing.Color.Transparent;
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
            _phase += 0.15;
            for (int i = 0; i < _bars.Length; i++)
            {
                if (IsPlaying)
                {
                    float val = (float)((Math.Sin(_phase * 2.2 + i * 0.4) * 0.5 + 0.5) * 28 + (Math.Cos(_phase * 1.5 - i * 0.3) * 0.5 + 0.5) * 14 + 5);
                    _bars[i] = val;
                }
                else
                {
                    _bars[i] = (float)((Math.Sin(_phase * 0.9 + i * 0.3) * 0.5 + 0.5) * 7 + 4);
                }
            }
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            int w = this.Width;
            int h = this.Height;
            if (w <= 4 || h <= 4) return;

            // Subtle frosted crystal grid lines
            using (System.Drawing.Pen gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(14, 255, 255, 255), 1f))
            {
                g.DrawLine(gridPen, 0, h * 0.33f, w, h * 0.33f);
                g.DrawLine(gridPen, 0, h * 0.66f, w, h * 0.66f);
            }

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

            string modeName;
            switch (Mode)
            {
                case VisualizerMode.SpectrumBars: modeName = "SPECTRUM"; break;
                case VisualizerMode.StereoMirror: modeName = "MIRROR"; break;
                case VisualizerMode.Oscilloscope: modeName = "OSCILLO"; break;
                default: modeName = "DOT MATRIX"; break;
            }

            using (Font f = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            {
                int badgeW = 70;
                int badgeH = 16;
                RectangleF bRect = new RectangleF(w - badgeW - 2f, 2f, badgeW, badgeH);
                using (GraphicsPath bp = GlassGfx.CreateRoundRect(bRect, 8f))
                using (SolidBrush bgB = new SolidBrush(_isHovered ? System.Drawing.Color.FromArgb(200, 18, 28, 44) : System.Drawing.Color.FromArgb(110, 12, 18, 30)))
                using (System.Drawing.Pen pB = new System.Drawing.Pen(_isHovered ? AccentColor : System.Drawing.Color.FromArgb(75, 255, 255, 255), 1f))
                {
                    g.FillPath(bgB, bp);
                    g.DrawPath(pB, bp);
                }
                TextRenderer.DrawText(g, modeName, f, Rectangle.Round(bRect),
                    _isHovered ? AccentColor : System.Drawing.Color.FromArgb(185, 210, 235),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawSpectrumBars(Graphics g, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            float spacing = (float)w / _bars.Length;
            float barW = Math.Max(3f, spacing - 3.5f);

            using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, w, h), SecondaryColor, AccentColor, 90f))
            using (SolidBrush capBrush = new SolidBrush(System.Drawing.Color.FromArgb(215, 255, 255, 255)))
            {
                for (int i = 0; i < _bars.Length; i++)
                {
                    float bh = Math.Min(h - 3f, Math.Max(4f, _bars[i]));
                    float bx = i * spacing + (spacing - barW) / 2f;
                    float by = h - bh;
                    RectangleF barRect = new RectangleF(bx, by, barW, bh);
                    using (GraphicsPath bp = GlassGfx.CreateRoundRect(barRect, Math.Min(2.5f, barW / 2f)))
                    {
                        g.FillPath(brush, bp);
                    }
                    g.FillEllipse(capBrush, bx, Math.Max(0f, by - 3f), barW, 2.2f);
                }
            }
        }

        private void DrawStereoMirror(Graphics g, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            float cy = h / 2f;
            float spacing = (float)w / _bars.Length;
            float barW = Math.Max(3f, spacing - 3.5f);

            using (LinearGradientBrush brushUp = new LinearGradientBrush(new RectangleF(0, 0, w, cy), SecondaryColor, AccentColor, 90f))
            using (LinearGradientBrush brushDown = new LinearGradientBrush(new RectangleF(0, cy, w, cy), AccentColor, System.Drawing.Color.FromArgb(120, SecondaryColor), 90f))
            using (System.Drawing.Pen divPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(85, 255, 255, 255), 1f))
            {
                for (int i = 0; i < _bars.Length; i++)
                {
                    float halfH = Math.Min(cy - 2f, Math.Max(2.5f, _bars[i] * 0.48f));
                    float bx = i * spacing + (spacing - barW) / 2f;
                    using (GraphicsPath bpUp = GlassGfx.CreateRoundRect(new RectangleF(bx, cy - halfH, barW, halfH), Math.Min(2.2f, barW / 2f)))
                    using (GraphicsPath bpDn = GlassGfx.CreateRoundRect(new RectangleF(bx, cy, barW, halfH), Math.Min(2.2f, barW / 2f)))
                    {
                        g.FillPath(brushUp, bpUp);
                        g.FillPath(brushDown, bpDn);
                    }
                }
                g.DrawLine(divPen, 0, cy, w, cy);
            }
        }

        private void DrawOscilloscope(Graphics g, int w, int h)
        {
            int ptsCount = 56;
            PointF[] points = new PointF[ptsCount];
            float cy = h / 2f;
            float maxAmp = (h / 2f) - 5f;

            for (int i = 0; i < ptsCount; i++)
            {
                float x = (float)i / (ptsCount - 1) * w;
                double wave = Math.Sin(_phase * 2.5 + i * 0.38) * 0.65 + Math.Sin(_phase * 4.1 + i * 0.72) * 0.35;
                if (!IsPlaying) wave *= 0.22;
                float y = cy + (float)(wave * maxAmp);
                points[i] = new PointF(x, y);
            }

            using (System.Drawing.Pen glowPen1 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(42, AccentColor), 6f))
            using (System.Drawing.Pen glowPen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(135, AccentColor), 2.6f))
            using (System.Drawing.Pen corePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(235, 255, 255, 255), 1.3f))
            {
                glowPen1.LineJoin = LineJoin.Round;
                glowPen2.LineJoin = LineJoin.Round;
                corePen.LineJoin = LineJoin.Round;
                g.DrawCurve(glowPen1, points, 0.4f);
                g.DrawCurve(glowPen2, points, 0.4f);
                g.DrawCurve(corePen, points, 0.4f);
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
                float norm = Math.Min(1f, _bars[barIdx] / 42f);
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
                        using (SolidBrush halo = new SolidBrush(System.Drawing.Color.FromArgb(55, dotCol)))
                        using (SolidBrush litBrush = new SolidBrush(dotCol))
                        {
                            g.FillEllipse(halo, dx - dotRadius * 1.4f, dy - dotRadius * 1.4f, dotRadius * 2.8f, dotRadius * 2.8f);
                            g.FillEllipse(litBrush, dx - dotRadius, dy - dotRadius, dotRadius * 2f, dotRadius * 2f);
                        }
                    }
                    else
                    {
                        using (SolidBrush unlitBrush = new SolidBrush(System.Drawing.Color.FromArgb(24, 255, 255, 255)))
                        {
                            g.FillEllipse(unlitBrush, dx - dotRadius * 0.65f, dy - dotRadius * 0.65f, dotRadius * 1.3f, dotRadius * 1.3f);
                        }
                    }
                }
            }
        }
    }

    // ========================================================================
    // 6b. Crystal Vinyl Spinning Disc
    // ========================================================================
    public class VinylDiscControl : Panel
    {
        private float _angle = 0f;
        private float _angularVelocity = 0f;
        public System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(103, 232, 249);
        public System.Drawing.Color SecondaryColor = System.Drawing.Color.FromArgb(224, 242, 254);

        public event EventHandler DiscClicked;

        public VinylDiscControl(int size = 66)
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
            _angularVelocity = _angularVelocity * 0.88f + targetVel * 0.12f;
            _angle = (_angle + _angularVelocity) % 360f;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            int sz = Math.Min(this.Width, this.Height);
            float cx = sz / 2f;
            float cy = sz / 2f;
            float r = sz / 2f - 2.5f;

            // Outer translucent crystal glow ring
            using (System.Drawing.Pen outerHalo = new System.Drawing.Pen(System.Drawing.Color.FromArgb(55, AccentColor), 2f))
            {
                g.DrawEllipse(outerHalo, cx - r - 1f, cy - r - 1f, (r + 1f) * 2f, (r + 1f) * 2f);
            }

            // 1. Deep Sapphire Vinyl Body
            RectangleF discRect = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
            using (LinearGradientBrush vinylBg = new LinearGradientBrush(discRect,
                System.Drawing.Color.FromArgb(26, 34, 52),
                System.Drawing.Color.FromArgb(10, 14, 22), 135f))
            {
                g.FillEllipse(vinylBg, discRect);
            }

            // Crisp Glass Outer Rim
            using (System.Drawing.Pen rimPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 255, 255, 255), 1.1f))
            {
                g.DrawEllipse(rimPen, discRect);
            }

            // 2. Fine Concentric Micro Grooves
            float[] grooves = new float[] { 0.89f, 0.80f, 0.71f, 0.62f, 0.53f };
            using (System.Drawing.Pen groovePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(28, 255, 255, 255), 0.9f))
            {
                foreach (float gf in grooves)
                {
                    float gr = r * gf;
                    g.DrawEllipse(groovePen, cx - gr, cy - gr, gr * 2f, gr * 2f);
                }
            }

            // 3. Rotating Specular Glass Sheen
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_angle);

            using (GraphicsPath sheenPath = new GraphicsPath())
            {
                sheenPath.AddPie(-r, -r, r * 2f, r * 2f, -22, 44);
                sheenPath.AddPie(-r, -r, r * 2f, r * 2f, 158, 44);
                using (SolidBrush sheenBrush = new SolidBrush(System.Drawing.Color.FromArgb(30, 255, 255, 255)))
                {
                    g.FillPath(sheenBrush, sheenPath);
                }
            }

            // 4. Crystal Accent Center Label
            float labelR = r * 0.38f;
            RectangleF labelRect = new RectangleF(-labelR, -labelR, labelR * 2f, labelR * 2f);
            using (LinearGradientBrush labelBrush = new LinearGradientBrush(labelRect,
                GlassGfx.Blend(AccentColor, SecondaryColor, 0.35f), AccentColor, 90f))
            {
                g.FillEllipse(labelBrush, labelRect);
            }

            using (System.Drawing.Pen labelRing = new System.Drawing.Pen(System.Drawing.Color.FromArgb(185, 255, 255, 255), 1.1f))
            {
                g.DrawEllipse(labelRing, -labelR * 0.72f, -labelR * 0.72f, labelR * 1.44f, labelR * 1.44f);
            }

            // Stylus Marker Dot
            using (SolidBrush markerBrush = new SolidBrush(System.Drawing.Color.White))
            {
                g.FillEllipse(markerBrush, labelR * 0.45f, -2f, 4f, 4f);
            }

            // Center Spindle Hole
            using (SolidBrush spindleBrush = new SolidBrush(System.Drawing.Color.FromArgb(12, 16, 26)))
            using (System.Drawing.Pen spindleRim = new System.Drawing.Pen(System.Drawing.Color.FromArgb(140, 255, 255, 255), 0.9f))
            {
                g.FillEllipse(spindleBrush, -3.2f, -3.2f, 6.4f, 6.4f);
                g.DrawEllipse(spindleRim, -3.2f, -3.2f, 6.4f, 6.4f);
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
        public int GlassClarity = 1; // 0 = Crystal (86%), 1 = Frosted (93%), 2 = Solid (100%)
        public bool AutoNext = true;
        public bool EqActive = true;
        public int EqPresetIndex = 0;
        public bool BassBoostActive = false;
        public int[] EqBands = new int[] { 0, 0, 0, 0, 0 };
        public bool IsLooping = false;
        public bool IsIsland = false;
        public bool IsPinned = false;

        // Last played track state & queue persistence
        public string LastTitle = "";
        public string LastArtist = "";
        public string LastVideoId = "";
        public string LastUrl = "";
        public int LastDurationMs = 0;
        public string LastGenre = "";
        public string LastSource = "";
        public bool HasSavedQueue = false;
        public List<TrackItem> Queue = new List<TrackItem>();

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
                cfg.GlassClarity = ExtractInt(json, "GlassClarity", 1);
                cfg.AutoNext = ExtractBool(json, "AutoNext", true);
                cfg.EqActive = ExtractBool(json, "EqActive", true);
                cfg.EqPresetIndex = ExtractInt(json, "EqPresetIndex", 0);
                cfg.BassBoostActive = ExtractBool(json, "BassBoostActive", false);
                cfg.IsLooping = ExtractBool(json, "IsLooping", false);
                cfg.IsIsland = ExtractBool(json, "IsIsland", false);
                cfg.IsPinned = ExtractBool(json, "IsPinned", false);

                cfg.LastTitle = ExtractString(json, "LastTitle", "");
                cfg.LastArtist = ExtractString(json, "LastArtist", "");
                cfg.LastVideoId = ExtractString(json, "LastVideoId", "");
                cfg.LastUrl = ExtractString(json, "LastUrl", "");
                cfg.LastDurationMs = ExtractInt(json, "LastDurationMs", 0);
                cfg.LastGenre = ExtractString(json, "LastGenre", "");
                cfg.LastSource = ExtractString(json, "LastSource", "");

                Match qMatch = Regex.Match(json, "\"Queue\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
                if (qMatch.Success)
                {
                    cfg.HasSavedQueue = true;
                    string qBody = qMatch.Groups[1].Value;
                    foreach (Match itemM in Regex.Matches(qBody, "\\{(.*?)\\}", RegexOptions.Singleline))
                    {
                        string obj = itemM.Groups[1].Value;
                        string t = ExtractString(obj, "Title", "");
                        if (string.IsNullOrEmpty(t)) continue;
                        cfg.Queue.Add(new TrackItem
                        {
                            Title = t,
                            Artist = ExtractString(obj, "Artist", "Unknown Artist"),
                            VideoId = ExtractString(obj, "VideoId", ""),
                            Url = ExtractString(obj, "Url", ""),
                            DurationMs = ExtractInt(obj, "DurationMs", 180000),
                            Genre = ExtractString(obj, "Genre", "YT MUSIC • Official Audio"),
                            Source = ExtractString(obj, "Source", "YT_MUSIC")
                        });
                    }
                }
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
                sb.AppendLine(string.Format("  \"GlassClarity\": {0},", GlassClarity));
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
                sb.AppendLine(string.Format("  \"LastSource\": \"{0}\",", EscapeJson(LastSource)));
                sb.AppendLine("  \"Queue\": [");
                List<TrackItem> validQueue = new List<TrackItem>();
                if (Queue != null)
                {
                    foreach (TrackItem tr in Queue)
                    {
                        if (tr != null && !string.IsNullOrEmpty(tr.Title) && tr.Source != "NONE") validQueue.Add(tr);
                    }
                }
                for (int i = 0; i < validQueue.Count; i++)
                {
                    TrackItem tr = validQueue[i];
                    string comma = (i < validQueue.Count - 1) ? "," : "";
                    sb.AppendLine(string.Format(
                        "    {{ \"Title\": \"{0}\", \"Artist\": \"{1}\", \"VideoId\": \"{2}\", \"Url\": \"{3}\", \"DurationMs\": {4}, \"Genre\": \"{5}\", \"Source\": \"{6}\" }}{7}",
                        EscapeJson(tr.Title),
                        EscapeJson(tr.Artist ?? ""),
                        EscapeJson(tr.VideoId ?? ""),
                        EscapeJson(tr.Url ?? ""),
                        tr.DurationMs,
                        EscapeJson(tr.Genre ?? "YT MUSIC • Official Audio"),
                        EscapeJson(tr.Source ?? "YT_MUSIC"),
                        comma));
                }
                sb.AppendLine("  ]");
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
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
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
    // 7b. Widget Skins Palette System (6 Crystal Glass Pro Themes)
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
                    Name = "1. Crystal Clear",
                    Tagline = "Ice Diamond & Arctic Glass",
                    Accent = System.Drawing.Color.FromArgb(103, 232, 249),
                    SecondaryAccent = System.Drawing.Color.FromArgb(224, 242, 254),
                    Background = System.Drawing.Color.FromArgb(11, 16, 28),
                    Surface = System.Drawing.Color.FromArgb(22, 32, 52),
                    TextMuted = System.Drawing.Color.FromArgb(168, 198, 228)
                },
                new WidgetSkin
                {
                    Name = "2. Cyber Neon",
                    Tagline = "Electric Cyan & Obsidian Glass",
                    Accent = System.Drawing.Color.FromArgb(0, 240, 255),
                    SecondaryAccent = System.Drawing.Color.FromArgb(96, 165, 250),
                    Background = System.Drawing.Color.FromArgb(9, 12, 20),
                    Surface = System.Drawing.Color.FromArgb(18, 24, 40),
                    TextMuted = System.Drawing.Color.FromArgb(156, 178, 204)
                },
                new WidgetSkin
                {
                    Name = "3. Sunset Synthwave",
                    Tagline = "Neon Rose & Sunset Amber Glass",
                    Accent = System.Drawing.Color.FromArgb(255, 72, 153),
                    SecondaryAccent = System.Drawing.Color.FromArgb(253, 186, 116),
                    Background = System.Drawing.Color.FromArgb(20, 12, 30),
                    Surface = System.Drawing.Color.FromArgb(35, 21, 52),
                    TextMuted = System.Drawing.Color.FromArgb(216, 180, 254)
                },
                new WidgetSkin
                {
                    Name = "4. Matrix Emerald",
                    Tagline = "Aurora Mint & Deep Jade Glass",
                    Accent = System.Drawing.Color.FromArgb(52, 211, 153),
                    SecondaryAccent = System.Drawing.Color.FromArgb(167, 243, 208),
                    Background = System.Drawing.Color.FromArgb(8, 18, 14),
                    Surface = System.Drawing.Color.FromArgb(15, 32, 24),
                    TextMuted = System.Drawing.Color.FromArgb(145, 224, 190)
                },
                new WidgetSkin
                {
                    Name = "5. Tokyo Amber",
                    Tagline = "Warm Champagne Gold & Smoked Glass",
                    Accent = System.Drawing.Color.FromArgb(251, 191, 36),
                    SecondaryAccent = System.Drawing.Color.FromArgb(254, 240, 138),
                    Background = System.Drawing.Color.FromArgb(20, 16, 12),
                    Surface = System.Drawing.Color.FromArgb(34, 27, 20),
                    TextMuted = System.Drawing.Color.FromArgb(245, 208, 128)
                },
                new WidgetSkin
                {
                    Name = "6. Nordic Frost",
                    Tagline = "Glacier Blue & Lavender Quartz",
                    Accent = System.Drawing.Color.FromArgb(56, 189, 248),
                    SecondaryAccent = System.Drawing.Color.FromArgb(196, 181, 253),
                    Background = System.Drawing.Color.FromArgb(12, 18, 32),
                    Surface = System.Drawing.Color.FromArgb(23, 34, 58),
                    TextMuted = System.Drawing.Color.FromArgb(186, 230, 253)
                }
            };
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

        // Sizes (Refined proportions for Crystal Clear breathing room)
        private const int COLLAPSED_W = 480;
        private const int COLLAPSED_H = 48;
        private const int EXPANDED_W = 600;
        private const int EXPANDED_H = 540;

        // Graphic Equalizer & Audio Enhancements
        private double _tempoSpeed = 1.0;
        private bool _autoNext = true;
        private bool _eqActive = true;
        private int _eqPresetIndex = 0;
        private int[] _eqBands = new int[5] { 0, 0, 0, 0, 0 };
        private bool _bassBoostActive = false;
        private bool _isInternalEqUpdate = false;
        private int _glassClarity = 1; // 0 = Crystal (86%), 1 = Frosted (93%), 2 = Solid (100%)

        private static readonly List<EqPreset> _eqPresets = new List<EqPreset>
        {
            new EqPreset("Flat (Neutral)", 0, 0, 0, 0, 0),
            new EqPreset("Bass Boost (+8 dB)", 8, 5, 1, 0, 1, 8),
            new EqPreset("Vocal & Acoustic", -2, 1, 6, 4, 2, 0, 3),
            new EqPreset("Treble Booster", -1, 0, 2, 5, 8, 0, 7),
            new EqPreset("Rock & Metal", 5, 3, -1, 3, 6, 4, 4),
            new EqPreset("Electronic / EDM", 7, 5, 0, 4, 7, 6, 5),
            new EqPreset("Pop & R&B", 4, 2, 2, 3, 4, 3, 2),
            new EqPreset("Lo-Fi & Chillout", 4, 3, -1, -3, -5, 4),
            new EqPreset("Custom (Manual)", 0, 0, 0, 0, 0)
        };

        // UI Controls for Deck & Studio
        private GlassButton _btnQuickSkin;
        private GlassButton _btnQuickClarity;
        private GlassButton[] _clarityButtons = new GlassButton[3];
        private DarkSelect _cmbEqPreset;
        private GlassButton _btnEqBypass;
        private GlassButton _btnEqReset;
        private DarkSlider[] _eqSliders = new DarkSlider[5];
        private Label[] _lblEqValues = new Label[5];
        private Label[] _lblEqBands = new Label[5];
        private GlassButton _btnBassBoost;
        private GlassButton _btnAutoNext;
        private GlassButton _btnAutoStart;
        private DarkSlider _sliderTempo;
        private Label _lblTempoVal;
        private GlassButton _btnTempoReset;
        private GlassCard _cardTheme;
        private GlassCard _cardEq;
        private GlassCard _cardEnhance;
        private GlassCard _lyricsHeaderCard;
        private GlassCard _queueHeaderCard;
        private Label _lblSkinTitle;
        private Label _lblEqTitle;
        private Label _lblEnhanceTitle;
        private Label _lblTempoTitle;
        private Label _lblLyricsHeader;
        private Label _lblQueueHeader;
        private GlassButton _btnAddFile;
        private GlassButton _btnClearQueue;
        private GlassButton _btnSearchExec;
        private int _queueHoverRemoveIdx = -1;

        // Cached Fonts (Prevents GDI Handle Leaks inside OwnerDraw ListBoxes)
        private readonly Font _fontBadge = new Font("Segoe UI", 6.8f, FontStyle.Bold);
        private readonly Font _fontListTime = new Font("Consolas", 8.5f, FontStyle.Bold);
        private readonly Font _fontListRegular = new Font("Segoe UI", 9.2f);
        private readonly Font _fontListSub = new Font("Segoe UI", 8f);
        private readonly Font _fontListBold = new Font("Segoe UI", 9.8f, FontStyle.Bold);
        private readonly Font _fontListItalic = new Font("Segoe UI", 9.2f, FontStyle.Italic);

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
        private float _headerProgressPct = 0f;

        // Dragging
        private bool _isMouseDown = false;
        private System.Drawing.Point _mouseDownPos = System.Drawing.Point.Empty;

        // Engines
        private NativeAudioPlayer _player = new NativeAudioPlayer();
        private bool _wasPlayingLastTick = false;
        private System.Windows.Forms.Timer _animTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer _tickTimer = new System.Windows.Forms.Timer();
        private NotifyIcon _trayIcon;
        private ToolTip _toolTip = new ToolTip();
        private bool _isApplyingSkin = false;

        // Colors
        private System.Drawing.Color _accent = System.Drawing.Color.FromArgb(103, 232, 249);
        private System.Drawing.Color _secondaryAccent = System.Drawing.Color.FromArgb(224, 242, 254);
        private System.Drawing.Color _bg = System.Drawing.Color.FromArgb(11, 16, 28);
        private System.Drawing.Color _surface = System.Drawing.Color.FromArgb(22, 32, 52);
        private System.Drawing.Color _textMuted = System.Drawing.Color.FromArgb(168, 198, 228);

        // Tracks & Data
        private TrackItem _currentTrack = CreateEmptyTrack();

        private static TrackItem CreateEmptyTrack()
        {
            return new TrackItem
            {
                Title = "No Track Loaded",
                Artist = "Search or drop audio",
                Genre = "READY",
                Source = "NONE",
                DurationMs = 0,
                VideoId = "",
                Url = ""
            };
        }

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
        private CrystalSegmentedBar _segmentedBar;
        private Panel[] _tabPanels = new Panel[6];

        // Tab 4: Pro Mashup Studio
        private NativeAudioPlayer _mashDeckA = new NativeAudioPlayer();
        private NativeAudioPlayer _mashDeckB = new NativeAudioPlayer();
        private TrackItem _mashTrackA;
        private TrackItem _mashTrackB;
        private AudioAnalysis _mashAnalA = new AudioAnalysis();
        private AudioAnalysis _mashAnalB = new AudioAnalysis();
        private int _mashLoadIdA = 0;
        private int _mashLoadIdB = 0;
        private double _mashTrimA = 1.0;
        private double _mashTrimB = 1.0;
        private double _mashSpeedA = 1.0;
        private double _mashSpeedB = 1.0;
        private double _mashPanA = 0.0;
        private double _mashPanB = 0.0;
        private int _mashCrossfade = 50; // 0 = 100% A, 50 = 50/50 Blend, 100 = 100% B
        private CrossfadeCurve _mashCurve = CrossfadeCurve.EqualPower;
        private int _mashBlendSec = 8;
        private bool _mashDucking = true;
        private bool _mashAutoBlending = false;
        private DateTime _mashBlendStartUtc = DateTime.MinValue;
        private int _mashBlendStartVal = 0;
        private int _mashBlendTargetVal = 100;
        private bool _mashInternalUpdate = false;

        private GlassCard _mashTopCard;
        private GlassCard _mashCardA;
        private GlassCard _mashCardB;
        private GlassCard _mashMixerCard;
        private GlassButton _btnMashSync;
        private GlassButton _btnMashCurve;
        private GlassButton _btnMashDuck;
        private GlassButton _btnMashExport;
        private Label _lblMashStatus;
        private DarkSelect _cmbMashA;
        private DarkSelect _cmbMashB;
        private GlassButton _btnMashFileA;
        private GlassButton _btnMashFileB;
        private Label _lblMashBpmA;
        private Label _lblMashBpmB;
        private MashupWaveformView _mashWaveA;
        private MashupWaveformView _mashWaveB;
        private GlassButton _btnMashPlayA;
        private GlassButton _btnMashPlayB;
        private GlassButton _btnMashCueA;
        private GlassButton _btnMashCueB;
        private GlassButton _btnMashPanA;
        private GlassButton _btnMashPanB;
        private DarkSlider _sliderMashTrimA;
        private DarkSlider _sliderMashTrimB;
        private Label _lblMashTimeA;
        private Label _lblMashTimeB;
        private DarkSlider _sliderMashCrossfader;
        private Label _lblMashMixRatio;
        private GlassButton _btnMashDur;
        private GlassButton _btnMashBlendAB;
        private GlassButton _btnMashBlendBA;
        private GlassButton _btnMashStopAll;

        // Tab 0: Deck
        private GlassCard _deckCard;
        private Label _lblTrackTitle;
        private Label _lblTrackArtist;
        private Label _lblSourceBadge;
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
        private Label _lblVolPct;
        private GlassButton _btnLoop;
        private GlassCard _deckInfoCard;
        private Label _lblDeckEngine;
        private Label _lblDeckQuality;
        private Label _lblDeckDetails;
        private Label _lblDeckLyricCur;
        private Label _lblDeckLyricNext;
        private GlassButton _btnOpenStudio;

        // Tab 1: YouTube Music Search
        private CrystalSearchBox _searchBox;
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
        private int _auraAlpha = 55;
        private int _lastAuraAlpha = -1;
        private int _auraDirection = 1;

        public DexplayForm()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.AllowDrop = true;
            this.BackColor = _bg;
            this.Opacity = 0.93;

            InitTray();
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
                        _lblTrackArtist.Text = _currentTrack.Artist + " (Audio stream unavailable)";
                    }
                });
            };

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - COLLAPSED_W) / 2;
            int y = _isIsland ? 36 : 0;
            this.Location = new System.Drawing.Point(x, y);
            this.Size = new System.Drawing.Size(COLLAPSED_W, COLLAPSED_H);
            ApplyRoundedRegion();

            _animTimer.Interval = 15;
            _animTimer.Tick += AnimTimer_Tick;

            _tickTimer.Interval = 25;
            _tickTimer.Tick += TickTimer_Tick;
            _tickTimer.Start();

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

        private MenuItem _trayAutoStartItem;

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    object val = key.GetValue("Dexplay");
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enabled)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enabled)
                        key.SetValue("Dexplay", "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue("Dexplay", false);
                }
            }
            catch { }
        }

        public void ToggleAutoStart()
        {
            bool next = !IsAutoStartEnabled();
            SetAutoStart(next);
            UpdateAutoStartUI();
        }

        private void UpdateAutoStartUI()
        {
            bool enabled = IsAutoStartEnabled();
            if (_btnAutoStart != null)
            {
                _btnAutoStart.Text = enabled ? "AUTOSTART: ON" : "AUTOSTART: OFF";
                _btnAutoStart.IsActive = enabled;
                _btnAutoStart.Invalidate();
            }
            if (_trayAutoStartItem != null)
            {
                _trayAutoStartItem.Checked = enabled;
            }
        }

        private void InitTray()
        {
            Icon appIcon = SystemIcons.Application;
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    appIcon = extracted;
                    this.Icon = extracted;
                }
            }
            catch { }

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Dexplay — Crystal Clear Desktop Music Island";
            _trayIcon.Icon = appIcon;
            _trayIcon.Visible = true;

            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Expand / Collapse Widget", (s, e) => { ShowFromTray(); ToggleExpand(); });
            menu.MenuItems.Add("Search YouTube Music", (s, e) => { ShowFromTray(); OpenSearchTab(); });
            menu.MenuItems.Add("Toggle Island / Dock Mode", (s, e) => { ShowFromTray(); ToggleIsland(); });
            menu.MenuItems.Add("Play / Pause", (s, e) => TogglePlay());
            menu.MenuItems.Add("Hide to Tray", (s, e) => { this.Hide(); });
            menu.MenuItems.Add("-");
            _trayAutoStartItem = new MenuItem("Auto-Start with Windows", (s, e) => ToggleAutoStart());
            _trayAutoStartItem.Checked = IsAutoStartEnabled();
            menu.MenuItems.Add(_trayAutoStartItem);
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Exit Dexplay", (s, e) => this.Close());

            _trayIcon.ContextMenu = menu;
            _trayIcon.DoubleClick += (s, e) => { ShowFromTray(); ToggleExpand(); };
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
        // 9. Crystal Clear Glassmorphic UI Construction
        // ====================================================================
        private void BuildUI()
        {
            // HEADER BAR (Dynamic Island / Notch)
            _headerPanel = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(COLLAPSED_W, COLLAPSED_H),
                BackColor = System.Drawing.Color.Transparent,
                Dock = DockStyle.Top,
                AllowDrop = true
            };
            _headerPanel.Paint += HeaderPanel_Paint;
            _headerPanel.MouseDown += Header_MouseDown;
            _headerPanel.MouseMove += Header_MouseMove;
            _headerPanel.MouseUp += Header_MouseUp;
            _headerPanel.DragEnter += (s, e) => OnDragEnter(e);
            _headerPanel.DragDrop += (s, e) => OnDragDrop(e);

            _btnHeaderPlay = new VectorButton(VectorIconType.Play, 10, 8, 32, 32)
            {
                IsCircleButton = true,
                CircleBgColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _btnHeaderPlay.Click += (s, e) => TogglePlay();

            _lblHeaderTitle = new Label
            {
                Location = new System.Drawing.Point(50, 11),
                Size = new System.Drawing.Size(218, 24),
                Text = _currentTrack.Artist + " — " + _currentTrack.Title,
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoEllipsis = true,
                UseCompatibleTextRendering = false,
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblHeaderTitle.Click += (s, e) => ToggleExpand();

            _miniEq = new MiniEqualizer
            {
                Location = new System.Drawing.Point(276, 13),
                Size = new System.Drawing.Size(24, 22),
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };

            _btnHeaderSearch = new VectorButton(VectorIconType.Search, 306, 9, 28, 28)
            {
                NormalColor = _accent,
                HoverColor = System.Drawing.Color.White
            };
            _toolTip.SetToolTip(_btnHeaderSearch, "Search Official Audio (YouTube Music)");
            _btnHeaderSearch.Click += (s, e) => OpenSearchTab();

            _btnHeaderExpand = new VectorButton(VectorIconType.ChevronDown, 336, 9, 28, 28)
            {
                NormalColor = _accent,
                HoverColor = System.Drawing.Color.White
            };
            _toolTip.SetToolTip(_btnHeaderExpand, "Expand / Collapse Deck");
            _btnHeaderExpand.Click += (s, e) => ToggleExpand();

            _btnHeaderIsland = new VectorButton(VectorIconType.Island, 366, 9, 28, 28);
            _toolTip.SetToolTip(_btnHeaderIsland, "Toggle Floating Island / Top Dock");
            _btnHeaderIsland.Click += (s, e) => ToggleIsland();

            _btnHeaderPin = new VectorButton(VectorIconType.Pin, 396, 9, 28, 28);
            _toolTip.SetToolTip(_btnHeaderPin, "Pin Always On Top");
            _btnHeaderPin.Click += (s, e) =>
            {
                _isPinned = !_isPinned;
                this.TopMost = _isPinned;
                _btnHeaderPin.IsActive = _isPinned;
                _btnHeaderPin.Invalidate();
                SaveCurrentConfig();
            };

            _btnHeaderClose = new VectorButton(VectorIconType.Close, 426, 9, 28, 28);
            _toolTip.SetToolTip(_btnHeaderClose, "Close Dexplay");
            _btnHeaderClose.Click += (s, e) => { this.Close(); };

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

            // EXPANDED DECK CONTAINER
            _deckPanel = new Panel
            {
                Location = new System.Drawing.Point(0, COLLAPSED_H),
                Size = new System.Drawing.Size(EXPANDED_W, EXPANDED_H - COLLAPSED_H),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false,
                AllowDrop = true
            };
            _deckPanel.DragEnter += (s, e) => OnDragEnter(e);
            _deckPanel.DragDrop += (s, e) => OnDragDrop(e);

            // Crystal Segmented Pill Navigation
            _segmentedBar = new CrystalSegmentedBar
            {
                Location = new System.Drawing.Point(16, 4),
                Size = new System.Drawing.Size(EXPANDED_W - 32, 34),
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _segmentedBar.TabSelected += (idx) => SwitchTab(idx);
            _deckPanel.Controls.Add(_segmentedBar);

            BuildTabDeck();
            BuildTabSearch();
            BuildTabLyrics();
            BuildTabQueue();
            BuildTabMashup();
            BuildTabEqualizer();

            this.Controls.Add(_deckPanel);
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);
            int w = _headerPanel.Width;
            int h = _headerPanel.Height;

            // Subtle crystal separator hairline at bottom of header when expanded
            if (_isExpanded)
            {
                using (System.Drawing.Pen sep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(26, 255, 255, 255), 1f))
                {
                    g.DrawLine(sep, 16, h - 1, w - 16, h - 1);
                }
            }

            // Ultra-thin luminous progress hairline along bottom edge of header
            if (_headerProgressPct > 0.002f)
            {
                int barLeft = _isIsland ? 24 : 18;
                int barMaxW = Math.Max(10, w - barLeft * 2);
                float fillW = Math.Max(4f, barMaxW * Math.Min(1f, _headerProgressPct));
                RectangleF pRect = new RectangleF(barLeft, h - 2.5f, fillW, 2f);
                using (GraphicsPath pp = GlassGfx.CreateRoundRect(pRect, 1f))
                using (LinearGradientBrush pb = new LinearGradientBrush(new RectangleF(barLeft, h - 3f, barMaxW, 3f), _accent, _secondaryAccent, 0f))
                {
                    g.FillPath(pb, pp);
                }
            }
        }

        private void LayoutHeaderControls(int width)
        {
            if (_headerPanel == null || _btnHeaderClose == null) return;
            _headerPanel.Width = width;

            _btnHeaderClose.Location = new System.Drawing.Point(width - 38, 10);
            _btnHeaderPin.Location = new System.Drawing.Point(width - 68, 10);
            _btnHeaderIsland.Location = new System.Drawing.Point(width - 98, 10);
            _btnHeaderExpand.Location = new System.Drawing.Point(width - 128, 10);
            _btnHeaderSearch.Location = new System.Drawing.Point(width - 158, 10);
            _miniEq.Location = new System.Drawing.Point(width - 188, 13);

            int titleX = 50;
            int titleW = Math.Max(120, (width - 196) - titleX);
            _lblHeaderTitle.Location = new System.Drawing.Point(titleX, 9);
            _lblHeaderTitle.Size = new System.Drawing.Size(titleW, 30);
        }

        private void OpenSearchTab()
        {
            SwitchTab(1);
            if (!_isExpanded) ToggleExpand();
            if (_txtSearch != null) _txtSearch.Focus();
        }

        // --- TAB 0: CRYSTAL DECK VIEW ---
        private void BuildTabDeck()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = true
            };

            // 1. HERO NOW-PLAYING GLASS CARD
            _deckCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(contentW, 154),
                CornerRadius = 14
            };

            _vinylDisc = new VinylDiscControl(66)
            {
                Location = new System.Drawing.Point(14, 14),
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _vinylDisc.DiscClicked += (s, e) => TogglePlay();

            _lblTrackTitle = new Label
            {
                Location = new System.Drawing.Point(92, 10),
                Size = new System.Drawing.Size(contentW - 194, 34),
                Text = _currentTrack.Title,
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _lblTrackArtist = new Label
            {
                Location = new System.Drawing.Point(92, 44),
                Size = new System.Drawing.Size(contentW - 194, 24),
                Text = _currentTrack.Artist,
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _lblSourceBadge = new Label
            {
                Location = new System.Drawing.Point(92, 68),
                Size = new System.Drawing.Size(240, 18),
                Text = "YT MUSIC • OFFICIAL AUDIO",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            _btnLoop = new GlassButton
            {
                Text = "LOOP: OFF",
                IsPill = true,
                Location = new System.Drawing.Point(contentW - 96, 16),
                Size = new System.Drawing.Size(82, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.5f, FontStyle.Bold)
            };
            _btnLoop.Click += (s, e) =>
            {
                _isLooping = !_isLooping;
                _btnLoop.IsActive = _isLooping;
                _btnLoop.Text = _isLooping ? "LOOP: ON" : "LOOP: OFF";
                _btnLoop.Invalidate();
                SaveCurrentConfig();
            };

            _visualizer = new VisualizerPanel
            {
                Location = new System.Drawing.Point(14, 92),
                Size = new System.Drawing.Size(contentW - 28, 52),
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _visualizer.ModeChanged += (m) => SaveCurrentConfig();

            _deckCard.Controls.Add(_vinylDisc);
            _deckCard.Controls.Add(_lblTrackTitle);
            _deckCard.Controls.Add(_lblTrackArtist);
            _deckCard.Controls.Add(_lblSourceBadge);
            _deckCard.Controls.Add(_btnLoop);
            _deckCard.Controls.Add(_visualizer);
            p.Controls.Add(_deckCard);

            // 2. CRYSTAL SCRUB BAR & TIME READOUT
            _lblTimeCur = new Label
            {
                Location = new System.Drawing.Point(2, 162),
                Size = new System.Drawing.Size(48, 22),
                Text = "0:00",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false,
                UseMnemonic = false
            };

            _scrubBar = new DarkSlider
            {
                Location = new System.Drawing.Point(52, 162),
                Size = new System.Drawing.Size(contentW - 104, 22),
                Minimum = 0,
                Maximum = 233000,
                Value = 0,
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _scrubBar.UserSeek += (s, e) => _player.Seek(_scrubBar.Value);
            _scrubBar.ValueChanged += (s, e) =>
            {
                if (_scrubBar.IsDragging && _lblTimeCur != null)
                {
                    _lblTimeCur.Text = FormatTime(_scrubBar.Value);
                }
            };

            _lblTimeTot = new Label
            {
                Location = new System.Drawing.Point(contentW - 50, 162),
                Size = new System.Drawing.Size(48, 22),
                Text = "3:53",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                UseCompatibleTextRendering = false,
                UseMnemonic = false
            };

            p.Controls.Add(_lblTimeCur);
            p.Controls.Add(_scrubBar);
            p.Controls.Add(_lblTimeTot);

            // 3. BALANCED TRANSPORT & QUICK CONTROLS ROW
            int rowY = 198;
            _btnQuickSkin = new GlassButton
            {
                Text = "THEME 1",
                IsPill = true,
                Location = new System.Drawing.Point(0, rowY + 8),
                Size = new System.Drawing.Size(82, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnQuickSkin, "Cycle Crystal Themes (Ctrl+Alt+T)");
            _btnQuickSkin.Click += (s, e) => CycleSkin();

            _btnQuickClarity = new GlassButton
            {
                Text = "GLASS 93%",
                IsPill = true,
                Location = new System.Drawing.Point(88, rowY + 8),
                Size = new System.Drawing.Size(86, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnQuickClarity, "Cycle Crystal Glass Transparency");
            _btnQuickClarity.Click += (s, e) => CycleGlassClarity();

            int cx = contentW / 2;
            _btnDeckPlay = new VectorButton(VectorIconType.Play, cx - 23, rowY, 46, 46)
            {
                IsCircleButton = true,
                CircleBgColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _btnDeckPrev = new VectorButton(VectorIconType.Prev, cx - 64, rowY + 6, 34, 34);
            _btnDeckNext = new VectorButton(VectorIconType.Next, cx + 30, rowY + 6, 34, 34);

            _btnDeckPrev.Click += (s, e) => PrevTrack();
            _btnDeckPlay.Click += (s, e) => TogglePlay();
            _btnDeckNext.Click += (s, e) => NextTrack();

            _btnDeckVol = new VectorButton(VectorIconType.Volume, contentW - 178, rowY + 8, 28, 28);
            _btnDeckVol.NormalColor = _textMuted;
            _btnDeckVol.HoverColor = _accent;
            _btnDeckVol.ActiveColor = System.Drawing.Color.FromArgb(248, 113, 113);
            _btnDeckVol.Click += (s, e) => ToggleMute();

            _deckVolumeBar = new DarkSlider
            {
                Location = new System.Drawing.Point(contentW - 148, rowY + 11),
                Size = new System.Drawing.Size(112, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };

            _lblVolPct = new Label
            {
                Location = new System.Drawing.Point(contentW - 36, rowY + 11),
                Size = new System.Drawing.Size(36, 22),
                Text = "75%",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

            _deckVolumeBar.ValueChanged += (s, e) =>
            {
                _masterVolume = _deckVolumeBar.Value;
                if (_lblVolPct != null) _lblVolPct.Text = _masterVolume + "%";
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

            p.Controls.Add(_btnQuickSkin);
            p.Controls.Add(_btnQuickClarity);
            p.Controls.Add(_btnDeckPrev);
            p.Controls.Add(_btnDeckPlay);
            p.Controls.Add(_btnDeckNext);
            p.Controls.Add(_btnDeckVol);
            p.Controls.Add(_deckVolumeBar);
            p.Controls.Add(_lblVolPct);

            // 4. TELEMETRY & LIVE SYNCED LYRICS PEEK GLASS CARD
            _deckInfoCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 260),
                Size = new System.Drawing.Size(contentW, contentH - 264),
                CornerRadius = 14
            };
            _deckInfoCard.Paint += (s, e) =>
            {
                using (System.Drawing.Pen div = new System.Drawing.Pen(System.Drawing.Color.FromArgb(24, 255, 255, 255), 1f))
                {
                    e.Graphics.DrawLine(div, 14, 54, _deckInfoCard.Width - 14, 54);
                }
            };

            _lblDeckEngine = new Label
            {
                Location = new System.Drawing.Point(14, 12),
                Size = new System.Drawing.Size(260, 18),
                Text = "DEXPLAY AUDIO ENGINE • DSP ACTIVE",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _lblDeckQuality = new Label
            {
                Location = new System.Drawing.Point(14, 30),
                Size = new System.Drawing.Size(contentW - 140, 18),
                Text = "EQ: Flat • Bass: OFF • Tempo: 100%",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _btnOpenStudio = new GlassButton
            {
                Text = "EQ STUDIO",
                IsPill = true,
                Location = new System.Drawing.Point(contentW - 110, 14),
                Size = new System.Drawing.Size(96, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnOpenStudio.Click += (s, e) => SwitchTab(5);

            _lblDeckDetails = new Label
            {
                Location = new System.Drawing.Point(14, 60),
                Size = new System.Drawing.Size(contentW - 28, 18),
                Text = "LIVE SYNCED LYRICS",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold),
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblDeckDetails.Click += (s, e) => SwitchTab(2);

            _lblDeckLyricCur = new Label
            {
                Location = new System.Drawing.Point(14, 79),
                Size = new System.Drawing.Size(contentW - 28, 28),
                Text = "Press Play to stream & follow synced lyrics...",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblDeckLyricCur.Click += (s, e) => SwitchTab(2);

            _lblDeckLyricNext = new Label
            {
                Location = new System.Drawing.Point(14, 108),
                Size = new System.Drawing.Size(contentW - 28, 24),
                Text = "Tip: Drag & drop local MP3, FLAC, WAV, or M4A anywhere on Dexplay",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblDeckLyricNext.Click += (s, e) => SwitchTab(2);

            _deckInfoCard.Controls.Add(_lblDeckEngine);
            _deckInfoCard.Controls.Add(_lblDeckQuality);
            _deckInfoCard.Controls.Add(_btnOpenStudio);
            _deckInfoCard.Controls.Add(_lblDeckDetails);
            _deckInfoCard.Controls.Add(_lblDeckLyricCur);
            _deckInfoCard.Controls.Add(_lblDeckLyricNext);

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
            if (_lblVolPct != null) _lblVolPct.Text = _masterVolume + "%";
            if (_btnDeckVol != null) { _btnDeckVol.IsActive = _isMuted; _btnDeckVol.Invalidate(); }
            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        // --- TAB 1: YOUTUBE MUSIC SEARCH ---
        private void BuildTabSearch()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };

            _searchBox = new CrystalSearchBox
            {
                Location = new System.Drawing.Point(0, 2),
                Size = new System.Drawing.Size(contentW - 98, 36),
                Placeholder = "Search song title, artist, or official audio..."
            };
            _txtSearch = _searchBox.InnerTextBox;
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    ExecuteYouTubeMusicSearch(_txtSearch.Text);
                }
            };

            _btnSearchExec = new GlassButton
            {
                Text = "SEARCH",
                IsActive = true,
                CornerRadius = 11,
                Location = new System.Drawing.Point(contentW - 90, 2),
                Size = new System.Drawing.Size(90, 36),
                Font = new System.Drawing.Font("Segoe UI", 8.6f, FontStyle.Bold)
            };
            _btnSearchExec.Click += (s, e) => ExecuteYouTubeMusicSearch(_txtSearch.Text);

            _lblSearchStatus = new Label
            {
                Location = new System.Drawing.Point(2, 44),
                Size = new System.Drawing.Size(contentW - 4, 18),
                Text = "Type a song title or artist and press Enter to find official audio.",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f),
                UseMnemonic = false
            };

            _lstSearchResults = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 66),
                Size = new System.Drawing.Size(contentW, contentH - 70),
                ItemHeight = 54
            };
            _lstSearchResults.DoubleClick += (s, e) => PlaySelectedSearchResult();
            _lstSearchResults.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PlaySelectedSearchResult(); };
            _lstSearchResults.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstSearchResults.Items.Count) return;
                GlassGfx.ApplyCrisp(e.Graphics);
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                using (SolidBrush clrB = new SolidBrush(_bg))
                {
                    e.Graphics.FillRectangle(clrB, e.Bounds);
                }

                RectangleF cardR = new RectangleF(e.Bounds.X + 2f, e.Bounds.Y + 2f, e.Bounds.Width - 6f, e.Bounds.Height - 4f);
                using (GraphicsPath cp = GlassGfx.CreateRoundRect(cardR, 9f))
                {
                    System.Drawing.Color fillCol = isSelected
                        ? GlassGfx.Blend(_surface, _accent, 0.14f)
                        : GlassGfx.Blend(_bg, _surface, 0.55f);
                    using (SolidBrush bgB = new SolidBrush(fillCol))
                    {
                        e.Graphics.FillPath(bgB, cp);
                    }
                    using (System.Drawing.Pen borderP = new System.Drawing.Pen(isSelected ? _accent : System.Drawing.Color.FromArgb(28, 255, 255, 255), 1f))
                    {
                        e.Graphics.DrawPath(borderP, cp);
                    }
                }

                if (_searchTracks != null && e.Index < _searchTracks.Count)
                {
                    TrackItem item = _searchTracks[e.Index];

                    if (isSelected)
                    {
                        using (GraphicsPath barP = GlassGfx.CreateRoundRect(new RectangleF(cardR.X + 3f, cardR.Y + 8f, 3f, cardR.Height - 16f), 1.5f))
                        using (SolidBrush barB = new SolidBrush(_accent))
                        {
                            e.Graphics.FillPath(barB, barP);
                        }
                    }

                    int bx = (int)cardR.X + 12;
                    int by = (int)cardR.Y + ((int)cardR.Height - 18) / 2;
                    RectangleF badgeR = new RectangleF(bx, by, 54f, 18f);
                    using (GraphicsPath bp = GlassGfx.CreateRoundRect(badgeR, 6f))
                    using (SolidBrush bBg = new SolidBrush(System.Drawing.Color.FromArgb(32, _accent)))
                    using (System.Drawing.Pen bPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(95, _accent), 1f))
                    {
                        e.Graphics.FillPath(bBg, bp);
                        e.Graphics.DrawPath(bPen, bp);
                    }
                    TextRenderer.DrawText(e.Graphics, "OFFICIAL", _fontBadge, Rectangle.Round(badgeR), _accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                    int tx = bx + 64;
                    int tw = (int)cardR.Right - tx - 10;
                    Rectangle titleRect = new Rectangle(tx, (int)cardR.Y + 4, tw, 24);
                    Rectangle artistRect = new Rectangle(tx, (int)cardR.Y + 26, tw, 20);
                    TextRenderer.DrawText(e.Graphics, item.Title, _fontListRegular, titleRect,
                        isSelected ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(232, 240, 250),
                        TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, item.Artist, _fontListSub, artistRect,
                        _textMuted,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
                else
                {
                    string fallback = _lstSearchResults.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 28, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, fallback, _fontListItalic, textRect, _textMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            p.Controls.Add(_searchBox);
            p.Controls.Add(_btnSearchExec);
            p.Controls.Add(_lblSearchStatus);
            p.Controls.Add(_lstSearchResults);

            _tabPanels[1] = p;
            _deckPanel.Controls.Add(p);
        }

        // --- TAB 2: SYNCED LYRICS VIEW ---
        private void BuildTabLyrics()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };

            _lyricsHeaderCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(contentW, 34),
                CornerRadius = 10
            };

            _lblLyricsHeader = new Label
            {
                Location = new System.Drawing.Point(12, 0),
                Size = new System.Drawing.Size(contentW - 24, 34),
                Text = "LRCLIB SYNCED LYRICS • Click any line to jump to timestamp",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            _lyricsHeaderCard.Controls.Add(_lblLyricsHeader);

            _lstLyrics = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 40),
                Size = new System.Drawing.Size(contentW, contentH - 44),
                ItemHeight = 42
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
                GlassGfx.ApplyCrisp(e.Graphics);
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                bool isCurrent = (_lyrics != null && e.Index < _lyrics.Count && e.Index == _activeLyricIndex);

                using (SolidBrush clrB = new SolidBrush(_bg))
                {
                    e.Graphics.FillRectangle(clrB, e.Bounds);
                }

                RectangleF cardR = new RectangleF(e.Bounds.X + 2f, e.Bounds.Y + 2f, e.Bounds.Width - 6f, e.Bounds.Height - 4f);
                if (isCurrent || isSelected)
                {
                    using (GraphicsPath cp = GlassGfx.CreateRoundRect(cardR, 8f))
                    using (SolidBrush bgB = new SolidBrush(isCurrent ? GlassGfx.Blend(_surface, _accent, 0.16f) : GlassGfx.Blend(_bg, _surface, 0.75f)))
                    using (System.Drawing.Pen borderP = new System.Drawing.Pen(isCurrent ? System.Drawing.Color.FromArgb(145, _accent) : System.Drawing.Color.FromArgb(40, 255, 255, 255), 1f))
                    {
                        e.Graphics.FillPath(bgB, cp);
                        e.Graphics.DrawPath(borderP, cp);
                    }
                    if (isCurrent)
                    {
                        using (GraphicsPath barP = GlassGfx.CreateRoundRect(new RectangleF(cardR.X + 4f, cardR.Y + 7f, 3f, cardR.Height - 14f), 1.5f))
                        using (SolidBrush barB = new SolidBrush(_accent))
                        {
                            e.Graphics.FillPath(barB, barP);
                        }
                    }
                }

                if (_lyrics != null && e.Index < _lyrics.Count)
                {
                    LyricLine line = _lyrics[e.Index];
                    int secVal = (int)line.TimeSec;
                    string timeStr = string.Format("{0}:{1:D2}", secVal / 60, secVal % 60);
                    TextRenderer.DrawText(e.Graphics, timeStr, _fontListTime,
                        new Rectangle(e.Bounds.X + 14, e.Bounds.Y, 44, e.Bounds.Height),
                        isCurrent ? _accent : System.Drawing.Color.FromArgb(115, 135, 165),
                        TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                    Font lFont = isCurrent ? _fontListBold : _fontListRegular;
                    System.Drawing.Color lCol = isCurrent ? System.Drawing.Color.White : _textMuted;
                    Rectangle textRect = new Rectangle(e.Bounds.X + 62, e.Bounds.Y, e.Bounds.Width - 70, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, line.Text, lFont, textRect, lCol,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
                else
                {
                    string itemText = _lstLyrics.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, itemText, _fontListItalic, textRect, _textMuted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            };

            p.Controls.Add(_lyricsHeaderCard);
            p.Controls.Add(_lstLyrics);

            _tabPanels[2] = p;
            _deckPanel.Controls.Add(p);
        }

        // --- TAB 3: QUEUE VIEW & LOCAL FILE IMPORT ---
        private RectangleF GetQueueRemoveRect(Rectangle itemBounds)
        {
            float cardRight = itemBounds.Right - 4f;
            float cardTop = itemBounds.Y + 2f;
            float cardH = itemBounds.Height - 4f;
            return new RectangleF(cardRight - 30f, cardTop + (cardH - 22f) / 2f, 22f, 22f);
        }

        private void BuildTabQueue()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };

            _queueHeaderCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(contentW, 38),
                CornerRadius = 10
            };

            _lblQueueHeader = new Label
            {
                Location = new System.Drawing.Point(12, 0),
                Size = new System.Drawing.Size(contentW - 230, 38),
                Text = "QUEUE • Double-click to play",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _btnClearQueue = new GlassButton
            {
                Text = "CLEAR",
                IsPill = true,
                Location = new System.Drawing.Point(contentW - 210, 6),
                Size = new System.Drawing.Size(78, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnClearQueue, "Clear playback queue");
            _btnClearQueue.Click += (s, e) => ClearQueue(false);

            _btnAddFile = new GlassButton
            {
                Text = "+ LOCAL AUDIO",
                IsPill = true,
                Location = new System.Drawing.Point(contentW - 126, 6),
                Size = new System.Drawing.Size(116, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnAddFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Audio Files (*.mp3;*.wav;*.flac;*.m4a)|*.mp3;*.wav;*.flac;*.m4a|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        LoadLocalAudioFile(ofd.FileName);
                    }
                }
            };
            _queueHeaderCard.Controls.Add(_lblQueueHeader);
            _queueHeaderCard.Controls.Add(_btnClearQueue);
            _queueHeaderCard.Controls.Add(_btnAddFile);

            _lstQueue = new DarkListBox
            {
                Location = new System.Drawing.Point(0, 44),
                Size = new System.Drawing.Size(contentW, contentH - 48),
                ItemHeight = 54
            };

            // Dark Glass Right-Click Context Menu for Queue Management
            ContextMenuStrip queueMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = System.Drawing.Color.FromArgb(18, 25, 40),
                ForeColor = System.Drawing.Color.White,
                Renderer = new DarkMenuRenderer(new DarkSelect { AccentColor = _accent })
            };
            ToolStripMenuItem miPlay = new ToolStripMenuItem("Play Track") { Height = 28 };
            ToolStripMenuItem miRemove = new ToolStripMenuItem("Remove from Queue") { Height = 28 };
            ToolStripMenuItem miClearOthers = new ToolStripMenuItem("Clear Other Tracks") { Height = 28 };
            ToolStripMenuItem miClearAll = new ToolStripMenuItem("Clear Entire Queue") { Height = 28 };
            miPlay.Click += (s, e) =>
            {
                if (_lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    LoadTrack(_queue[_lstQueue.SelectedIndex], true);
                    SwitchTab(0);
                }
            };
            miRemove.Click += (s, e) =>
            {
                if (_lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    RemoveQueueTrackAt(_lstQueue.SelectedIndex);
                }
            };
            miClearOthers.Click += (s, e) => ClearQueue(true);
            miClearAll.Click += (s, e) => ClearQueue(false);
            queueMenu.Items.AddRange(new ToolStripItem[] { miPlay, miRemove, new ToolStripSeparator(), miClearOthers, miClearAll });
            _lstQueue.ContextMenuStrip = queueMenu;

            _lstQueue.MouseMove += (s, e) =>
            {
                int idx = _lstQueue.IndexFromPoint(e.Location);
                int nextHover = -1;
                if (idx >= 0 && idx < _queue.Count)
                {
                    Rectangle r = _lstQueue.GetItemRectangle(idx);
                    if (GetQueueRemoveRect(r).Contains(e.Location))
                    {
                        nextHover = idx;
                    }
                }
                if (nextHover != _queueHoverRemoveIdx)
                {
                    _queueHoverRemoveIdx = nextHover;
                    _lstQueue.Cursor = (nextHover >= 0) ? Cursors.Hand : Cursors.Default;
                    _lstQueue.Invalidate();
                }
            };
            _lstQueue.MouseLeave += (s, e) =>
            {
                if (_queueHoverRemoveIdx != -1)
                {
                    _queueHoverRemoveIdx = -1;
                    _lstQueue.Cursor = Cursors.Default;
                    _lstQueue.Invalidate();
                }
            };
            _lstQueue.MouseDown += (s, e) =>
            {
                int idx = _lstQueue.IndexFromPoint(e.Location);
                if (idx >= 0 && idx < _queue.Count)
                {
                    _lstQueue.SelectedIndex = idx;
                    if (e.Button == MouseButtons.Left)
                    {
                        Rectangle r = _lstQueue.GetItemRectangle(idx);
                        if (GetQueueRemoveRect(r).Contains(e.Location))
                        {
                            RemoveQueueTrackAt(idx);
                            return;
                        }
                    }
                }
            };
            _lstQueue.KeyDown += (s, e) =>
            {
                if ((e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) && _lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    e.Handled = true;
                    RemoveQueueTrackAt(_lstQueue.SelectedIndex);
                }
                else if (e.KeyCode == Keys.Enter && _lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    LoadTrack(_queue[_lstQueue.SelectedIndex], true);
                    SwitchTab(0);
                }
            };
            _lstQueue.DoubleClick += (s, e) =>
            {
                if (_lstQueue.SelectedIndex >= 0 && _lstQueue.SelectedIndex < _queue.Count)
                {
                    Point pt = _lstQueue.PointToClient(Cursor.Position);
                    Rectangle r = _lstQueue.GetItemRectangle(_lstQueue.SelectedIndex);
                    if (GetQueueRemoveRect(r).Contains(pt)) return;
                    LoadTrack(_queue[_lstQueue.SelectedIndex], true);
                    SwitchTab(0);
                }
            };
            _lstQueue.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstQueue.Items.Count) return;
                GlassGfx.ApplyCrisp(e.Graphics);
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                using (SolidBrush clrB = new SolidBrush(_bg))
                {
                    e.Graphics.FillRectangle(clrB, e.Bounds);
                }

                RectangleF cardR = new RectangleF(e.Bounds.X + 2f, e.Bounds.Y + 2f, e.Bounds.Width - 6f, e.Bounds.Height - 4f);
                bool isCurrent = (_queue != null && e.Index < _queue.Count && _currentTrack != null &&
                                  _currentTrack.VideoId == _queue[e.Index].VideoId && _currentTrack.Title == _queue[e.Index].Title);

                using (GraphicsPath cp = GlassGfx.CreateRoundRect(cardR, 9f))
                {
                    System.Drawing.Color fillCol = isCurrent
                        ? GlassGfx.Blend(_surface, _accent, 0.14f)
                        : (isSelected ? GlassGfx.Blend(_bg, _surface, 0.85f) : GlassGfx.Blend(_bg, _surface, 0.45f));
                    using (SolidBrush bgB = new SolidBrush(fillCol))
                    {
                        e.Graphics.FillPath(bgB, cp);
                    }
                    using (System.Drawing.Pen borderP = new System.Drawing.Pen(isCurrent ? System.Drawing.Color.FromArgb(145, _accent) : (isSelected ? System.Drawing.Color.FromArgb(65, 255, 255, 255) : System.Drawing.Color.FromArgb(24, 255, 255, 255)), 1f))
                    {
                        e.Graphics.DrawPath(borderP, cp);
                    }
                }

                if (_queue != null && e.Index < _queue.Count)
                {
                    TrackItem item = _queue[e.Index];

                    string numStr = isCurrent ? "▶" : (e.Index + 1).ToString();
                    TextRenderer.DrawText(e.Graphics, numStr, _fontListTime,
                        new Rectangle((int)cardR.X + 6, (int)cardR.Y, 26, (int)cardR.Height),
                        isCurrent ? _accent : _textMuted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                    // Per-row Remove (×) button on right
                    RectangleF rmRect = GetQueueRemoveRect(e.Bounds);
                    bool rmHover = (_queueHoverRemoveIdx == e.Index);
                    using (SolidBrush rmBg = new SolidBrush(rmHover ? System.Drawing.Color.FromArgb(68, 248, 113, 113) : System.Drawing.Color.FromArgb(22, 255, 255, 255)))
                    using (System.Drawing.Pen rmRim = new System.Drawing.Pen(rmHover ? System.Drawing.Color.FromArgb(248, 113, 113) : System.Drawing.Color.FromArgb(42, 255, 255, 255), 1f))
                    using (System.Drawing.Pen rmX = new System.Drawing.Pen(rmHover ? System.Drawing.Color.FromArgb(254, 202, 202) : _textMuted, 1.5f))
                    {
                        rmX.StartCap = LineCap.Round;
                        rmX.EndCap = LineCap.Round;
                        e.Graphics.FillEllipse(rmBg, rmRect);
                        e.Graphics.DrawEllipse(rmRim, rmRect);
                        float cxm = rmRect.X + rmRect.Width / 2f;
                        float cym = rmRect.Y + rmRect.Height / 2f;
                        e.Graphics.DrawLine(rmX, cxm - 3.2f, cym - 3.2f, cxm + 3.2f, cym + 3.2f);
                        e.Graphics.DrawLine(rmX, cxm + 3.2f, cym - 3.2f, cxm - 3.2f, cym + 3.2f);
                    }

                    int rightBadgeW = 34;
                    if (isCurrent)
                    {
                        RectangleF nowR = new RectangleF(rmRect.X - 48f, cardR.Y + (cardR.Height - 18f) / 2f, 42f, 18f);
                        using (GraphicsPath np = GlassGfx.CreateRoundRect(nowR, 6f))
                        using (SolidBrush nb = new SolidBrush(System.Drawing.Color.FromArgb(38, _accent)))
                        {
                            e.Graphics.FillPath(nb, np);
                        }
                        TextRenderer.DrawText(e.Graphics, "NOW", _fontBadge, Rectangle.Round(nowR), _accent,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        rightBadgeW = 86;
                    }

                    int tx = (int)cardR.X + 36;
                    int tw = (int)cardR.Right - tx - rightBadgeW - 6;
                    TextRenderer.DrawText(e.Graphics, item.Title, _fontListRegular,
                        new Rectangle(tx, (int)cardR.Y + 4, tw, 24),
                        isCurrent ? _accent : System.Drawing.Color.White,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, item.Artist + " • " + (item.Source == "LOCAL" ? "Local Audio" : "YT Music"), _fontListSub,
                        new Rectangle(tx, (int)cardR.Y + 26, tw, 20),
                        _textMuted,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
                else
                {
                    string fallback = _lstQueue.Items[e.Index].ToString();
                    Rectangle textRect = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, fallback, _fontListItalic, textRect, _textMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            };

            p.Controls.Add(_queueHeaderCard);
            p.Controls.Add(_lstQueue);

            _tabPanels[3] = p;
            _deckPanel.Controls.Add(p);
            RefreshQueueUI();
        }

        // --- TAB 4: PRO MASHUP STUDIO (DUAL DECKS, BPM SYNC, AUTO-BLEND, WAV EXPORT) ---
        private void BuildTabMashup()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };

            // 1. TOP MASTER BAR
            _mashTopCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(contentW, 40),
                CornerRadius = 11
            };

            _btnMashSync = new GlassButton
            {
                Text = "BEAT SYNC",
                IsPill = true,
                Location = new System.Drawing.Point(8, 7),
                Size = new System.Drawing.Size(90, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.4f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnMashSync, "Lock Deck B tempo to Deck A BPM");
            _btnMashSync.Click += (s, e) => SyncMashupBpm();

            _btnMashCurve = new GlassButton
            {
                Text = "CURVE: EQ PWR",
                IsPill = true,
                Location = new System.Drawing.Point(104, 7),
                Size = new System.Drawing.Size(112, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnMashCurve, "Cycle Crossfader Law: Equal Power / S-Curve / Hard Cut");
            _btnMashCurve.Click += (s, e) => CycleMashupCurve();

            _btnMashDuck = new GlassButton
            {
                Text = "DUCK: ON",
                IsPill = true,
                IsActive = true,
                Location = new System.Drawing.Point(222, 7),
                Size = new System.Drawing.Size(80, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnMashDuck, "Smart sidechain ducking when both decks blend");
            _btnMashDuck.Click += (s, e) =>
            {
                _mashDucking = !_mashDucking;
                _btnMashDuck.IsActive = _mashDucking;
                _btnMashDuck.Text = _mashDucking ? "DUCK: ON" : "DUCK: OFF";
                _btnMashDuck.Invalidate();
                UpdateMashupMix();
            };

            _lblMashStatus = new Label
            {
                Location = new System.Drawing.Point(308, 0),
                Size = new System.Drawing.Size(contentW - 422, 40),
                Text = "DUAL-DECK ENGINE",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Segoe UI", 7.4f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _btnMashExport = new GlassButton
            {
                Text = "EXPORT .WAV",
                IsPill = true,
                IsActive = true,
                Location = new System.Drawing.Point(contentW - 108, 7),
                Size = new System.Drawing.Size(100, 26),
                Font = new System.Drawing.Font("Segoe UI", 7.3f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(_btnMashExport, "Render & export studio 44.1kHz stereo mashup .WAV");
            _btnMashExport.Click += (s, e) => ExportMashupWav();

            _mashTopCard.Controls.Add(_btnMashSync);
            _mashTopCard.Controls.Add(_btnMashCurve);
            _mashTopCard.Controls.Add(_btnMashDuck);
            _mashTopCard.Controls.Add(_lblMashStatus);
            _mashTopCard.Controls.Add(_btnMashExport);
            p.Controls.Add(_mashTopCard);

            // 2. DUAL DECK CARDS (SIDE BY SIDE)
            int deckW = (contentW - 8) / 2; // 280px each
            int deckH = 262;
            _mashCardA = BuildSingleMashupDeckCard(true, 0, 46, deckW, deckH);
            _mashCardB = BuildSingleMashupDeckCard(false, deckW + 8, 46, deckW, deckH);
            p.Controls.Add(_mashCardA);
            p.Controls.Add(_mashCardB);

            // 3. BOTTOM PRO CROSSFADER & AUTO-BLEND CONSOLE
            _mashMixerCard = new GlassCard
            {
                Location = new System.Drawing.Point(0, 314),
                Size = new System.Drawing.Size(contentW, contentH - 318),
                CornerRadius = 13
            };

            Label lblDeckAL = new Label
            {
                Location = new System.Drawing.Point(14, 10),
                Size = new System.Drawing.Size(60, 18),
                Text = "DECK A",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _lblMashMixRatio = new Label
            {
                Location = new System.Drawing.Point(80, 10),
                Size = new System.Drawing.Size(contentW - 160, 18),
                Text = "CROSSFADER • 50% A / 50% B (CENTER BLEND)",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };

            Label lblDeckBR = new Label
            {
                Location = new System.Drawing.Point(contentW - 74, 10),
                Size = new System.Drawing.Size(60, 18),
                Text = "DECK B",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _secondaryAccent,
                Font = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

            _sliderMashCrossfader = new DarkSlider
            {
                Location = new System.Drawing.Point(14, 32),
                Size = new System.Drawing.Size(contentW - 28, 24),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                IsCenterZero = false,
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };
            _sliderMashCrossfader.ValueChanged += (s, e) =>
            {
                if (!_mashInternalUpdate) _mashAutoBlending = false;
                _mashCrossfade = _sliderMashCrossfader.Value;
                UpdateMashupMix();
            };

            int botY = 68;
            int btnGap = 8;
            int wDur = 92;
            int wBlend = 136;
            int wStop = contentW - 28 - wDur - wBlend * 2 - btnGap * 3;

            _btnMashDur = new GlassButton
            {
                Text = "BLEND: 8s",
                IsPill = true,
                Location = new System.Drawing.Point(14, botY),
                Size = new System.Drawing.Size(wDur, 32),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnMashDur.Click += (s, e) =>
            {
                int[] durs = { 4, 8, 12, 16 };
                int idx = Array.IndexOf(durs, _mashBlendSec);
                _mashBlendSec = durs[(idx + 1) % durs.Length];
                _btnMashDur.Text = "BLEND: " + _mashBlendSec + "s";
                _btnMashDur.Invalidate();
            };

            _btnMashBlendAB = new GlassButton
            {
                Text = "AUTO BLEND A → B",
                IsPill = true,
                Location = new System.Drawing.Point(14 + wDur + btnGap, botY),
                Size = new System.Drawing.Size(wBlend, 32),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnMashBlendAB.Click += (s, e) => StartAutoBlend(true);

            _btnMashBlendBA = new GlassButton
            {
                Text = "AUTO BLEND B → A",
                IsPill = true,
                Location = new System.Drawing.Point(14 + wDur + wBlend + btnGap * 2, botY),
                Size = new System.Drawing.Size(wBlend, 32),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnMashBlendBA.Click += (s, e) => StartAutoBlend(false);

            _btnMashStopAll = new GlassButton
            {
                Text = "STOP ALL",
                IsPill = true,
                Location = new System.Drawing.Point(14 + wDur + wBlend * 2 + btnGap * 3, botY),
                Size = new System.Drawing.Size(wStop, 32),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnMashStopAll.Click += (s, e) => StopAllMashupDecks();

            _mashMixerCard.Controls.Add(lblDeckAL);
            _mashMixerCard.Controls.Add(_lblMashMixRatio);
            _mashMixerCard.Controls.Add(lblDeckBR);
            _mashMixerCard.Controls.Add(_sliderMashCrossfader);
            _mashMixerCard.Controls.Add(_btnMashDur);
            _mashMixerCard.Controls.Add(_btnMashBlendAB);
            _mashMixerCard.Controls.Add(_btnMashBlendBA);
            _mashMixerCard.Controls.Add(_btnMashStopAll);
            p.Controls.Add(_mashMixerCard);

            _mashDeckA.MediaEnded += () => SafeUi(() => { UpdateMashupButtonsUI(); });
            _mashDeckB.MediaEnded += () => SafeUi(() => { UpdateMashupButtonsUI(); });

            _tabPanels[4] = p;
            _deckPanel.Controls.Add(p);
            RefreshMashupQueueSelectors();
            UpdateMashupMix();
        }

        private GlassCard BuildSingleMashupDeckCard(bool isDeckA, int x, int y, int w, int h)
        {
            GlassCard card = new GlassCard
            {
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, h),
                CornerRadius = 13
            };

            Label lblDeckHdr = new Label
            {
                Location = new System.Drawing.Point(12, 10),
                Size = new System.Drawing.Size(64, 20),
                Text = isDeckA ? "DECK A" : "DECK B",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = isDeckA ? _accent : _secondaryAccent,
                Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold),
                UseMnemonic = false
            };

            Label lblBpm = new Label
            {
                Location = new System.Drawing.Point(78, 10),
                Size = new System.Drawing.Size(w - 154, 20),
                Text = "NO TRACK LOADED",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Consolas", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };

            GlassButton btnFile = new GlassButton
            {
                Text = "+ FILE",
                IsPill = true,
                Location = new System.Drawing.Point(w - 68, 8),
                Size = new System.Drawing.Size(58, 22),
                Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold)
            };
            btnFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Audio Files (*.mp3;*.wav;*.flac;*.m4a)|*.mp3;*.wav;*.flac;*.m4a|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        TrackItem tr = CreateLocalTrackItem(ofd.FileName);
                        LoadMashupTrack(isDeckA, tr);
                    }
                }
            };

            DarkSelect cmb = new DarkSelect
            {
                Location = new System.Drawing.Point(10, 36),
                Size = new System.Drawing.Size(w - 20, 28),
                Font = new System.Drawing.Font("Segoe UI", 8.3f),
                AccentColor = _accent,
                PlaceholderText = "Select track from queue..."
            };
            cmb.SelectedIndexChanged += (s, e) =>
            {
                if (_mashInternalUpdate) return;
                if (cmb.SelectedIndex >= 0 && cmb.SelectedIndex < _queue.Count)
                {
                    LoadMashupTrack(isDeckA, _queue[cmb.SelectedIndex]);
                }
            };

            MashupWaveformView wave = new MashupWaveformView
            {
                Location = new System.Drawing.Point(10, 72),
                Size = new System.Drawing.Size(w - 20, 72),
                AccentColor = isDeckA ? _accent : _secondaryAccent,
                SecondaryColor = _secondaryAccent
            };
            wave.SeekRequested += (pct) =>
            {
                NativeAudioPlayer pl = isDeckA ? _mashDeckA : _mashDeckB;
                AudioAnalysis an = isDeckA ? _mashAnalA : _mashAnalB;
                int dur = pl.GetLengthMs();
                if (dur <= 0 && an != null) dur = an.DurationMs;
                if (dur > 0)
                {
                    pl.Seek((int)(pct * dur));
                    wave.Progress = pct;
                    wave.Invalidate();
                }
            };

            GlassButton btnPlay = new GlassButton
            {
                Text = "PLAY",
                IsPill = true,
                Location = new System.Drawing.Point(10, 154),
                Size = new System.Drawing.Size(80, 30),
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold)
            };
            btnPlay.Click += (s, e) => ToggleMashupPlay(isDeckA);

            GlassButton btnCue = new GlassButton
            {
                Text = "CUE DROP",
                IsPill = true,
                Location = new System.Drawing.Point(96, 154),
                Size = new System.Drawing.Size(92, 30),
                Font = new System.Drawing.Font("Segoe UI", 7.4f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(btnCue, "Jump directly to detected Drop Cue point");
            btnCue.Click += (s, e) => CueMashupDrop(isDeckA);

            GlassButton btnPan = new GlassButton
            {
                Text = "PAN: C",
                IsPill = true,
                Location = new System.Drawing.Point(w - 78, 154),
                Size = new System.Drawing.Size(68, 30),
                Font = new System.Drawing.Font("Segoe UI", 7.4f, FontStyle.Bold)
            };
            _toolTip.SetToolTip(btnPan, "Cycle Stereo Pan: Center / Left / Right");
            btnPan.Click += (s, e) => CycleMashupPan(isDeckA);

            Label lblGain = new Label
            {
                Location = new System.Drawing.Point(10, 196),
                Size = new System.Drawing.Size(44, 22),
                Text = "GAIN",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 7.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            DarkSlider slTrim = new DarkSlider
            {
                Location = new System.Drawing.Point(54, 196),
                Size = new System.Drawing.Size(w - 66, 22),
                Minimum = 0,
                Maximum = 120,
                Value = 100,
                AccentColor = isDeckA ? _accent : _secondaryAccent,
                SecondaryColor = _secondaryAccent
            };
            slTrim.ValueChanged += (s, e) =>
            {
                if (isDeckA) _mashTrimA = slTrim.Value / 100.0;
                else _mashTrimB = slTrim.Value / 100.0;
                UpdateMashupMix();
            };

            Label lblTime = new Label
            {
                Location = new System.Drawing.Point(10, 226),
                Size = new System.Drawing.Size(w - 20, 22),
                Text = "0:00 / 0:00  •  DROP @ --:--",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _textMuted,
                Font = new System.Drawing.Font("Consolas", 7.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };

            card.Controls.Add(lblDeckHdr);
            card.Controls.Add(lblBpm);
            card.Controls.Add(btnFile);
            card.Controls.Add(cmb);
            card.Controls.Add(wave);
            card.Controls.Add(btnPlay);
            card.Controls.Add(btnCue);
            card.Controls.Add(btnPan);
            card.Controls.Add(lblGain);
            card.Controls.Add(slTrim);
            card.Controls.Add(lblTime);

            if (isDeckA)
            {
                _lblMashBpmA = lblBpm;
                _btnMashFileA = btnFile;
                _cmbMashA = cmb;
                _mashWaveA = wave;
                _btnMashPlayA = btnPlay;
                _btnMashCueA = btnCue;
                _btnMashPanA = btnPan;
                _sliderMashTrimA = slTrim;
                _lblMashTimeA = lblTime;
            }
            else
            {
                _lblMashBpmB = lblBpm;
                _btnMashFileB = btnFile;
                _cmbMashB = cmb;
                _mashWaveB = wave;
                _btnMashPlayB = btnPlay;
                _btnMashCueB = btnCue;
                _btnMashPanB = btnPan;
                _sliderMashTrimB = slTrim;
                _lblMashTimeB = lblTime;
            }
            return card;
        }

        private void RefreshMashupQueueSelectors()
        {
            if (_cmbMashA == null || _cmbMashB == null) return;
            _mashInternalUpdate = true;
            try
            {
                _cmbMashA.ClearItems();
                _cmbMashB.ClearItems();
                int selA = -1;
                int selB = -1;
                for (int i = 0; i < _queue.Count; i++)
                {
                    string label = string.Format("{0}. {1} — {2}", i + 1, _queue[i].Title, _queue[i].Artist);
                    _cmbMashA.AddItem(label, _accent);
                    _cmbMashB.AddItem(label, _secondaryAccent);
                    if (_mashTrackA != null && _queue[i].Title == _mashTrackA.Title && _queue[i].VideoId == _mashTrackA.VideoId)
                    {
                        selA = i;
                    }
                    if (_mashTrackB != null && _queue[i].Title == _mashTrackB.Title && _queue[i].VideoId == _mashTrackB.VideoId)
                    {
                        selB = i;
                    }
                }

                // Preserve active selection only; never auto-load or open audio streams at startup
                _cmbMashA.SetSelectedIndexSilent(selA);
                _cmbMashB.SetSelectedIndexSilent(selB);
            }
            finally
            {
                _mashInternalUpdate = false;
            }
        }

        private void LoadMashupTrack(bool isDeckA, TrackItem track)
        {
            if (track == null) return;
            TrackItem clone = new TrackItem
            {
                Title = track.Title,
                Artist = track.Artist,
                Genre = track.Genre,
                Source = track.Source,
                Url = track.Url,
                VideoId = track.VideoId,
                DurationMs = track.DurationMs > 0 ? track.DurationMs : 180000
            };

            if (isDeckA) _mashTrackA = clone;
            else _mashTrackB = clone;

            int loadId = isDeckA ? Interlocked.Increment(ref _mashLoadIdA) : Interlocked.Increment(ref _mashLoadIdB);
            MashupWaveformView wave = isDeckA ? _mashWaveA : _mashWaveB;
            Label lblBpm = isDeckA ? _lblMashBpmA : _lblMashBpmB;
            NativeAudioPlayer deck = isDeckA ? _mashDeckA : _mashDeckB;

            deck.Stop();
            UpdateMashupButtonsUI();

            if (wave != null)
            {
                wave.IsAnalyzing = true;
                wave.Progress = 0f;
                wave.Peaks = null;
                wave.Invalidate();
            }
            if (lblBpm != null) lblBpm.Text = "LOADING...";

            ThreadPool.QueueUserWorkItem(state =>
            {
                string filePath = clone.Url;
                if (string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(clone.VideoId))
                {
                    string cacheDir = Path.Combine(Path.GetTempPath(), "music_widget_cache");
                    Directory.CreateDirectory(cacheDir);
                    string target = Path.Combine(cacheDir, clone.VideoId + ".m4a");
                    if (!IsValidCachedAudio(target))
                    {
                        bool ok = TryRunYtDlpMashup("yt-dlp", clone.VideoId, target, false);
                        if (!ok) TryRunYtDlpMashup("python", clone.VideoId, target, true);
                    }
                    if (IsValidCachedAudio(target)) filePath = target;
                }

                int curLoad = isDeckA ? _mashLoadIdA : _mashLoadIdB;
                if (loadId != curLoad) return;

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    SafeUi(() =>
                    {
                        if (wave != null) { wave.IsAnalyzing = false; wave.Invalidate(); }
                        if (lblBpm != null) lblBpm.Text = "UNAVAILABLE";
                    });
                    return;
                }

                clone.Url = filePath;
                AudioAnalysis anal = MashupAnalyzer.AnalyzeFile(filePath, clone.DurationMs);
                curLoad = isDeckA ? _mashLoadIdA : _mashLoadIdB;
                if (loadId != curLoad) return;

                SafeUi(() =>
                {
                    if (isDeckA) { _mashAnalA = anal; _mashSpeedA = 1.0; }
                    else { _mashAnalB = anal; _mashSpeedB = 1.0; }

                    deck.Open(filePath);
                    deck.SetSpeedRatio(1.0);
                    deck.SetBalance(isDeckA ? _mashPanA : _mashPanB);
                    UpdateMashupMix();

                    if (wave != null)
                    {
                        wave.IsAnalyzing = false;
                        wave.Peaks = anal.IsReady ? anal.WaveformPeaks : null;
                        wave.DropCuePct = (anal.IsReady && anal.DurationMs > 0) ? (float)anal.DropCueMs / anal.DurationMs : -1f;
                        wave.Invalidate();
                    }
                    if (lblBpm != null)
                    {
                        lblBpm.Text = anal.IsReady ? string.Format("{0:0.0} BPM • 1.00x", anal.Bpm) : "READY • 1.00x";
                    }
                    UpdateMashupTimeLabels();
                });
            });
        }

        private bool TryRunYtDlpMashup(string exe, string videoId, string targetFile, bool isPython)
        {
            string tempFile = targetFile + ".mash.tmp.m4a";
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                string args = isPython
                    ? string.Format("-m yt_dlp --js-runtimes node -f \"140/139/141/ba[ext=m4a]/18/b[ext=mp4]\" --no-playlist --no-part -o \"{0}\" \"https://www.youtube.com/watch?v={1}\"", tempFile, videoId)
                    : string.Format("--js-runtimes node -f \"140/139/141/ba[ext=m4a]/18/b[ext=mp4]\" --no-playlist --no-part -o \"{0}\" \"https://www.youtube.com/watch?v={1}\"", tempFile, videoId);
                ProcessStartInfo psi = new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false };
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(45000)) { try { p.Kill(); } catch { } }
                }
                if (IsValidCachedAudio(tempFile))
                {
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Move(tempFile, targetFile);
                    return true;
                }
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch { try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { } }
            return false;
        }

        private void ToggleMashupPlay(bool isDeckA)
        {
            TrackItem tr = isDeckA ? _mashTrackA : _mashTrackB;
            NativeAudioPlayer deck = isDeckA ? _mashDeckA : _mashDeckB;
            if (tr == null || string.IsNullOrEmpty(tr.Url))
            {
                if (_lblMashStatus != null) _lblMashStatus.Text = "LOAD TRACK FIRST";
                return;
            }
            // Pause main single-track player so mashup decks don't clash with main Deck tab
            if (!deck.IsPlaying && _player.IsPlaying)
            {
                _player.Pause();
                UpdatePlayButtonUI(false);
            }
            deck.TogglePlay();
            UpdateMashupMix();
            UpdateMashupButtonsUI();
        }

        private void CueMashupDrop(bool isDeckA)
        {
            AudioAnalysis an = isDeckA ? _mashAnalA : _mashAnalB;
            NativeAudioPlayer deck = isDeckA ? _mashDeckA : _mashDeckB;
            TrackItem tr = isDeckA ? _mashTrackA : _mashTrackB;
            if (tr == null || string.IsNullOrEmpty(tr.Url)) return;

            int targetMs = (an != null && an.IsReady && an.DropCueMs > 0) ? an.DropCueMs : 0;
            if (_player.IsPlaying) { _player.Pause(); UpdatePlayButtonUI(false); }
            deck.Seek(targetMs);
            if (!deck.IsPlaying) deck.Play();
            UpdateMashupMix();
            UpdateMashupButtonsUI();
        }

        private void CycleMashupPan(bool isDeckA)
        {
            double cur = isDeckA ? _mashPanA : _mashPanB;
            double next = (Math.Abs(cur) < 0.1) ? -0.75 : (cur < -0.1 ? 0.75 : 0.0);
            string lbl = (Math.Abs(next) < 0.1) ? "PAN: C" : (next < 0 ? "PAN: L" : "PAN: R");

            if (isDeckA) { _mashPanA = next; _mashDeckA.SetBalance(next); if (_btnMashPanA != null) { _btnMashPanA.Text = lbl; _btnMashPanA.IsActive = (Math.Abs(next) > 0.1); _btnMashPanA.Invalidate(); } }
            else { _mashPanB = next; _mashDeckB.SetBalance(next); if (_btnMashPanB != null) { _btnMashPanB.Text = lbl; _btnMashPanB.IsActive = (Math.Abs(next) > 0.1); _btnMashPanB.Invalidate(); } }
        }

        private void SyncMashupBpm()
        {
            if (_mashAnalA == null || _mashAnalB == null || !_mashAnalA.IsReady || !_mashAnalB.IsReady)
            {
                if (_lblMashStatus != null) _lblMashStatus.Text = "ANALYZING TRACKS...";
                return;
            }
            double ratio = MashupDsp.ComputeBeatSyncRatio(_mashAnalA.Bpm, _mashAnalB.Bpm);
            _mashSpeedB = ratio;
            _mashDeckB.SetSpeedRatio(ratio);
            if (_lblMashBpmB != null) _lblMashBpmB.Text = string.Format("{0:0.0} BPM • {1:0.00}x", _mashAnalB.Bpm * ratio, ratio);
            if (_btnMashSync != null) { _btnMashSync.IsActive = Math.Abs(ratio - 1.0) > 0.002 || Math.Abs(_mashAnalA.Bpm - _mashAnalB.Bpm) < 1.0; _btnMashSync.Invalidate(); }
            if (_lblMashStatus != null) _lblMashStatus.Text = string.Format("SYNCED @ {0:0.0} BPM", _mashAnalA.Bpm);
        }

        private void CycleMashupCurve()
        {
            _mashCurve = (CrossfadeCurve)(((int)_mashCurve + 1) % 3);
            if (_btnMashCurve != null)
            {
                string txt = _mashCurve == CrossfadeCurve.EqualPower ? "CURVE: EQ PWR" : (_mashCurve == CrossfadeCurve.SmoothSCurve ? "CURVE: SMOOTH" : "CURVE: CUT");
                _btnMashCurve.Text = txt;
                _btnMashCurve.Invalidate();
            }
            UpdateMashupMix();
        }

        private void StartAutoBlend(bool aToB)
        {
            if (_mashTrackA == null || string.IsNullOrEmpty(_mashTrackA.Url) || _mashTrackB == null || string.IsNullOrEmpty(_mashTrackB.Url))
            {
                if (_lblMashStatus != null) _lblMashStatus.Text = "LOAD DECK A & B FIRST";
                return;
            }

            if (_player.IsPlaying) { _player.Pause(); UpdatePlayButtonUI(false); }

            // Auto beat sync when both BPMs are analyzed
            if (_mashAnalA != null && _mashAnalB != null && _mashAnalA.IsReady && _mashAnalB.IsReady)
            {
                SyncMashupBpm();
            }

            if (aToB)
            {
                if (!_mashDeckA.IsPlaying) _mashDeckA.Play();
                int cueB = (_mashAnalB != null && _mashAnalB.IsReady) ? _mashAnalB.IntroCueMs : 0;
                _mashDeckB.Seek(cueB);
                if (!_mashDeckB.IsPlaying) _mashDeckB.Play();
                _mashBlendStartVal = 0;
                _mashBlendTargetVal = 100;
            }
            else
            {
                if (!_mashDeckB.IsPlaying) _mashDeckB.Play();
                int cueA = (_mashAnalA != null && _mashAnalA.IsReady) ? _mashAnalA.IntroCueMs : 0;
                _mashDeckA.Seek(cueA);
                if (!_mashDeckA.IsPlaying) _mashDeckA.Play();
                _mashBlendStartVal = 100;
                _mashBlendTargetVal = 0;
            }

            _mashInternalUpdate = true;
            _mashCrossfade = _mashBlendStartVal;
            if (_sliderMashCrossfader != null) _sliderMashCrossfader.Value = _mashCrossfade;
            _mashInternalUpdate = false;

            _mashBlendStartUtc = DateTime.UtcNow;
            _mashAutoBlending = true;
            if (_lblMashStatus != null) _lblMashStatus.Text = aToB ? "BLENDING A → B..." : "BLENDING B → A...";
            UpdateMashupMix();
            UpdateMashupButtonsUI();
        }

        private void StopAllMashupDecks()
        {
            _mashAutoBlending = false;
            _mashDeckA.Pause();
            _mashDeckB.Pause();
            UpdateMashupButtonsUI();
            if (_lblMashStatus != null) _lblMashStatus.Text = "PAUSED";
        }

        private void UpdateMashupButtonsUI()
        {
            if (_btnMashPlayA != null)
            {
                _btnMashPlayA.Text = _mashDeckA.IsPlaying ? "PAUSE" : "PLAY";
                _btnMashPlayA.IsActive = _mashDeckA.IsPlaying;
                _btnMashPlayA.Invalidate();
            }
            if (_btnMashPlayB != null)
            {
                _btnMashPlayB.Text = _mashDeckB.IsPlaying ? "PAUSE" : "PLAY";
                _btnMashPlayB.IsActive = _mashDeckB.IsPlaying;
                _btnMashPlayB.Invalidate();
            }
        }

        private void UpdateMashupMix()
        {
            double p = _mashCrossfade / 100.0;
            double gA, gB;
            MashupDsp.ComputeCrossfadeGains(p, _mashCurve, out gA, out gB);

            double duckFactor = 1.0;
            if (_mashDucking && _mashDeckA.IsPlaying && _mashDeckB.IsPlaying && p > 0.08 && p < 0.92 && _mashAnalA != null && _mashAnalA.IsReady)
            {
                int durA = Math.Max(1, _mashAnalA.DurationMs);
                int bin = Math.Max(0, Math.Min(_mashAnalA.WaveformPeaks.Length - 1, (_mashDeckA.GetPositionMs() * _mashAnalA.WaveformPeaks.Length) / durA));
                float energy = _mashAnalA.WaveformPeaks[bin];
                duckFactor = 1.0 - energy * 0.26;
            }

            double master = _isMuted ? 0.0 : (_masterVolume / 100.0);
            _mashDeckA.SetVolumeExact(master * gA * _mashTrimA);
            _mashDeckB.SetVolumeExact(master * gB * _mashTrimB * duckFactor);

            if (_lblMashMixRatio != null)
            {
                int pctA = (int)Math.Round(gA * 100);
                int pctB = (int)Math.Round(gB * 100);
                string tag = _mashCrossfade <= 10 ? "DECK A SOLO" : (_mashCrossfade >= 90 ? "DECK B SOLO" : "DUAL MASHUP BLEND");
                _lblMashMixRatio.Text = string.Format("CROSSFADER • {0}% A / {1}% B ({2})", pctA, pctB, tag);
            }
        }

        private void UpdateMashupTimeLabels()
        {
            if (_lblMashTimeA != null)
            {
                int posA = _mashDeckA.GetPositionMs();
                int lenA = _mashDeckA.GetLengthMs();
                if (lenA <= 0 && _mashAnalA != null) lenA = _mashAnalA.DurationMs;
                string dropA = (_mashAnalA != null && _mashAnalA.IsReady) ? FormatTime(_mashAnalA.DropCueMs) : "--:--";
                _lblMashTimeA.Text = string.Format("{0} / {1}  •  DROP @ {2}", FormatTime(posA), FormatTime(lenA), dropA);
                if (_mashWaveA != null && lenA > 0)
                {
                    _mashWaveA.Progress = Math.Max(0f, Math.Min(1f, (float)posA / lenA));
                    _mashWaveA.Invalidate();
                }
            }
            if (_lblMashTimeB != null)
            {
                int posB = _mashDeckB.GetPositionMs();
                int lenB = _mashDeckB.GetLengthMs();
                if (lenB <= 0 && _mashAnalB != null) lenB = _mashAnalB.DurationMs;
                string dropB = (_mashAnalB != null && _mashAnalB.IsReady) ? FormatTime(_mashAnalB.DropCueMs) : "--:--";
                _lblMashTimeB.Text = string.Format("{0} / {1}  •  DROP @ {2}", FormatTime(posB), FormatTime(lenB), dropB);
                if (_mashWaveB != null && lenB > 0)
                {
                    _mashWaveB.Progress = Math.Max(0f, Math.Min(1f, (float)posB / lenB));
                    _mashWaveB.Invalidate();
                }
            }
        }

        private void ExportMashupWav()
        {
            if (_mashTrackA == null || string.IsNullOrEmpty(_mashTrackA.Url) || _mashTrackB == null || string.IsNullOrEmpty(_mashTrackB.Url))
            {
                if (_lblMashStatus != null) _lblMashStatus.Text = "LOAD BOTH DECKS FIRST";
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "WAV Studio Audio (*.wav)|*.wav";
                string safeA = Regex.Replace(_mashTrackA.Title ?? "DeckA", @"[^\w\- ]", "").Trim();
                string safeB = Regex.Replace(_mashTrackB.Title ?? "DeckB", @"[^\w\- ]", "").Trim();
                if (safeA.Length > 18) safeA = safeA.Substring(0, 18);
                if (safeB.Length > 18) safeB = safeB.Substring(0, 18);
                sfd.FileName = string.Format("Dexplay_Mashup_{0}_x_{1}.wav", safeA, safeB);

                if (sfd.ShowDialog() != DialogResult.OK) return;

                string outPath = sfd.FileName;
                string pathA = _mashTrackA.Url;
                string pathB = _mashTrackB.Url;
                int startA = (_mashAnalA != null && _mashAnalA.IsReady) ? _mashAnalA.IntroCueMs : 0;
                int startB = (_mashAnalB != null && _mashAnalB.IsReady) ? _mashAnalB.IntroCueMs : 0;
                double sA = _mashSpeedA;
                double sB = _mashSpeedB;
                int blendSec = _mashBlendSec;
                CrossfadeCurve curve = _mashCurve;
                double tA = _mashTrimA;
                double tB = _mashTrimB;
                bool duck = _mashDucking;

                if (_btnMashExport != null) { _btnMashExport.Enabled = false; _btnMashExport.Text = "RENDERING..."; }
                if (_lblMashStatus != null) _lblMashStatus.Text = "MIXING STUDIO .WAV...";

                ThreadPool.QueueUserWorkItem(state =>
                {
                    string msg;
                    bool ok = MashupDsp.ExportStudioMashupWav(pathA, pathB, sA, sB, startA, startB, blendSec, curve, tA, tB, duck, outPath, out msg);
                    SafeUi(() =>
                    {
                        if (_btnMashExport != null) { _btnMashExport.Enabled = true; _btnMashExport.Text = "EXPORT .WAV"; }
                        if (_lblMashStatus != null) _lblMashStatus.Text = ok ? "EXPORTED READY!" : "EXPORT FAILED";
                        if (ok)
                        {
                            TrackItem exported = CreateLocalTrackItem(outPath);
                            exported.Title = Path.GetFileNameWithoutExtension(outPath);
                            exported.Artist = "Dexplay Mashup Studio";
                            _queue.Insert(0, exported);
                            RefreshQueueUI();
                        }
                    });
                });
            }
        }

        // --- TAB 4: STUDIO (CRYSTAL GLASS, THEMES, 5-BAND EQ & TEMPO) ---
        private void BuildTabEqualizer()
        {
            int contentW = EXPANDED_W - 32;
            int contentH = EXPANDED_H - COLLAPSED_H - 52;
            Panel p = new Panel
            {
                Location = new System.Drawing.Point(16, 44),
                Size = new System.Drawing.Size(contentW, contentH),
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };

            // 1. TOP CARD: THEME PALETTE & GLASS CLARITY
            _cardTheme = new GlassCard
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(contentW, 96),
                CornerRadius = 13
            };

            _lblSkinTitle = new Label
            {
                Location = new System.Drawing.Point(14, 10),
                Size = new System.Drawing.Size(contentW - 28, 16),
                Text = "CRYSTAL THEME & GLASS CLARITY",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _cmbSkinSelector = new DarkSelect
            {
                Location = new System.Drawing.Point(14, 30),
                Size = new System.Drawing.Size(contentW - 28, 30),
                Font = new System.Drawing.Font("Segoe UI", 8.8f),
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

            string[] clarityNames = { "GLASS: CRYSTAL (86%)", "GLASS: FROSTED (93%)", "GLASS: SOLID (100%)" };
            int clarW = (contentW - 40) / 3;
            for (int i = 0; i < 3; i++)
            {
                int clarIdx = i;
                GlassButton cb = new GlassButton
                {
                    Text = clarityNames[i],
                    IsPill = true,
                    IsActive = (_glassClarity == i),
                    Location = new System.Drawing.Point(14 + i * (clarW + 6), 65),
                    Size = new System.Drawing.Size(clarW, 23),
                    Font = new System.Drawing.Font("Segoe UI", 7.2f, FontStyle.Bold)
                };
                cb.Click += (s, e) => ApplyGlassClarity(clarIdx);
                _clarityButtons[i] = cb;
                _cardTheme.Controls.Add(cb);
            }

            _cardTheme.Controls.Add(_lblSkinTitle);
            _cardTheme.Controls.Add(_cmbSkinSelector);
            p.Controls.Add(_cardTheme);

            // 2. MIDDLE CARD: 5-BAND GRAPHIC EQUALIZER
            _cardEq = new GlassCard
            {
                Location = new System.Drawing.Point(0, 106),
                Size = new System.Drawing.Size(contentW, 224),
                CornerRadius = 13
            };

            _lblEqTitle = new Label
            {
                Location = new System.Drawing.Point(14, 14),
                Size = new System.Drawing.Size(96, 22),
                Text = "GRAPHIC EQ",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 8.2f, FontStyle.Bold),
                UseMnemonic = false
            };

            _cmbEqPreset = new DarkSelect
            {
                Location = new System.Drawing.Point(116, 10),
                Size = new System.Drawing.Size(232, 28),
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

            _btnEqBypass = new GlassButton
            {
                Text = _eqActive ? "EQ: ON" : "EQ: BYPASS",
                IsPill = true,
                IsActive = _eqActive,
                Location = new System.Drawing.Point(358, 10),
                Size = new System.Drawing.Size(102, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold)
            };
            _btnEqBypass.Click += (s, e) => ToggleEqualizerBypass();

            _btnEqReset = new GlassButton
            {
                Text = "RESET",
                IsPill = true,
                Location = new System.Drawing.Point(468, 10),
                Size = new System.Drawing.Size(86, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold)
            };
            _btnEqReset.Click += (s, e) => ResetEqualizer();

            _cardEq.Controls.Add(_lblEqTitle);
            _cardEq.Controls.Add(_cmbEqPreset);
            _cardEq.Controls.Add(_btnEqBypass);
            _cardEq.Controls.Add(_btnEqReset);

            string[] bandLabels = { "60 Hz", "250 Hz", "1 kHz", "4 kHz", "12 kHz" };
            string[] bandDescs = { "Sub-Bass", "Bass", "Mid", "Presence", "Treble" };
            int faderStartY = 50;
            int faderRowH = 34;

            for (int i = 0; i < 5; i++)
            {
                int bandIdx = i;
                int yPos = faderStartY + i * faderRowH;

                Label lblBand = new Label
                {
                    Location = new System.Drawing.Point(14, yPos + 2),
                    Size = new System.Drawing.Size(124, 20),
                    Text = string.Format("{0} ({1})", bandLabels[i], bandDescs[i]),
                    BackColor = System.Drawing.Color.Transparent,
                    ForeColor = _textMuted,
                    Font = new System.Drawing.Font("Consolas", 8.2f, FontStyle.Bold),
                    UseMnemonic = false
                };

                DarkSlider slider = new DarkSlider
                {
                    Location = new System.Drawing.Point(144, yPos),
                    Size = new System.Drawing.Size(contentW - 216, 22),
                    Minimum = -12,
                    Maximum = 12,
                    Value = _eqBands[i],
                    IsCenterZero = true,
                    AccentColor = _accent,
                    SecondaryColor = _secondaryAccent
                };

                Label lblVal = new Label
                {
                    Location = new System.Drawing.Point(contentW - 68, yPos + 2),
                    Size = new System.Drawing.Size(54, 20),
                    Text = FormatDb(_eqBands[i]),
                    BackColor = System.Drawing.Color.Transparent,
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
                        _cmbEqPreset.SetSelectedIndexSilent(_eqPresets.Count - 1);
                        _eqPresetIndex = _eqPresets.Count - 1;
                        _isInternalEqUpdate = false;
                    }
                    UpdateEqualizerDsp();
                    SaveCurrentConfig();
                };

                _lblEqBands[i] = lblBand;
                _eqSliders[i] = slider;
                _lblEqValues[i] = lblVal;

                _cardEq.Controls.Add(lblBand);
                _cardEq.Controls.Add(slider);
                _cardEq.Controls.Add(lblVal);
            }
            p.Controls.Add(_cardEq);

            // 3. BOTTOM CARD: ACOUSTICS, AUTOMATION & PLAYBACK TEMPO
            _cardEnhance = new GlassCard
            {
                Location = new System.Drawing.Point(0, 340),
                Size = new System.Drawing.Size(contentW, contentH - 344),
                CornerRadius = 13
            };

            _lblEnhanceTitle = new Label
            {
                Location = new System.Drawing.Point(14, 12),
                Size = new System.Drawing.Size(260, 16),
                Text = "ACOUSTIC BOOST & AUTOMATION",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _btnBassBoost = new GlassButton
            {
                Text = _bassBoostActive ? "BASS: +6 dB" : "BASS: OFF",
                IsPill = true,
                IsActive = _bassBoostActive,
                Location = new System.Drawing.Point(14, 38),
                Size = new System.Drawing.Size(96, 34),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnBassBoost.Click += (s, e) => ToggleBassBoost();

            _btnAutoNext = new GlassButton
            {
                Text = _autoNext ? "AUTO-NEXT: ON" : "AUTO-NEXT: OFF",
                IsPill = true,
                IsActive = _autoNext,
                Location = new System.Drawing.Point(116, 38),
                Size = new System.Drawing.Size(110, 34),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnAutoNext.Click += (s, e) => ToggleAutoNext();

            bool autoStartOn = IsAutoStartEnabled();
            _btnAutoStart = new GlassButton
            {
                Text = autoStartOn ? "AUTOSTART: ON" : "AUTOSTART: OFF",
                IsPill = true,
                IsActive = autoStartOn,
                Location = new System.Drawing.Point(232, 38),
                Size = new System.Drawing.Size(112, 34),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnAutoStart.Click += (s, e) => ToggleAutoStart();

            int tempoX = 358;
            int tempoW = contentW - tempoX - 14;

            _lblTempoTitle = new Label
            {
                Location = new System.Drawing.Point(tempoX, 12),
                Size = new System.Drawing.Size(tempoW, 16),
                Text = "PLAYBACK TEMPO",
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _accent,
                Font = new System.Drawing.Font("Segoe UI", 7.8f, FontStyle.Bold),
                UseMnemonic = false
            };

            _sliderTempo = new DarkSlider
            {
                Location = new System.Drawing.Point(tempoX, 44),
                Size = new System.Drawing.Size(tempoW - 92, 22),
                Minimum = 50,
                Maximum = 150,
                Value = (int)(_tempoSpeed * 100),
                AccentColor = _accent,
                SecondaryColor = _secondaryAccent
            };

            _lblTempoVal = new Label
            {
                Location = new System.Drawing.Point(tempoX + tempoW - 88, 44),
                Size = new System.Drawing.Size(42, 22),
                Text = string.Format("{0}%", (int)(_tempoSpeed * 100)),
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = _tempoSpeed == 1.0 ? _textMuted : _accent,
                Font = new System.Drawing.Font("Consolas", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };

            _btnTempoReset = new GlassButton
            {
                Text = "1.0x",
                IsPill = true,
                Location = new System.Drawing.Point(tempoX + tempoW - 44, 41),
                Size = new System.Drawing.Size(44, 28),
                Font = new System.Drawing.Font("Segoe UI", 7.6f, FontStyle.Bold)
            };
            _btnTempoReset.Click += (s, e) => SetPlaybackTempo(1.0);

            _sliderTempo.ValueChanged += (s, e) =>
            {
                double speed = _sliderTempo.Value / 100.0;
                _tempoSpeed = speed;
                _player.SetSpeedRatio(speed);
                _lblTempoVal.Text = string.Format("{0}%", _sliderTempo.Value);
                _lblTempoVal.ForeColor = _sliderTempo.Value == 100 ? _textMuted : _accent;
                UpdateDeckInfoCard();
                SaveCurrentConfig();
            };

            _cardEnhance.Controls.Add(_lblEnhanceTitle);
            _cardEnhance.Controls.Add(_btnBassBoost);
            _cardEnhance.Controls.Add(_btnAutoNext);
            _cardEnhance.Controls.Add(_btnAutoStart);
            _cardEnhance.Controls.Add(_lblTempoTitle);
            _cardEnhance.Controls.Add(_sliderTempo);
            _cardEnhance.Controls.Add(_lblTempoVal);
            _cardEnhance.Controls.Add(_btnTempoReset);

            p.Controls.Add(_cardEnhance);

            _tabPanels[5] = p;
            _deckPanel.Controls.Add(p);
        }

        private void SwitchTab(int tabIndex)
        {
            _currentTab = tabIndex;
            if (_segmentedBar != null) _segmentedBar.SelectedIndex = tabIndex;
            for (int i = 0; i < 6; i++)
            {
                if (_tabPanels[i] != null) _tabPanels[i].Visible = (i == tabIndex);
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
            if (_headerPanel != null) _headerPanel.Invalidate();

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
            bool mashPlaying = _mashDeckA.IsPlaying || _mashDeckB.IsPlaying || _mashAutoBlending;

            int desiredInterval = (playing || mashPlaying) ? 40 : 250;
            if (_tickTimer.Interval != desiredInterval) _tickTimer.Interval = desiredInterval;

            if (playing || _wasPlayingLastTick)
            {
                _miniEq.IsPlaying = playing || mashPlaying;
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
            }
            else if (mashPlaying)
            {
                _miniEq.IsPlaying = true;
                _miniEq.UpdateFrame();
            }
            _wasPlayingLastTick = playing || mashPlaying;

            UpdateAmbientAura(playing || mashPlaying);

            if (playing || _isExpanded)
            {
                UpdateHeaderTitleTicker();
            }

            if (mashPlaying)
            {
                if (_mashAutoBlending)
                {
                    double elapsed = (DateTime.UtcNow - _mashBlendStartUtc).TotalSeconds;
                    double t = Math.Max(0.0, Math.Min(1.0, elapsed / Math.Max(1.0, _mashBlendSec)));
                    int nextVal = (int)Math.Round(_mashBlendStartVal + (_mashBlendTargetVal - _mashBlendStartVal) * t);
                    if (nextVal != _mashCrossfade)
                    {
                        _mashInternalUpdate = true;
                        _mashCrossfade = nextVal;
                        if (_sliderMashCrossfader != null) _sliderMashCrossfader.Value = _mashCrossfade;
                        _mashInternalUpdate = false;
                    }
                    if (t >= 1.0)
                    {
                        _mashAutoBlending = false;
                        if (_lblMashStatus != null) _lblMashStatus.Text = "BLEND COMPLETE";
                    }
                }
                UpdateMashupMix();
                if (_isExpanded && _currentTab == 4)
                {
                    UpdateMashupTimeLabels();
                }
            }

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
                if (len > 0)
                {
                    float nextPct = Math.Max(0f, Math.Min(1f, (float)pos / len));
                    if (Math.Abs(nextPct - _headerProgressPct) > 0.004f)
                    {
                        _headerProgressPct = nextPct;
                        if (_headerPanel != null) _headerPanel.Invalidate(new Rectangle(0, _headerPanel.Height - 4, _headerPanel.Width, 4));
                    }
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
            if (_tickerCounter % 8 != 0) return;

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
                if (_auraAlpha >= 145) { _auraAlpha = 145; _auraDirection = -1; }
                else if (_auraAlpha <= 55) { _auraAlpha = 55; _auraDirection = 1; }
            }
            else
            {
                _auraAlpha = 48;
            }

            if (_auraAlpha != _lastAuraAlpha && (_tickerCounter % 6 == 0 || !playing))
            {
                _lastAuraAlpha = _auraAlpha;
                this.Invalidate(false);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            GlassGfx.ApplyCrisp(g);

            // Crystal glass vertical gradient backdrop
            System.Drawing.Color topBg = GlassGfx.Blend(_bg, _surface, 0.45f);
            System.Drawing.Color botBg = GlassGfx.Blend(_bg, System.Drawing.Color.Black, 0.12f);
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(this.ClientRectangle, topBg, botBg, 90f))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Subtle upper crystal glass sheen
            Rectangle sheenRect = new Rectangle(0, 0, this.Width, Math.Min(110, Math.Max(24, this.Height / 2)));
            using (LinearGradientBrush sheen = new LinearGradientBrush(sheenRect,
                System.Drawing.Color.FromArgb(24, 255, 255, 255),
                System.Drawing.Color.FromArgb(0, 255, 255, 255), 90f))
            {
                g.FillRectangle(sheen, sheenRect);
            }

            // Soft top-left accent refraction
            Rectangle glowRect = new Rectangle(0, 0, Math.Max(120, this.Width / 2), Math.Max(46, this.Height / 3));
            using (LinearGradientBrush glow = new LinearGradientBrush(glowRect,
                System.Drawing.Color.FromArgb(22, _accent),
                System.Drawing.Color.FromArgb(0, _accent), 35f))
            {
                g.FillRectangle(glow, glowRect);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            GlassGfx.ApplyCrisp(e.Graphics);
            int r = _isIsland ? 24 : 18;
            RectangleF bounds = new RectangleF(0.5f, 0.5f, this.Width - 1.5f, this.Height - 1.5f);
            using (GraphicsPath path = GlassGfx.CreateRoundRect(bounds, r))
            {
                // Crisp outer crystal rim (luminous white highlight fading to accent aura)
                using (LinearGradientBrush rimBrush = new LinearGradientBrush(this.ClientRectangle,
                    System.Drawing.Color.FromArgb(Math.Min(210, _auraAlpha + 95), 255, 255, 255),
                    System.Drawing.Color.FromArgb(_auraAlpha, _accent), 90f))
                using (System.Drawing.Pen auraPen = new System.Drawing.Pen(rimBrush, 1.35f))
                {
                    e.Graphics.DrawPath(auraPen, path);
                }
            }
        }

        private void ApplyGlassClarity(int clarity, bool save = true)
        {
            _glassClarity = Math.Max(0, Math.Min(2, clarity));
            double[] opacities = { 0.86, 0.93, 1.00 };
            string[] shortLabels = { "GLASS 86%", "GLASS 93%", "SOLID" };
            try { this.Opacity = opacities[_glassClarity]; } catch { }

            if (_btnQuickClarity != null)
            {
                _btnQuickClarity.Text = shortLabels[_glassClarity];
                _btnQuickClarity.IsActive = (_glassClarity < 2);
                _btnQuickClarity.Invalidate();
            }
            if (_clarityButtons != null)
            {
                for (int i = 0; i < _clarityButtons.Length; i++)
                {
                    if (_clarityButtons[i] != null)
                    {
                        _clarityButtons[i].IsActive = (i == _glassClarity);
                        _clarityButtons[i].Invalidate();
                    }
                }
            }
            if (save) SaveCurrentConfig();
        }

        public void CycleGlassClarity()
        {
            ApplyGlassClarity((_glassClarity + 1) % 3, true);
        }

        private void LoadSavedConfig()
        {
            try
            {
                WidgetConfig cfg = WidgetConfig.Load();
                _masterVolume = Math.Max(0, Math.Min(100, cfg.MasterVolume));
                _isMuted = (_masterVolume == 0);
                if (_deckVolumeBar != null) _deckVolumeBar.Value = _masterVolume;
                if (_lblVolPct != null) _lblVolPct.Text = _masterVolume + "%";
                if (_btnDeckVol != null) { _btnDeckVol.IsActive = _isMuted; _btnDeckVol.Invalidate(); }

                _autoNext = cfg.AutoNext;
                _eqActive = cfg.EqActive;
                _eqPresetIndex = cfg.EqPresetIndex;
                _bassBoostActive = cfg.BassBoostActive;
                if (cfg.EqBands != null && cfg.EqBands.Length == 5) _eqBands = cfg.EqBands;
                if (_btnAutoNext != null)
                {
                    _btnAutoNext.Text = _autoNext ? "AUTO-NEXT: ON" : "AUTO-NEXT: OFF";
                    _btnAutoNext.IsActive = _autoNext;
                    _btnAutoNext.Invalidate();
                }
                _isLooping = cfg.IsLooping;
                if (_btnLoop != null)
                {
                    _btnLoop.IsActive = _isLooping;
                    _btnLoop.Text = _isLooping ? "LOOP: ON" : "LOOP: OFF";
                    _btnLoop.Invalidate();
                }
                _isIsland = cfg.IsIsland;
                _isPinned = cfg.IsPinned;
                this.TopMost = _isPinned;
                if (_btnHeaderPin != null) { _btnHeaderPin.IsActive = _isPinned; _btnHeaderPin.Invalidate(); }
                if (_btnHeaderIsland != null) { _btnHeaderIsland.IsActive = _isIsland; _btnHeaderIsland.Invalidate(); }
                int y = _isIsland ? 36 : 0;
                this.Location = new System.Drawing.Point(this.Location.X, y);
                ApplyRoundedRegion();

                ApplyGlassClarity(cfg.GlassClarity, false);

                if (cfg.SkinIndex >= 0 && cfg.SkinIndex < _skins.Count)
                {
                    ApplySkin(cfg.SkinIndex, false);
                }
                else
                {
                    ApplySkin(0, false);
                }

                if (_visualizer != null && cfg.VisualizerMode >= 0 && cfg.VisualizerMode <= 3)
                {
                    _visualizer.SetMode((VisualizerMode)cfg.VisualizerMode);
                }
                UpdateEqualizerDsp();

                if (_queue == null) _queue = new List<TrackItem>();
                _queue.Clear();
                if (cfg.HasSavedQueue && cfg.Queue != null)
                {
                    _queue.AddRange(cfg.Queue);
                }

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
                    if (!cfg.HasSavedQueue && _queue.Count == 0)
                    {
                        _queue.Add(_currentTrack);
                    }
                    RefreshQueueUI();
                    LoadTrack(_currentTrack, false);
                }
                else
                {
                    _currentTrack = (_queue.Count > 0) ? _queue[0] : CreateEmptyTrack();
                    RefreshQueueUI();
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
                cfg.GlassClarity = _glassClarity;
                cfg.AutoNext = _autoNext;
                cfg.EqActive = _eqActive;
                cfg.EqPresetIndex = _eqPresetIndex;
                cfg.BassBoostActive = _bassBoostActive;
                cfg.EqBands = _eqBands;
                cfg.IsLooping = _isLooping;
                cfg.IsIsland = _isIsland;
                cfg.IsPinned = _isPinned;
                if (_currentTrack != null && _currentTrack.Source != "NONE" && !string.IsNullOrEmpty(_currentTrack.Title) && _currentTrack.Title != "No Track Loaded")
                {
                    cfg.LastTitle = _currentTrack.Title ?? "";
                    cfg.LastArtist = _currentTrack.Artist ?? "";
                    cfg.LastVideoId = _currentTrack.VideoId ?? "";
                    cfg.LastUrl = _currentTrack.Url ?? "";
                    cfg.LastDurationMs = _currentTrack.DurationMs;
                    cfg.LastGenre = _currentTrack.Genre ?? "YT MUSIC • Official Audio";
                    cfg.LastSource = _currentTrack.Source ?? "YT_MUSIC";
                }
                else
                {
                    cfg.LastTitle = "";
                    cfg.LastArtist = "";
                    cfg.LastVideoId = "";
                    cfg.LastUrl = "";
                    cfg.LastDurationMs = 0;
                    cfg.LastGenre = "";
                    cfg.LastSource = "";
                }
                cfg.Queue = _queue != null ? new List<TrackItem>(_queue) : new List<TrackItem>();
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
            if (track == null) track = CreateEmptyTrack();
            _currentTrack = track;
            _pendingPlay = playImmediate;
            _headerProgressPct = 0f;
            int loadId = Interlocked.Increment(ref _currentLoadId);

            _player.Stop();
            if (playImmediate) StopAllMashupDecks();

            bool isEmptyTrack = (track.Source == "NONE") ||
                (string.IsNullOrEmpty(track.VideoId) && string.IsNullOrEmpty(track.Url) && (string.IsNullOrEmpty(track.Title) || track.Title == "No Track Loaded"));

            if (_scrubBar != null)
            {
                _scrubBar.Value = 0;
                _scrubBar.Maximum = Math.Max(1000, track.DurationMs > 0 ? track.DurationMs : 200000);
            }
            if (_lblTimeCur != null) _lblTimeCur.Text = "0:00";
            if (_lblTimeTot != null) _lblTimeTot.Text = isEmptyTrack ? "0:00" : FormatTime(track.DurationMs > 0 ? track.DurationMs : 200000);

            _activeLyricIndex = -1;
            if (_lyrics != null) _lyrics.Clear();
            if (_lstLyrics != null)
            {
                _lstLyrics.Items.Clear();
                _lstLyrics.Items.Add(isEmptyTrack ? "Search or play a track to load synced lyrics" : "Loading synced lyrics...");
            }
            UpdateDeckLyricPreview();
            if (_lstQueue != null) _lstQueue.Invalidate();

            _headerScrollOffset = 0;
            _headerScrollPause = 20;
            _lastHeaderText = "";
            SetHeaderLabelText(isEmptyTrack ? "Dexplay — Ready" : (track.Artist + " — " + track.Title), true);
            _lblTrackTitle.Text = track.Title;
            _lblTrackArtist.Text = track.Artist;
            if (_lblSourceBadge != null)
            {
                _lblSourceBadge.Text = isEmptyTrack ? "READY" : ((track.Source == "LOCAL") ? track.Genre.ToUpper() : "YT MUSIC • OFFICIAL AUDIO");
            }
            UpdateDeckInfoCard();

            UpdatePlayButtonUI(false);
            _player.SetSpeedRatio(_tempoSpeed);
            SaveCurrentConfig();

            if (isEmptyTrack)
            {
                _isDownloadingAudio = false;
                _pendingPlay = false;
                return;
            }

            ThreadPool.QueueUserWorkItem(state => FetchLrcLyrics(track.Title, track.Artist, loadId));

            // Keep startup / passive restore completely silent until the user presses Play
            if (!playImmediate)
            {
                _isDownloadingAudio = false;
                return;
            }

            UpdatePlayButtonUI(true);

            bool isYoutube = !string.IsNullOrEmpty(track.VideoId);
            bool hasLocalFile = !string.IsNullOrEmpty(track.Url) && File.Exists(track.Url);

            if (isYoutube && !hasLocalFile)
            {
                track.Url = "";
                ExtractAndPlayYouTubeTrack(track, loadId);
            }
            else if (!string.IsNullOrEmpty(track.Url))
            {
                _isDownloadingAudio = false;
                _player.Play(track.Url);
                UpdateEqualizerDsp();
                UpdatePlayButtonUI(_player.IsPlaying);
            }
            else
            {
                _isDownloadingAudio = false;
                _lblTrackArtist.Text = "Searching audio stream...";
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

            if (_currentTrack == null || _currentTrack.Source == "NONE" ||
                (string.IsNullOrEmpty(_currentTrack.VideoId) && string.IsNullOrEmpty(_currentTrack.Url)))
            {
                OpenSearchTab();
                return;
            }

            if (!_player.HasOpenedMedia)
            {
                LoadTrack(_currentTrack, true);
                return;
            }

            if (string.IsNullOrEmpty(_currentTrack.Url) && !string.IsNullOrEmpty(_currentTrack.VideoId))
            {
                _pendingPlay = true;
                UpdatePlayButtonUI(true);
                ExtractAndPlayYouTubeTrack(_currentTrack, _currentLoadId);
                return;
            }

            if (!_player.IsPlaying && (_mashDeckA.IsPlaying || _mashDeckB.IsPlaying))
            {
                StopAllMashupDecks();
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
                _btnEqBypass.IsActive = _eqActive;
                _btnEqBypass.Invalidate();
            }
            UpdateEqualizerDsp();
            SaveCurrentConfig();
        }

        public void ResetEqualizer()
        {
            ApplyEqualizerPreset(0);
        }

        public void ToggleBassBoost()
        {
            _bassBoostActive = !_bassBoostActive;
            if (_btnBassBoost != null)
            {
                _btnBassBoost.Text = _bassBoostActive ? "BASS: +6 dB" : "BASS: OFF";
                _btnBassBoost.IsActive = _bassBoostActive;
                _btnBassBoost.Invalidate();
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
                _btnAutoNext.IsActive = _autoNext;
                _btnAutoNext.Invalidate();
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
            UpdateMashupMix();
            UpdateDeckInfoCard();
        }

        private void UpdateDeckInfoCard()
        {
            if (_lblDeckQuality != null)
            {
                string presetName = (_eqPresets != null && _eqPresetIndex >= 0 && _eqPresetIndex < _eqPresets.Count)
                    ? _eqPresets[_eqPresetIndex].Name
                    : "Custom";
                _lblDeckQuality.Text = string.Format("EQ: {0} • Bass: {1} • Tempo: {2}%",
                    _eqActive ? presetName : "Bypass",
                    _bassBoostActive ? "+6 dB" : "OFF",
                    (int)(_tempoSpeed * 100));
            }
        }

        private void UpdateDeckLyricPreview()
        {
            if (_lblDeckLyricCur == null || _lblDeckLyricNext == null) return;
            if (_lyrics != null && _lyrics.Count > 0)
            {
                int idx = (_activeLyricIndex >= 0 && _activeLyricIndex < _lyrics.Count) ? _activeLyricIndex : 0;
                _lblDeckLyricCur.Text = _lyrics[idx].Text;
                _lblDeckLyricNext.Text = (idx + 1 < _lyrics.Count) ? _lyrics[idx + 1].Text : "End of synced lyrics";
            }
            else
            {
                _lblDeckLyricCur.Text = "No synced lyrics loaded for this track";
                _lblDeckLyricNext.Text = "Tip: Drag & drop local MP3, FLAC, WAV, or M4A anywhere";
            }
        }

        private void RegisterGlobalHotkeys()
        {
            try
            {
                Win32.RegisterHotKey(this.Handle, 1, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Space);
                Win32.RegisterHotKey(this.Handle, 2, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Right);
                Win32.RegisterHotKey(this.Handle, 3, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Left);
                Win32.RegisterHotKey(this.Handle, 4, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.E);
                Win32.RegisterHotKey(this.Handle, 5, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.T);
                Win32.RegisterHotKey(this.Handle, 6, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.B);
                Win32.RegisterHotKey(this.Handle, 7, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, (int)Keys.Up);
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
            try { _player.Stop(); _player.Dispose(); } catch { }
            try { _mashDeckA.Stop(); _mashDeckA.Dispose(); } catch { }
            try { _mashDeckB.Stop(); _mashDeckB.Dispose(); } catch { }
            try { if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); _trayIcon = null; } } catch { }
            try { if (_toolTip != null) { _toolTip.Dispose(); _toolTip = null; } } catch { }
            try { _fontBadge.Dispose(); _fontListTime.Dispose(); _fontListRegular.Dispose(); _fontListSub.Dispose(); _fontListBold.Dispose(); _fontListItalic.Dispose(); } catch { }
            base.OnFormClosing(e);
            Application.Exit();
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
                    case 4: SwitchTab(5); if (!_isExpanded) ToggleExpand(); break;
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

        private void StyleGlassCard(GlassCard card, System.Drawing.Color surface, System.Drawing.Color accent)
        {
            if (card == null) return;
            card.TopColor = GlassGfx.Blend(surface, System.Drawing.Color.White, 0.08f);
            card.BottomColor = GlassGfx.Blend(surface, System.Drawing.Color.Black, 0.14f);
            card.BorderColor = System.Drawing.Color.FromArgb(42, 255, 255, 255);
            card.TopHighlightColor = System.Drawing.Color.FromArgb(105, 255, 255, 255);
            card.AccentGlowColor = System.Drawing.Color.FromArgb(26, accent);
            card.Invalidate();
        }

        private void StyleGlassButton(GlassButton btn, System.Drawing.Color surface, System.Drawing.Color accent, System.Drawing.Color secondary, System.Drawing.Color muted)
        {
            if (btn == null) return;
            btn.AccentColor = accent;
            btn.SecondaryColor = secondary;
            btn.IdleTopColor = GlassGfx.Blend(surface, System.Drawing.Color.White, 0.10f);
            btn.IdleBottomColor = GlassGfx.Blend(surface, System.Drawing.Color.Black, 0.08f);
            btn.IdleTextColor = GlassGfx.Blend(muted, System.Drawing.Color.White, 0.45f);
            btn.Invalidate();
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
                _secondaryAccent = s.SecondaryAccent;
                _bg = s.Background;
                _surface = s.Surface;
                _textMuted = s.TextMuted;

                this.BackColor = _bg;
                StyleGlassCard(_deckCard, _surface, _accent);
                StyleGlassCard(_deckInfoCard, _surface, _accent);
                StyleGlassCard(_cardTheme, _surface, _accent);
                StyleGlassCard(_cardEq, _surface, _accent);
                StyleGlassCard(_cardEnhance, _surface, _accent);
                StyleGlassCard(_lyricsHeaderCard, _surface, _accent);
                StyleGlassCard(_queueHeaderCard, _surface, _accent);
                StyleGlassCard(_mashTopCard, _surface, _accent);
                StyleGlassCard(_mashCardA, _surface, _accent);
                StyleGlassCard(_mashCardB, _surface, _secondaryAccent);
                StyleGlassCard(_mashMixerCard, _surface, _accent);

                if (_searchBox != null) _searchBox.SyncColors(_surface, _accent);
                if (_lblSearchStatus != null) _lblSearchStatus.ForeColor = _textMuted;
                if (_lstSearchResults != null) { _lstSearchResults.BackColor = _bg; _lstSearchResults.Invalidate(); }
                if (_lstLyrics != null) { _lstLyrics.BackColor = _bg; _lstLyrics.Invalidate(); }
                if (_lstQueue != null) { _lstQueue.BackColor = _bg; _lstQueue.Invalidate(); }

                if (_lblDeckEngine != null) _lblDeckEngine.ForeColor = _accent;
                if (_lblDeckDetails != null) _lblDeckDetails.ForeColor = _accent;
                if (_lblDeckQuality != null) _lblDeckQuality.ForeColor = _textMuted;
                if (_lblDeckLyricNext != null) _lblDeckLyricNext.ForeColor = _textMuted;
                if (_lblTrackArtist != null) _lblTrackArtist.ForeColor = _textMuted;
                if (_lblSourceBadge != null) _lblSourceBadge.ForeColor = _accent;
                if (_lblTimeCur != null) _lblTimeCur.ForeColor = _textMuted;
                if (_lblTimeTot != null) _lblTimeTot.ForeColor = _textMuted;
                if (_lblVolPct != null) _lblVolPct.ForeColor = _textMuted;

                if (_lblSkinTitle != null) _lblSkinTitle.ForeColor = _accent;
                if (_lblEqTitle != null) _lblEqTitle.ForeColor = _accent;
                if (_lblEnhanceTitle != null) _lblEnhanceTitle.ForeColor = _accent;
                if (_lblTempoTitle != null) _lblTempoTitle.ForeColor = _accent;
                if (_lblLyricsHeader != null) _lblLyricsHeader.ForeColor = _accent;
                if (_lblQueueHeader != null) _lblQueueHeader.ForeColor = _textMuted;
                if (_lblEqBands != null) { foreach (var lb in _lblEqBands) { if (lb != null) lb.ForeColor = _textMuted; } }

                if (_miniEq != null) { _miniEq.AccentColor = _accent; _miniEq.SecondaryColor = _secondaryAccent; _miniEq.Invalidate(); }
                if (_visualizer != null) { _visualizer.AccentColor = _accent; _visualizer.SecondaryColor = _secondaryAccent; _visualizer.Invalidate(); }
                if (_btnHeaderPlay != null) { _btnHeaderPlay.CircleBgColor = _accent; _btnHeaderPlay.SecondaryColor = _secondaryAccent; _btnHeaderPlay.Invalidate(); }
                if (_btnHeaderSearch != null) { _btnHeaderSearch.NormalColor = _accent; _btnHeaderSearch.Invalidate(); }
                if (_btnHeaderExpand != null) { _btnHeaderExpand.NormalColor = _accent; _btnHeaderExpand.Invalidate(); }
                if (_btnHeaderIsland != null) { _btnHeaderIsland.ActiveColor = _accent; _btnHeaderIsland.Invalidate(); }
                if (_btnHeaderPin != null) { _btnHeaderPin.ActiveColor = _accent; _btnHeaderPin.Invalidate(); }
                if (_btnDeckPlay != null) { _btnDeckPlay.CircleBgColor = _accent; _btnDeckPlay.SecondaryColor = _secondaryAccent; _btnDeckPlay.Invalidate(); }
                if (_btnDeckVol != null) { _btnDeckVol.NormalColor = _textMuted; _btnDeckVol.HoverColor = _accent; _btnDeckVol.Invalidate(); }
                if (_scrubBar != null) { _scrubBar.AccentColor = _accent; _scrubBar.SecondaryColor = _secondaryAccent; _scrubBar.Invalidate(); }
                if (_deckVolumeBar != null) { _deckVolumeBar.AccentColor = _accent; _deckVolumeBar.SecondaryColor = _secondaryAccent; _deckVolumeBar.Invalidate(); }
                if (_vinylDisc != null) { _vinylDisc.AccentColor = _accent; _vinylDisc.SecondaryColor = _secondaryAccent; _vinylDisc.Invalidate(); }

                if (_segmentedBar != null)
                {
                    _segmentedBar.AccentColor = _accent;
                    _segmentedBar.SecondaryColor = _secondaryAccent;
                    _segmentedBar.ContainerTop = GlassGfx.Blend(_surface, System.Drawing.Color.White, 0.06f);
                    _segmentedBar.ContainerBottom = GlassGfx.Blend(_surface, System.Drawing.Color.Black, 0.14f);
                    _segmentedBar.Invalidate();
                }

                if (_btnQuickSkin != null) _btnQuickSkin.Text = "THEME " + (skinIdx + 1);
                StyleGlassButton(_btnQuickSkin, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnQuickClarity, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnLoop, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnOpenStudio, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnSearchExec, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnClearQueue, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnAddFile, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnEqBypass, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnEqReset, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnBassBoost, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnAutoNext, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnAutoStart, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnTempoReset, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashSync, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashCurve, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashDuck, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashExport, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashFileA, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashFileB, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashPlayA, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashPlayB, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashCueA, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashCueB, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashPanA, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashPanB, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashDur, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashBlendAB, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashBlendBA, _surface, _accent, _secondaryAccent, _textMuted);
                StyleGlassButton(_btnMashStopAll, _surface, _accent, _secondaryAccent, _textMuted);
                if (_mashWaveA != null) { _mashWaveA.AccentColor = _accent; _mashWaveA.SecondaryColor = _secondaryAccent; _mashWaveA.Invalidate(); }
                if (_mashWaveB != null) { _mashWaveB.AccentColor = _secondaryAccent; _mashWaveB.SecondaryColor = _accent; _mashWaveB.Invalidate(); }
                if (_sliderMashTrimA != null) { _sliderMashTrimA.AccentColor = _accent; _sliderMashTrimA.SecondaryColor = _secondaryAccent; _sliderMashTrimA.Invalidate(); }
                if (_sliderMashTrimB != null) { _sliderMashTrimB.AccentColor = _secondaryAccent; _sliderMashTrimB.SecondaryColor = _accent; _sliderMashTrimB.Invalidate(); }
                if (_sliderMashCrossfader != null) { _sliderMashCrossfader.AccentColor = _accent; _sliderMashCrossfader.SecondaryColor = _secondaryAccent; _sliderMashCrossfader.Invalidate(); }
                if (_cmbMashA != null) _cmbMashA.AccentColor = _accent;
                if (_cmbMashB != null) _cmbMashB.AccentColor = _secondaryAccent;
                if (_clarityButtons != null)
                {
                    foreach (var cb in _clarityButtons) StyleGlassButton(cb, _surface, _accent, _secondaryAccent, _textMuted);
                }

                if (_cmbSkinSelector != null)
                {
                    _cmbSkinSelector.AccentColor = _accent;
                    _cmbSkinSelector.SetSelectedIndexSilent(skinIdx);
                }
                if (_cmbEqPreset != null) _cmbEqPreset.AccentColor = _accent;

                UpdateAutoStartUI();
                if (_eqSliders != null)
                {
                    foreach (var sl in _eqSliders)
                    {
                        if (sl != null) { sl.AccentColor = _accent; sl.SecondaryColor = _secondaryAccent; sl.Invalidate(); }
                    }
                }
                if (_sliderTempo != null)
                {
                    _sliderTempo.AccentColor = _accent;
                    _sliderTempo.SecondaryColor = _secondaryAccent;
                    _sliderTempo.Invalidate();
                }

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
                    UpdateDeckLyricPreview();
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
                    UpdateDeckLyricPreview();
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
                                UpdateDeckLyricPreview();
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
                _lstLyrics.Items.Add("No synced lyrics found for this track.");
                UpdateDeckLyricPreview();
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
                UpdateDeckLyricPreview();
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

        private void RemoveQueueTrackAt(int idx)
        {
            if (_queue == null || idx < 0 || idx >= _queue.Count) return;
            TrackItem removed = _queue[idx];
            bool wasCurrent = (_currentTrack != null && _currentTrack.VideoId == removed.VideoId && _currentTrack.Title == removed.Title);
            bool wasPlaying = _player.IsPlaying;

            _queue.RemoveAt(idx);
            _queueHoverRemoveIdx = -1;
            RefreshQueueUI();

            if (wasCurrent)
            {
                if (_queue.Count > 0)
                {
                    int nextIdx = Math.Min(idx, _queue.Count - 1);
                    LoadTrack(_queue[nextIdx], wasPlaying);
                }
                else
                {
                    LoadTrack(CreateEmptyTrack(), false);
                }
            }
            else
            {
                SaveCurrentConfig();
            }
        }

        private void ClearQueue(bool keepCurrent)
        {
            if (_queue == null || _queue.Count == 0) return;
            if (keepCurrent && _currentTrack != null)
            {
                TrackItem active = _queue.Find(t => t.VideoId == _currentTrack.VideoId && t.Title == _currentTrack.Title);
                _queue.Clear();
                if (active != null) _queue.Add(active);
                _queueHoverRemoveIdx = -1;
                RefreshQueueUI();
                SaveCurrentConfig();
            }
            else
            {
                _queue.Clear();
                _queueHoverRemoveIdx = -1;
                RefreshQueueUI();
                LoadTrack(CreateEmptyTrack(), false);
            }
        }

        private void RefreshQueueUI()
        {
            if (_lstQueue == null) return;
            _lstQueue.Items.Clear();
            if (_queue.Count == 0)
            {
                _lstQueue.Items.Add("Queue is empty — search tracks or drop audio files here");
            }
            else
            {
                for (int i = 0; i < _queue.Count; i++)
                {
                    _lstQueue.Items.Add(_queue[i].Title);
                }
            }
            if (_lblQueueHeader != null)
            {
                _lblQueueHeader.Text = string.Format("QUEUE ({0}) • Click × or Del to remove", _queue.Count);
            }
            if (_btnClearQueue != null)
            {
                _btnClearQueue.Enabled = (_queue.Count > 0);
                _btnClearQueue.Visible = (_queue.Count > 0);
            }
            RefreshMashupQueueSelectors();
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
