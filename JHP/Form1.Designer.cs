namespace JHP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // 모든 컨트롤은 Form1.cs InitControls()에서 코드로 생성
            SuspendLayout();
            //
            // Form1
            //
            AutoScaleMode   = AutoScaleMode.Font;
            BackColor       = Color.FromArgb(20, 20, 20);
            ClientSize      = new Size(1100, 700);
            ForeColor       = Color.White;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize     = new Size(700, 450);
            Text            = "JHP";
            Load           += Form1_Load;
            ResumeLayout(false);
        }
    }
}
