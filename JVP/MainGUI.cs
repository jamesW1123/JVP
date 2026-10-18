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
        private bool durationSet;
        private string fileName;
        private bool isPlaying;
        private double volume { get; set; }

        public MainGUI()
        {
            InitializeComponent();
            chapters = new BinaryTree<Chapter>();
            setChapters();
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
            string label = "Chapter " + (chapters.getSize() + 1);
            Chapter c = new Chapter(label, value);
            chapters.insert(c);
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
                mnuChapters.DropDownItems.Add(mnuItem);
            }
        }

        private void mnuChapterItem_Click(object sender, EventArgs e)
        {
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