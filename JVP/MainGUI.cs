﻿using System;
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
            fileName = "";

            onNext = delegate (DeviceVolumeChangedArgs args) { changeVolume(); };

            IDisposable subscriber = ObservableExtensions.Subscribe<DeviceVolumeChangedArgs>(audioDevice.VolumeChanged, new Action<DeviceVolumeChangedArgs> (onNext));
            
        }

        void changeVolume()
        {
            Console.Out.WriteLine(audioDevice.Volume);
            //volume = audioDevice.Volume;
            //barVolume.Value = (int)volume;
        }
       

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (isPlaying) wmPlayer.Ctlcontrols.pause();                
            else wmPlayer.Ctlcontrols.play();
            isPlaying = !isPlaying;
        }

        private void btnRewind_Click(object sender, EventArgs e)
        {

        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            wmPlayer.Ctlcontrols.stop();
            isPlaying = false;
        }

        private void btnForward_Click(object sender, EventArgs e)
        {

        }

        private void mnuOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fd = new OpenFileDialog())
            {
                fd.Filter = "Video files (*.mp4)|*.mp4|All files (*.*)|*.*";

                if(fd.ShowDialog() == DialogResult.OK)
                {
                    fileName = fd.FileName;
                    wmPlayer.URL = fileName;
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
            
        }

        private void pbProgress_Click(object sender, EventArgs e)
        {

        }

        private void wmPlayer_ClickEvent(object sender, AxWMPLib._WMPOCXEvents_ClickEvent e)
        {
            btnPlay_Click(null, null);
        }

      
    }
}
