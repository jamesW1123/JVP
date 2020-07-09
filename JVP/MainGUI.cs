﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JVP
{
    public partial class MainGUI : Form
    {

        private bool isPlaying;

        public MainGUI()
        {
            InitializeComponent();

            isPlaying = false;
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

        }

        private void btnForward_Click(object sender, EventArgs e)
        {

        }

        private void mnuOpen_Click(object sender, EventArgs e)
        {

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

        }
    }
}
