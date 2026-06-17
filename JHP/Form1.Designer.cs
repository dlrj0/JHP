using JHP.Controls;
using Microsoft.Web.WebView2.WinForms;

namespace JHP
{
    partial class Form1
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webView?.Dispose();
                timer?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ── 타이머 ──
        private System.Windows.Forms.Timer? timer;

        // ── 타이틀바 ──
        private Panel          pnlTitleBar  = null!;
        private Label          lblNextAlarm = null!;
        private ControlButton  btnClose     = null!;
        private ControlButton  btnMaximize  = null!;
        private ControlButton  btnMinimize  = null!;
        private ComboBox       cmbSite      = null!;
        private CheckBox       cbTopMost    = null!;
        private CheckBox       cbHideBorder = null!;

        // ── 메인 영역 ──
        private SplitContainer splitMain = null!;
        private WebView2       webView   = null!;
        private Panel          pnlAlarm  = null!;

        // ── 알람 패널 컨트롤 ──
        private readonly CheckBox[]       alarmChecks   = new CheckBox[8];
        private readonly TextBox[]        customNames   = new TextBox[3];
        private readonly NumericUpDown[]  customTicks   = new NumericUpDown[3];
        private readonly CheckBox[]       customEnabled = new CheckBox[3];
        private ComboBox  cmbAlarmFile    = null!;
        private NSlider   sliderVolume    = null!;
        private Label     lblVolumeValue  = null!;
        private CheckBox  cbTts           = null!;
        private NSlider   sliderRate      = null!;
        private Button    btnAlarmSettings = null!;

        private void InitializeComponent()
        {
            SuspendLayout();

            // 폼 기본 속성만 설정
            // 모든 컨트롤 생성 및 배치는 Form1.cs 의 InitControls() 에서 처리
            AutoScaleMode   = AutoScaleMode.Font;
            BackColor       = Color.FromArgb(20, 20, 20);
            ClientSize      = new Size(1100, 700);
            MinimumSize     = new Size(700, 450);
            FormBorderStyle = FormBorderStyle.None;
            Text            = "JHP";

            ResumeLayout(false);
        }
    }
}
