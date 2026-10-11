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
                webView?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.ToolTip toolTip;

        private Panel pnlTitleBar;
        private Button btnMenu;
        private Label lblTitle;
        private TimerButton timerButton;
        private Label lblVolumeValue;
        private Label lblOpacityValue;
        private NSlider sliderVolume;
        private NSlider sliderOpacity;
        private TextBox tbInlineEdit;
        private ControlButton btnMinimize;
        private ControlButton btnMaximize;
        private ControlButton btnClose;

        private WebView2 webView;

        private const int ResizeEdge = 5;
        private Panel pnlResizeLeft;
        private Panel pnlResizeRight;
        private Panel pnlResizeBottom;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            timer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            toolTip = new System.Windows.Forms.ToolTip(components);

            pnlTitleBar = new Panel();
            btnMenu = new Button();
            lblTitle = new Label();
            timerButton = new TimerButton();
            lblVolumeValue = new Label();
            lblOpacityValue = new Label();
            sliderVolume = new NSlider();
            sliderOpacity = new NSlider();
            tbInlineEdit = new TextBox();
            btnMinimize = new ControlButton();
            btnMaximize = new ControlButton();
            btnClose = new ControlButton();

            webView = new WebView2();

            SuspendLayout();

            // ===== 타이틀바 =====
            pnlTitleBar.Dock = DockStyle.Top;
            pnlTitleBar.Height = 36;
            pnlTitleBar.BackColor = Color.FromArgb(28, 28, 28);

            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(48, 9);
            lblTitle.Text = "JHP";

            // V버튼: 타이틀바 맨 왼쪽 — 사이트 목록 + 사이트 추가 + 항상위/테두리숨김을 드롭다운으로 흡수
            // (구버전의 pnlMenuBar/menuStrip, pnlSidebar/siteList 대체)
            btnMenu.Location = new Point(3, 4);
            btnMenu.Size = new Size(28, 28);
            btnMenu.BackColor = Color.FromArgb(28, 28, 28);
            btnMenu.FlatStyle = FlatStyle.Flat;
            btnMenu.FlatAppearance.BorderSize = 0;
            btnMenu.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            btnMenu.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 90, 145);
            btnMenu.ForeColor = Color.White;
            btnMenu.Font = new Font("Segoe UI", 9F);
            btnMenu.Text = "▼";
            btnMenu.Cursor = Cursors.Hand;
            btnMenu.UseVisualStyleBackColor = false;
            btnMenu.TabStop = false;

            // timerButton: lblNextAlarm + btnAlarmSettings 대체
            // 좌클릭=타이머 시작/정지, 우클릭=알람설정
            timerButton.Location = new Point(90, 3);
            timerButton.Size = new Size(30, 30);

            lblVolumeValue.AutoSize = true;
            lblVolumeValue.ForeColor = Color.LightGray;
            lblVolumeValue.Location = new Point(136, 10);
            lblVolumeValue.Text = "볼륨 50";
            lblVolumeValue.Cursor = Cursors.IBeam;

            sliderVolume.Location = new Point(194, 11);
            sliderVolume.Size = new Size(170, 20);
            sliderVolume.Minimum = 0;
            sliderVolume.Maximum = 100;

            lblOpacityValue.AutoSize = true;
            lblOpacityValue.ForeColor = Color.LightGray;
            lblOpacityValue.Location = new Point(378, 10);
            lblOpacityValue.Text = "투명도 100%";
            lblOpacityValue.Cursor = Cursors.IBeam;

            sliderOpacity.Location = new Point(466, 11);
            sliderOpacity.Size = new Size(170, 20);
            sliderOpacity.Minimum = 30;
            sliderOpacity.Maximum = 100;

            // 인라인 직접입력 TextBox (기본 숨김, 볼륨/투명도 숫자 클릭 시 표시)
            tbInlineEdit.Visible = false;
            tbInlineEdit.Size = new Size(60, 20);
            tbInlineEdit.BackColor = Color.FromArgb(50, 50, 50);
            tbInlineEdit.ForeColor = Color.White;
            tbInlineEdit.BorderStyle = BorderStyle.FixedSingle;

            // 우측 버튼 위치는 LayoutTitleBar()에서 계산 (Anchor는 패널 기본 폭 200 기준으로 계산되어 창 밖으로 밀려남)
            btnMinimize.Location = new Point(862, 3);
            btnMinimize.Type = ControlButton.ButtonType.Minimize;

            btnMaximize.Location = new Point(908, 3);
            btnMaximize.Type = ControlButton.ButtonType.Maximize;

            btnClose.Location = new Point(954, 3);
            btnClose.Type = ControlButton.ButtonType.Close;

            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(btnMenu);
            pnlTitleBar.Controls.Add(timerButton);
            pnlTitleBar.Controls.Add(lblVolumeValue);
            pnlTitleBar.Controls.Add(sliderVolume);
            pnlTitleBar.Controls.Add(lblOpacityValue);
            pnlTitleBar.Controls.Add(sliderOpacity);
            pnlTitleBar.Controls.Add(tbInlineEdit);
            pnlTitleBar.Controls.Add(btnMinimize);
            pnlTitleBar.Controls.Add(btnMaximize);
            pnlTitleBar.Controls.Add(btnClose);

            // ===== 웹뷰 =====
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(24, 24, 24);

            // ===== Form1 =====
            // DPI 배율(125%/150%/200%)에서 좌표·크기가 같이 커지도록 Dpi 모드 사용 (좌표는 96dpi 기준 값)
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(24, 24, 24);
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(760, 420);
            FormBorderStyle = FormBorderStyle.None;
            Text = "JHP";

            // pnlSidebar(Left)/pnlMenuBar(Top) 제거 — V버튼 드롭다운으로 흡수됨에 따라
            // Dock=Top 패널이 pnlTitleBar 하나만 남아 z-order 문제가 사라짐.
            // 리사이즈 핸들: webView가 클라이언트 영역을 빈틈없이 덮어 WM_NCHITTEST가 폼에 도달하지 못하므로,
            // 가장자리에 얇은 패널을 두고 MouseDown에서 SendMessage(WM_NCLBUTTONDOWN, HTxxx)로 리사이즈를 시작한다.
            pnlResizeLeft = new Panel { Dock = DockStyle.Left, Width = ResizeEdge, Cursor = Cursors.SizeWE, BackColor = Color.FromArgb(24, 24, 24) };
            pnlResizeRight = new Panel { Dock = DockStyle.Right, Width = ResizeEdge, Cursor = Cursors.SizeWE, BackColor = Color.FromArgb(24, 24, 24) };
            pnlResizeBottom = new Panel { Dock = DockStyle.Bottom, Height = ResizeEdge, BackColor = Color.FromArgb(24, 24, 24) };

            // Dock 처리 순서: 나중에 추가한 것부터 → 타이틀바(Top) → 하단 → 우 → 좌 → webView(Fill)
            Controls.Add(webView);
            Controls.Add(pnlResizeLeft);
            Controls.Add(pnlResizeRight);
            Controls.Add(pnlResizeBottom);
            Controls.Add(pnlTitleBar);

            ResumeLayout(false);
        }
    }
}
