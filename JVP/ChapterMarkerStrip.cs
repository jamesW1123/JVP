using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace JVP
{
    // Thin strip above the progress slider with a tick at each chapter. The slider is an ActiveX
    // control we can't paint on, so the strip lines its ticks up with the slider's track instead.
    internal class ChapterMarkerStrip : Control
    {
        private const int TBM_GETTHUMBRECT = 0x0400 + 25;
        private const int TBM_GETCHANNELRECT = 0x0400 + 26;
        // Used when the slider's inner trackbar window can't be found
        private const int fallbackInset = 12;

        private readonly Control slider;
        private List<Chapter> chapters = new List<Chapter>();
        private int duration;
        private readonly ToolTip tip = new ToolTip();
        private Chapter hovered;

        public Color tickColor { get; set; } = Color.OrangeRed;

        // Raised when a tick is clicked
        public event Action<Chapter> chapterClicked;
        // Raised when a tick is right-clicked, with the click's screen position
        public event Action<Chapter, Point> chapterRightClicked;

        public ChapterMarkerStrip(Control slider)
        {
            this.slider = slider;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            slider.Resize += (s, e) => Invalidate();
            slider.Move += (s, e) => Invalidate();
        }

        public void setChapters(List<Chapter> chapters, int duration)
        {
            this.chapters = chapters;
            this.duration = duration;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (duration <= 0) return;
            int left, right;
            getTrack(out left, out right);
            using (Brush b = new SolidBrush(tickColor))
            {
                foreach (Chapter item in chapters)
                {
                    if (item.value > duration) continue;
                    e.Graphics.FillRectangle(b, xFor(item.value, left, right) - 1, 0, 3, Height);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Chapter near = chapterAt(e.X);
            Cursor = near != null ? Cursors.Hand : Cursors.Default;
            if (near == hovered) return;
            hovered = near;
            if (near == null) tip.Hide(this);
            else tip.Show($"{near.label}  ({near.displayTime})", this, e.X + 10, -24);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Chapter near = chapterAt(e.X);
            if (near == null) return;
            if (e.Button == MouseButtons.Left) chapterClicked?.Invoke(near);
            else if (e.Button == MouseButtons.Right) chapterRightClicked?.Invoke(near, PointToScreen(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = null;
            tip.Hide(this);
        }

        // Nearest chapter within a few pixels of x, so a thin tick is easy to hover and click
        private Chapter chapterAt(int x)
        {
            if (duration <= 0) return null;
            int left, right;
            getTrack(out left, out right);
            Chapter best = null;
            int bestDist = 4;
            foreach (Chapter item in chapters)
            {
                if (item.value > duration) continue;
                int dist = Math.Abs(xFor(item.value, left, right) - x);
                if (dist <= bestDist)
                {
                    best = item;
                    bestDist = dist;
                }
            }
            return best;
        }

        private int xFor(int seconds, int left, int right)
        {
            return left + (int)Math.Round((right - left) * (double)seconds / duration);
        }

        // Where the thumb's centre travels, in this strip's coordinates
        private void getTrack(out int left, out int right)
        {
            IntPtr track = slider.IsHandleCreated ? findTrackbar(slider.Handle) : IntPtr.Zero;
            if (track != IntPtr.Zero)
            {
                RECT channel = new RECT(), thumb = new RECT();
                SendMessage(track, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);
                SendMessage(track, TBM_GETTHUMBRECT, IntPtr.Zero, ref thumb);
                int half = (thumb.right - thumb.left) / 2;
                POINT[] pts = { new POINT { x = channel.left + half }, new POINT { x = channel.right - half } };
                MapWindowPoints(track, Handle, pts, 2);
                if (pts[1].x > pts[0].x)
                {
                    left = pts[0].x;
                    right = pts[1].x;
                    return;
                }
            }
            Point origin = PointToClient(slider.PointToScreen(Point.Empty));
            left = origin.X + fallbackInset;
            right = origin.X + slider.Width - fallbackInset;
        }

        private static IntPtr findTrackbar(IntPtr parent)
        {
            if (isTrackbar(parent)) return parent;
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, (hwnd, l) =>
            {
                if (!isTrackbar(hwnd)) return true;
                found = hwnd;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private static bool isTrackbar(IntPtr hwnd)
        {
            StringBuilder name = new StringBuilder(64);
            GetClassName(hwnd, name, name.Capacity);
            return name.ToString().IndexOf("trackbar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        [DllImport("user32.dll")]
        private static extern int MapWindowPoints(IntPtr from, IntPtr to, [In, Out] POINT[] points, int count);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);
    }
}
