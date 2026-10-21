using JLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace JVP
{
    public partial class MainGUI : Form
    {
        private BinaryTree<Chapter> chapters;
        private ChapterMarkerStrip chapterMarkers;
        private ContextMenuStrip chapterContextMenu;
        private ChapterStore chapterStore;
        // Content hash of the open video; null until a video is opened
        private string videoKey;
        // Set on a right mouse down so the jump on Click is skipped for that press
        private bool chapterItemRightClick;
        private bool durationSet;
        // True while the left button is held on the progress bar, so the timer leaves the bar alone
        private bool seeking;
        private int seekStartX;
        // Pixels the mouse may move between press and release and still count as a click
        private const int clickSlop = 3;
        private string fileName;
        private bool isPlaying;
        // Ask for a label when marking a chapter; meant to become a settings option later
        private bool promptForLabel = true;
        // Fast-forward and rewind speeds; each click on the button steps to the next one
        private static readonly int[] shuttleSpeeds = { 2, 4, 8 };
        // Index into shuttleSpeeds while fast-forwarding or rewinding, -1 at normal speed
        private int shuttleStep = -1;
        private bool rewinding;
        // Fastest rate the player reliably plays at; it silently drops back to normal speed above this
        private const int maxPlayRate = 4;
        // Rewind, and fast-forward above maxPlayRate, seek in steps while paused instead.
        // Most video formats can't play in reverse at all.
        private readonly Timer stepTimer = new Timer { Interval = 200 };

        public MainGUI()
        {
            InitializeComponent();
            // Chapter ticks sit in the thin spacer row just above the progress slider
            chapterMarkers = new ChapterMarkerStrip(sbProgress) { Dock = DockStyle.Fill, Margin = new Padding(0) };
            tableLayoutPanel1.RowStyles[7].Height = 7;
            tableLayoutPanel1.Controls.Add(chapterMarkers, 1, 7);
            tableLayoutPanel1.SetColumnSpan(chapterMarkers, 8);
            chapterMarkers.chapterClicked += c => { if (wmPlayer.currentMedia != null) jumpTo(c.value); };
            chapterMarkers.chapterRightClicked += showChapterContextMenu;
            chapterContextMenu = new ContextMenuStrip();
            chapterContextMenu.Items.Add("Delete", null, mnuDeleteChapter_Click);
            chapterContextMenu.Closed += chapterContextMenu_Closed;
            chapters = new BinaryTree<Chapter>();
            chapterStore = new ChapterStore();
            // The player keeps its own volume, separate from the Windows volume
            barVolume.Value = Math.Max(barVolume.Minimum, Math.Min(barVolume.Maximum, wmPlayer.settings.volume));
            isPlaying = false;
            durationSet = false;
            fileName = "";
            lblElapsed.Text = Chapter.formatTime(0);
            // A placeholder child makes Chapters a submenu; the real list is built on open
            mnuChapters.DropDownItems.Add("(no chapters)");
            mnuChapters.DropDownOpening += mnuChapters_DropDownOpening;
            stepTimer.Tick += stepTimer_Tick;
        }

        private void barVolume_MouseDown(object sender, MouseEventArgs e)
        {
            barVolume.Value = e.X;
            wmPlayer.settings.volume = barVolume.Value;
        }

        private void barVolume_Scroll(object sender, EventArgs e)
        {
            wmPlayer.settings.volume = barVolume.Value;
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            shuttle(false);
        }

        private void btnMarkChapter_Click(object sender, EventArgs e)
        {
            // Chapters are stored per video, so there's nothing to attach one to yet
            if (videoKey == null) return;
            int value = (int)wmPlayer.Ctlcontrols.currentPosition;
            if (chapters.search(new Chapter("", value)))
            {
                MessageBox.Show("A chapter is already marked at this time.", "Mark Chapter");
                return;
            }

            // Default number is where the new chapter falls in time order
            int index = 1;
            foreach (Chapter item in chapters.inorder())
            {
                if (item.value < value) index++;
            }
            string label = "Chapter " + index;

            if (promptForLabel)
            {
                // Check the player itself; isPlaying misses autostart after opening a file
                bool wasPlaying = wmPlayer.playState == WMPLib.WMPPlayState.wmppsPlaying;
                if (wasPlaying) wmPlayer.Ctlcontrols.pause();
                using (ChapterLabelDialog dlg = new ChapterLabelDialog(label))
                {
                    DialogResult result = dlg.ShowDialog(this);
                    if (wasPlaying) wmPlayer.Ctlcontrols.play();
                    if (result != DialogResult.OK) return;
                    if (dlg.label != "") label = dlg.label;
                }
            }

            chapters.insert(new Chapter(label, value));
            renumberChapters();
            updateChapterMarkers();
            saveChapters();
        }

        // Keep default "Chapter N" labels matching their position in time order; custom labels are left alone
        private void renumberChapters()
        {
            List<Chapter> temp = chapters.inorder();
            for (int i = 0; i < temp.Count; i++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(temp[i].label, @"^Chapter \d+$"))
                    temp[i].label = "Chapter " + (i + 1);
            }
        }

        // Shared by the marker strip and the Edit > Chapters list
        private void showChapterContextMenu(Chapter c, System.Drawing.Point screenPos)
        {
            ToolStripItem item = chapterContextMenu.Items[0];
            item.Text = "Delete " + c.label;
            item.Tag = c;
            chapterContextMenu.Show(screenPos);
        }

        private void mnuDeleteChapter_Click(object sender, EventArgs e)
        {
            chapters.delete((Chapter)((ToolStripItem)sender).Tag);
            renumberChapters();
            updateChapterMarkers();
            saveChapters();
        }

        // The Edit menu can't be left open once its right-click menu has been up (it stops closing
        // on outside clicks), so close every menu whether Delete was picked or not
        private void chapterContextMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            keepChapterMenuOpen(false);
            editToolStripMenuItem.DropDown.Close();
        }

        // Stops Edit > Chapters closing while its right-click menu is up, like a normal context menu
        private void keepChapterMenuOpen(bool keepOpen)
        {
            editToolStripMenuItem.DropDown.AutoClose = !keepOpen;
            mnuChapters.DropDown.AutoClose = !keepOpen;
        }

        // Ticks only make sense once the video's length is known
        private void updateChapterMarkers()
        {
            chapterMarkers.setChapters(chapters.inorder(), durationSet ? sbProgress.Max : 0);
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            // Play while fast-forwarding or rewinding goes back to normal speed rather than pausing
            if (shuttleStep >= 0)
            {
                stopShuttle();
                wmPlayer.Ctlcontrols.play();
                isPlaying = true;
                return;
            }
            if (isPlaying) wmPlayer.Ctlcontrols.pause();
            else wmPlayer.Ctlcontrols.play();
            isPlaying = !isPlaying;
        }

        private void btnRewind_Click(object sender, EventArgs e)
        {
            shuttle(true);
        }

        // Steps through the speeds in one direction, wrapping back to the slowest.
        // Switching direction starts again at the slowest speed.
        private void shuttle(bool rewind)
        {
            if (wmPlayer.currentMedia == null) return;
            shuttleStep = shuttleStep >= 0 && rewinding == rewind ? (shuttleStep + 1) % shuttleSpeeds.Length : 0;
            rewinding = rewind;
            int speed = shuttleSpeeds[shuttleStep];
            if (rewind || speed > maxPlayRate)
            {
                wmPlayer.settings.rate = 1;
                wmPlayer.Ctlcontrols.pause();
                stepTimer.Start();
            }
            else
            {
                stepTimer.Stop();
                wmPlayer.settings.rate = speed;
                wmPlayer.Ctlcontrols.play();
            }
            isPlaying = true;
            btnRewind.Text = rewind ? speed + "x" : "R";
            btnForward.Text = rewind ? "F" : speed + "x";
        }

        private void stopShuttle()
        {
            shuttleStep = -1;
            stepTimer.Stop();
            wmPlayer.settings.rate = 1;
            btnRewind.Text = "R";
            btnForward.Text = "F";
        }

        private void stepTimer_Tick(object sender, EventArgs e)
        {
            double step = shuttleSpeeds[shuttleStep] * stepTimer.Interval / 1000.0;
            double target = wmPlayer.Ctlcontrols.currentPosition + (rewinding ? -step : step);
            double end = wmPlayer.currentMedia.duration;
            if (target > 0 && target < end)
            {
                wmPlayer.Ctlcontrols.currentPosition = target;
                return;
            }
            // Reached the start or end: carry on playing from there at normal speed
            stopShuttle();
            wmPlayer.Ctlcontrols.currentPosition = rewinding ? 0 : Math.Max(0, end - 1);
            wmPlayer.Ctlcontrols.play();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            stopShuttle();
            wmPlayer.Ctlcontrols.stop();
            isPlaying = false;
        }

        private void mnuChapters_Click(object sender, EventArgs e)
        {
        }

        // Rebuild the chapter list each time it opens so newly marked chapters show up
        private void mnuChapters_DropDownOpening(object sender, EventArgs e)
        {
            chapterItemRightClick = false;
            keepChapterMenuOpen(false);
            mnuChapters.DropDownItems.Clear();
            List<Chapter> temp = chapters.inorder();
            if (temp.Count == 0)
            {
                mnuChapters.DropDownItems.Add(new ToolStripMenuItem("(no chapters)") { Enabled = false });
                return;
            }
            bool hasMedia = wmPlayer.currentMedia != null;
            foreach (Chapter item in temp)
            {
                ToolStripMenuItem mnuItem = new ToolStripMenuItem($"{item.label}  ({item.displayTime})");
                mnuItem.Tag = item;
                mnuItem.Enabled = hasMedia;
                mnuItem.Click += mnuChapterItem_Click;
                mnuItem.MouseDown += mnuChapterItem_MouseDown;
                mnuItem.MouseUp += mnuChapterItem_MouseUp;
                mnuChapters.DropDownItems.Add(mnuItem);
            }
        }

        private void mnuChapterItem_MouseDown(object sender, MouseEventArgs e)
        {
            chapterItemRightClick = e.Button == MouseButtons.Right;
            // Set before the button comes up, or the right-click would close the menus
            if (chapterItemRightClick) keepChapterMenuOpen(true);
        }

        private void mnuChapterItem_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            showChapterContextMenu((Chapter)((ToolStripMenuItem)sender).Tag, Cursor.Position);
        }

        private void mnuChapterItem_Click(object sender, EventArgs e)
        {
            if (chapterItemRightClick)
            {
                chapterItemRightClick = false;
                return;
            }
            Chapter c = (Chapter)((ToolStripMenuItem)sender).Tag;
            jumpTo(c.value);
        }

        // Within the first 2 seconds of a chapter, go to the one before it instead of restarting it
        private void btnPrevChapter_Click(object sender, EventArgs e)
        {
            if (wmPlayer.currentMedia == null) return;
            double position = wmPlayer.Ctlcontrols.currentPosition;
            int target = 0;
            foreach (Chapter item in chapters.inorder())
            {
                if (item.value < position - 2) target = item.value;
                else break;
            }
            jumpTo(target);
        }

        private void btnNextChapter_Click(object sender, EventArgs e)
        {
            if (wmPlayer.currentMedia == null) return;
            double position = wmPlayer.Ctlcontrols.currentPosition;
            foreach (Chapter item in chapters.inorder())
            {
                if (item.value > position)
                {
                    jumpTo(item.value);
                    return;
                }
            }
        }

        private void jumpTo(int seconds)
        {
            wmPlayer.Ctlcontrols.currentPosition = seconds;
            if (sbProgress.Max > 0) sbProgress.Value = Math.Min(seconds, sbProgress.Max);
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void mnuOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fd = new OpenFileDialog())
            {
                fd.Filter = "Video files (*.mp4, *.avi)|*.mp4;*.avi|All files (*.*)|*.*";

                if (fd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        videoKey = ChapterStore.keyFor(fd.FileName);
                    }
                    catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                    {
                        MessageBox.Show($"Could not open {fd.FileName}: {ex.Message}", "Open");
                        return;
                    }
                    fileName = fd.FileName;
                    loadChapters();
                    // Re-read the length for the new file so the bar and markers scale to it
                    durationSet = false;
                    updateChapterMarkers();
                    stopShuttle();
                    wmPlayer.URL = fileName;
                    timer1.Start();
                }
            }
        }

        // Left button held on the progress bar: the bar follows the mouse and we seek once on release
        private void sbProgress_MouseDownEvent(object sender, Axmscomctl.ISliderEvents_MouseDownEvent e)
        {
            if (e.button != 1) return;
            seeking = true;
            seekStartX = Cursor.Position.X;
        }

        private void sbProgress_MouseUpEvent(object sender, Axmscomctl.ISliderEvents_MouseUpEvent e)
        {
            if (!seeking) return;
            seeking = false;
            if (wmPlayer.currentMedia == null || sbProgress.Max <= 0) return;
            // A press that barely moved is a click: go to the clicked spot rather than the slider's page step
            Point at = Cursor.Position;
            bool click = Math.Abs(at.X - seekStartX) <= clickSlop;
            jumpTo(click ? chapterMarkers.secondsAt(at, sbProgress.Max) : sbProgress.Value);
        }

        private void sbProgress_Scroll(object sender, EventArgs e)
        {
            // While dragging only preview the time; the seek happens on mouse up
            if (seeking) lblElapsed.Text = Chapter.formatTime(sbProgress.Value);
            else if (wmPlayer.currentMedia != null) wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        // Swap in the chapters saved for the video that was just opened
        private void loadChapters()
        {
            chapters = new BinaryTree<Chapter>();
            foreach (Chapter c in chapterStore.getChapters(videoKey))
            {
                chapters.insert(c);
            }
            renumberChapters();
        }

        private void saveChapters()
        {
            try
            {
                chapterStore.setChapters(videoKey, fileName, chapters.inorder());
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show($"Could not save chapters: {ex.Message}", "Chapters");
            }
        }

        private void setDuration()
        {
            if (!durationSet && wmPlayer.currentMedia != null && wmPlayer.currentMedia.duration > 0)
            {
                sbProgress.Max = (int)wmPlayer.currentMedia.duration;
                lblTotal.Text = wmPlayer.currentMedia.durationString;
                if (sbProgress.Max > 0) durationSet = true;
                updateChapterMarkers();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            setDuration();
            // Fast-forwarding into the end stops the video; drop back to normal speed for next time
            if (shuttleStep >= 0 && !rewinding && (wmPlayer.playState == WMPLib.WMPPlayState.wmppsStopped || wmPlayer.playState == WMPLib.WMPPlayState.wmppsMediaEnded))
            {
                stopShuttle();
                isPlaying = false;
            }
            if (sbProgress.Max > 0 && !seeking)
            {
                sbProgress.Value = (int)wmPlayer.Ctlcontrols.currentPosition;
                sbProgress.SelLength = sbProgress.Value;
                lblElapsed.Text = Chapter.formatTime((int)wmPlayer.Ctlcontrols.currentPosition);
            }
        }

        private void wmPlayer_ClickEvent(object sender, AxWMPLib._WMPOCXEvents_ClickEvent e)
        {
            btnPlay_Click(null, null);
        }
    }

    internal class Chapter : IComparable<Chapter>
    {
        //public int index { get; set; }
        public string label { get; set; }

        public int value { get; set; }

        public Chapter()
        {
            label = "";
            value = 0;
        }

        public Chapter(string label, int value)
        {
            this.label = label;
            this.value = value;
        }

        // Display only: hh:mm:ss when there are hours, otherwise mm:ss. The file still stores seconds.
        public string displayTime => formatTime(value);

        // Also used for the elapsed label, so it only grows an hours part on videos that have one
        public static string formatTime(int seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes:00}:{t.Seconds:00}";
        }

        public int CompareTo(Chapter other)
        {
            return value.CompareTo(other.value);
        }
    }
}