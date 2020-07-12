using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;
using AudioSwitcher.AudioApi;

namespace JVP
{
    public partial class MainGUI : Form
    {

        private bool isPlaying;
        private bool durationSet;
        private string fileName;
        private CoreAudioDevice audioDevice;
        Action<DeviceVolumeChangedArgs> onNext;
       

        private double volume { get; set; }

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
            onNext = delegate (DeviceVolumeChangedArgs args) { this.Invoke(new MethodInvoker(()=> barVolume.Value = (int)audioDevice.Volume)); };

            IDisposable subscriber = ObservableExtensions.Subscribe<DeviceVolumeChangedArgs>(audioDevice.VolumeChanged, new Action<DeviceVolumeChangedArgs> (onNext));
           
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

        private void btnForward_MouseDown(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.fastForward();
        }

        private void btnForward_MouseUp(object sender, MouseEventArgs e)
        {
            wmPlayer.Ctlcontrols.play();
        }

        private void mnuOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fd = new OpenFileDialog())
            {
                fd.Filter = "Video files (*.mp4, *.avi)|*.mp4;*.avi|All files (*.*)|*.*";

                if(fd.ShowDialog() == DialogResult.OK)
                {
                    fileName = fd.FileName;
                    wmPlayer.URL = fileName;
                    timer1.Start();
                }
            }
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void mnuChapters_Click(object sender, EventArgs e)
        {

        }

        private void barVolume_Scroll(object sender, EventArgs e)
        {
            volume = barVolume.Value;
            audioDevice.Volume = volume;
        }      

        private void wmPlayer_ClickEvent(object sender, AxWMPLib._WMPOCXEvents_ClickEvent e)
        {
            btnPlay_Click(null, null);
        }

        private void setDuration()
        {
            if(!durationSet && wmPlayer.currentMedia != null && wmPlayer.currentMedia.duration > 0)
            {
                sbProgress.Max = (int)wmPlayer.currentMedia.duration;
                lblTotal.Text = wmPlayer.currentMedia.durationString;              
                if (sbProgress.Max > 0) durationSet = true;
            } 
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            setDuration();
            if(sbProgress.Max > 0)
            {
                sbProgress.Value = (int)wmPlayer.Ctlcontrols.currentPosition;
                sbProgress.SelLength = sbProgress.Value;
                lblElapsed.Text = string.IsNullOrWhiteSpace(wmPlayer.Ctlcontrols.currentPositionString) ? "00:00:00" : wmPlayer.Ctlcontrols.currentPositionString;
            }
            //else if(wmPlayer.currentMedia != null)
            //{
            //    sbProgress.Max = (int)wmPlayer.currentMedia.duration;
            //    lblTotal.Text = wmPlayer.currentMedia.durationString;
            //}
        }
    }
}
