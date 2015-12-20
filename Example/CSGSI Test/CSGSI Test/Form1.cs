using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Media;
using System.Runtime.InteropServices;
using System.Net;
using CSGSI;

namespace CSGSI_Test
{
    public partial class Form1 : Form
    {
        /*
            Get started: 
            - Add: http://pastebin.com/d18hNB6D to Counter-Strike Global Offensive\csgo\cfg\gamestate_integration_test.cfg
            - Add: using CSGSI;
            - Download Newtonsoft.Json from NuGet
            - For event handeler: 
                    CSGOSharp.NewGameState += new CSGOSharp.NewGameStateHandler(onNewgameState);
                    private void onNewgameState(){}
            - To start: CSGOSharp.Start();
            - To stop: CSGOSharp.Stop();
        */
        public Form1()
        {
            InitializeComponent();

            
            backgroundWorker1.RunWorkerAsync();
            timer.Start();
            
            CSGOSharp.NewGameState += new CSGOSharp.NewGameStateHandler(onNewgameState);
            
        }
        static bool beep = true, beep10 = false;
        List<string> buffer = new List<string>();
            
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            SystemSounds.Beep.Play();
            beep = !beep;
        }

        private void onNewgameState()
        {


        }

        private void timerBeep_Tick(object sender, EventArgs e)
        {
            if (labelKit.Text == "RUN" && CSGOSharp.server.round.bomb == "planted" && beep)
            {
                SystemSounds.Beep.Play();
                timerBeep.Start();
            }
            
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            // obsolete because of 0.5 - 4 sec delay on bomb timer
            if (CSGOSharp.planted)
            {
                foreach (Timers item in CSGOSharp.timers)
                {
                    if (CSGOSharp.server.map.mode == item.name)
                    {

                        progressBarTime.Maximum = item.bomb;
                    }
                }
                try { progressBarTime.Value = CSGOSharp.sec; } catch (Exception) { }
                if (CSGOSharp.sec  == 10 && !beep10 && false)
                {
                    SystemSounds.Beep.Play();
                    beep10 = true;
                }
                else
                {
                    beep10 = false;
                }
            }
            else
            {
                progressBarTime.Value = 0;
                timerBeep.Stop();
            }
            try
            {
                if (!buffer.SequenceEqual(CSGOSharp.playerNames) && CSGOSharp.listLoaded)
                {
                    buffer.Clear();
                    listBox1.Items.Clear();
                    foreach (string item in CSGOSharp.playerNames)
                    {
                        listBox1.Items.Add(item);
                        buffer.Add(item);
                    }
                }
            }
            catch (Exception)
            {

            }
            if (!timerBeep.Enabled)
            {
                timerBeep_Tick(null, null);
            }
            

            label1.Text = CSGOSharp.dumpSelectedBuffer;
            label2.Text = CSGOSharp.dumpCurrentPLayer;
            label3.Text = CSGOSharp.dumpServerStats;
            labelTime.Text = CSGOSharp.timeString;
            labelKit.Text = CSGOSharp.kitornot;
        }

        

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            CSGOSharp.dumpPlayerIndex = (sender as ListBox).SelectedIndex;
        }

        

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CSGOSharp.Stop();
            Application.ExitThread();
            Environment.Exit(0);
        }

        
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        private void Form1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            CSGOSharp.Start(IPAddress.Loopback,3000);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void labelKit_Click(object sender, EventArgs e)
        {
            CSGOSharp.Stop();
        }

    }

}
