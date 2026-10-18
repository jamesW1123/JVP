using JLib;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace JVP
{
    public partial class MainGUI : Form
    {
        private MMDevice audioDevice;
        private BinaryTree<Chapter> chapters;
        private ChapterMarkerStrip chapterMarkers;
        private ContextMenuStrip chapterContextMenu;
        // Set on a right mouse down so the jump on Click is skipped for that press
        private bool chapterItemRightClick;
        private bool durationSet;
        private string fileName;
        private bool isPlaying;
        // Ask for a label when marking a chapter; meant to become a settings option later
        private bool promptForLabel = true;
        private double volume { get; set; }

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
            setChapters();
            renumberChapters();
            updateChapterMarkers();
            List<Chapter> temp = chapters.inorder();
            foreach (Chapter item in temp)
            {
                Console.Out.WriteLine($"label: {item.label} value: {item.value}");
            }
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                audioDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            volume = Math.Round(audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            barVolume.Value = (int)volume;
            isPlaying = false;
            durationSet = false;
            fileName = "";
            lblElapsed.Text = "00:00:00";
            audioDevice.AudioEndpointVolume.OnVolumeNotification += audioDevice_VolumeChanged;
            // A placeholder child makes Chapters a submenu; the real list is built on open
            mnuChapters.DropDownItems.Add("(no chapters)");
            mnuChapters.DropDownOpening += mnuChapters_DropDownOpening;
        }

        // Raised on a Windows audio thread, so marshal back to the UI thread
        private void audioDevice_VolumeChanged(AudioVolumeNotificationData data)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new MethodInvoker(() => barVolume.Value = (int)Math.Round(data.MasterVolume * 100)));
        }

        private void setDeviceVolume()
        {
            audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(volume / 100);
        }

        private void barVolume_MouseDown(object sender, MouseEventArgs e)
        {
            barVolume.Value = e.X;
            volume = barVolume.Value;
            setDeviceVolume();
        }

        private void barVolume_Scroll(object sender, EventArgs e)
        {
            volume = barVolume.Value;
            setDeviceVolume();
        }

        private void btnForward_MouseDown(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.fastForward();
        }

        private void btnForward_MouseUp(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.play();
        }

        private void btnMarkChapter_Click(object sender, EventArgs e)
        {
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
            if (isPlaying) wmPlayer.Ctlcontrols.pause();
            else wmPlayer.Ctlcontrols.play();
            isPlaying = !isPlaying;
        }

        private void btnRewind_MouseDown(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.fastReverse();
        }

        private void btnRewind_MouseUp(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.play();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            wmPlayer.Ctlcontrols.stop();
            isPlaying = false;
        }

        private void MainGUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            audioDevice.AudioEndpointVolume.OnVolumeNotification -= audioDevice_VolumeChanged;
            audioDevice.Dispose();

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"D:\Repos\JVP\JVP\ref\chapters.txt"))
            {
                string line = "";
                List<Chapter> temp = chapters.inorder();
                foreach (Chapter item in temp)
                {
                    line = item.label + "~" + item.value;
                    file.WriteLine(line);
                }
            }
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
                    fileName = fd.FileName;
                    // Re-read the length for the new file so the bar and markers scale to it
                    durationSet = false;
                    updateChapterMarkers();
                    wmPlayer.URL = fileName;
                    timer1.Start();
                }
            }
        }

        private void sbProgress_MouseDownEvent(object sender, Axmscomctl.ISliderEvents_MouseDownEvent e)
        {
            //sbProgress.Value = e.x;
            Console.Out.WriteLine($"DOWN e.x: {e.x} sbProgress.Value: {sbProgress.Value} currentPosition: {wmPlayer.Ctlcontrols.currentPosition}");
            wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        private void sbProgress_MouseUpEvent(object sender, Axmscomctl.ISliderEvents_MouseUpEvent e)
        {
            //sbProgress.Value = e.x;
            //Console.Out.WriteLine($"UP e.x: {e.x} currentPosition: {wmPlayer.Ctlcontrols.currentPosition}");
            //wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        private void sbProgress_Scroll(object sender, EventArgs e)
        {
            wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        private void setChapters()
        {
            string[] lines = System.IO.File.ReadAllLines(@"D:\Repos\JVP\JVP\ref\chapters.txt");

            foreach (string s in lines)
            {
                if (String.IsNullOrWhiteSpace(s))
                    continue;
                string[] temp = s.Split('~');
                Chapter c = new Chapter(temp[0], Int32.Parse(temp[1]));
                chapters.insert(c);
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
            if (sbProgress.Max > 0)
            {
                sbProgress.Value = (int)wmPlayer.Ctlcontrols.currentPosition;
                sbProgress.SelLength = sbProgress.Value;
                lblElapsed.Text = string.IsNullOrWhiteSpace(wmPlayer.Ctlcontrols.currentPositionString) ? "00:00:00" : wmPlayer.Ctlcontrols.currentPositionString;
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
        public string displayTime
        {
            get
            {
                TimeSpan t = TimeSpan.FromSeconds(value);
                return t.TotalHours >= 1
                    ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
                    : $"{t.Minutes:00}:{t.Seconds:00}";
            }
        }

        public int CompareTo(Chapter other)
        {
            return value.CompareTo(other.value);
        }
    }
}