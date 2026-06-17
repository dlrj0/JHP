using JHP.Controls;
using Microsoft.Web.WebView2.WinForms;

namespace JHP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            ClientSize = new Size(1100, 700);
            MinimumSize = new Size(760, 480);
            FormBorderStyle = FormBorderStyle.None;
            Text = "JHP";
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
            Activated += Form1_Activated;
            Deactivate += Form1_Deactivate;
            ResumeLayout(false);
        }
    }
}
