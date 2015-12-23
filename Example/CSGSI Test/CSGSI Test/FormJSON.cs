using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSGSI_Test
{
    public partial class FormJSON: Form
    {
        public FormJSON(Form1 orig)
        {
            InitializeComponent();
            this.orig = orig;
            timer1.Start();
            this.MouseWheel += FormJSON_MouseWheel;
        }

        Form1 orig;

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
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
        int scroll = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            string buf = orig.JSONString;
            if (scroll > 0)
            {
                for (int i = 0; i < scroll; i++)
                {
                    buf = buf.Remove(0, buf.IndexOf("\n")+1);
                    buf = buf.Remove(0, buf.IndexOf("\n")+1);
                }
            }
            else
            {

            }

            
            label1.Text = buf;
        }

        private void FormJSON_MouseWheel(object sender, MouseEventArgs e)
        {
           
            if (e.Delta > 0 && scroll >=0)
            {
                scroll--;
            }
            else
            {
                scroll++;
            }
        }
    }
}
