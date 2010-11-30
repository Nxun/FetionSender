using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace FXTester
{
    public partial class VerifyForm : Form
    {
        public string PicText { set; get; }
        public VerifyForm(string strPic)
        {
            InitializeComponent();
            byte[] bs = Convert.FromBase64String(strPic);
            MemoryStream stream = new MemoryStream(bs);
            Bitmap bmp = new Bitmap(stream);
            this.pictureBox1.Image = bmp;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PicText = this.textBox1.Text;
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            //this.Close();
        }
    }
}
