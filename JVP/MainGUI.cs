using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace JVP
{
    public partial class MainGUI : Form
    {
        private CoreAudioDevice audioDevice;
        private bool durationSet;
        private string fileName;
        private bool isPlaying;
        private Action<DeviceVolumeChangedArgs> onNext;

        public MainGUI()
        {
            InitializeComponent();

            audioDevice = new CoreAudioController().DefaultPlaybackDevice;
            volume = audioDevice.Volume;
            barVolume.Value = (int)volume;
            isPlaying = false;
            durationSet = false;
            fileName = "";
            lblElapsed.Text = "00:00:00";
            onNext = delegate (DeviceVolumeChangedArgs args) { this.Invoke(new MethodInvoker(() => barVolume.Value = (int)audioDevice.Volume)); };

            IDisposable subscriber = ObservableExtensions.Subscribe<DeviceVolumeChangedArgs>(audioDevice.VolumeChanged, new Action<DeviceVolumeChangedArgs>(onNext));
        }

        private double volume { get; set; }

        private void barVolume_MouseDown(object sender, MouseEventArgs e)
        {
            barVolume.Value = e.X;
            volume = barVolume.Value;
            audioDevice.Volume = volume;
        }

        private void barVolume_Scroll(object sender, EventArgs e)
        {
            volume = barVolume.Value;
            audioDevice.Volume = volume;
        }

        private void btnForward_MouseDown(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.fastForward();
        }

        private void btnForward_MouseUp(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.play();
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

        private void sbProgress_MouseDownEvent(object sender, Axmscomctl.ISliderEvents_MouseDownEvent e)
        {
            sbProgress.Value = e.x;
            Console.Out.WriteLine($"DOWN e.x: {e.x} sbProgress.Value: {sbProgress.Value} currentPosition: {wmPlayer.Ctlcontrols.currentPosition}");
            wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        private void sbProgress_Scroll(object sender, EventArgs e)
        {
            //wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }

        private void sbProgress_MouseUpEvent(object sender, Axmscomctl.ISliderEvents_MouseUpEvent e)
        {
            //sbProgress.Value = e.x;
            //Console.Out.WriteLine($"UP e.x: {e.x} currentPosition: {wmPlayer.Ctlcontrols.currentPosition}");
            //wmPlayer.Ctlcontrols.currentPosition = sbProgress.Value;
        }
    }
}