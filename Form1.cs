using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Nxun.Fetion;

namespace FXTester
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FetionSender fx = new FetionSender(this.textBox1.Text, this.textBox2.Text);
            string strId, strPic;
            int status;
            while ((status = fx.Initialize()) == 421 || status == 420)
            {
                fx.GetVerifyPic(out strId, out strPic);
                using (VerifyForm verifyForm = new VerifyForm(strPic))
                {
                    verifyForm.ShowDialog();
                    fx.Verify(strId, verifyForm.PicText);
                }
            }
            if (status != 200)
            {
                if (status == 401)
                    MessageBox.Show("密码错误!");
                else
                    MessageBox.Show("错误码:" + status);
                return;
            }
            while ((status = fx.SendMessage(this.textBox3.Text, this.textBox4.Text)) == 421 || status == 420)
            {
                fx.GetVerifyPic(out strId, out strPic);
                using (VerifyForm verifyForm = new VerifyForm(strPic))
                {
                    verifyForm.ShowDialog();
                    fx.Verify(strId, verifyForm.PicText);
                }
            }
            if (status == 280)
            {
                MessageBox.Show("发送成功");
            }
            else
            {
                MessageBox.Show("错误码:" + status);
            }
        }
    }
}
