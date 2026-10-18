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
        // Ask for a label when marking a chapter; meant to become a settings option later
        private bool promptForLabel = true;
        private double volume { get; set; }

        public MainGUI()
        {
            InitializeComponent();
            chapters = new BinaryTree<Chapter>();
            setChapters();
            renumberChapters();
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

        public int CompareTo(Chapter other)
        {
            return value.CompareTo(other.value);
        }
    }
}