using JHP.Api;
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

        private Panel pnlMenuBar;
        private MenuStrip menuStrip;
        private ToolStripMenuItem menuAddSite;
        private ToolStripMenuItem menuTopmost;
        private ToolStripMenuItem menuHideBorder;

        private Panel pnlSidebar;
        private SiteListViewControl siteList;
        private Panel pnlSidebarBottom;
        private Button btnAddSite;
        private Button btnRemoveSite;

        private WebView2 webView;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            timer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            toolTip = new System.Windows.Forms.ToolTip(components);

            pnlTitleBar = new Panel();
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

            pnlMenuBar = new Panel();
            menuStrip = new MenuStrip();
            menuAddSite = new ToolStripMenuItem();
            menuTopmost = new ToolStripMenuItem();
            menuHideBorder = new ToolStripMenuItem();

            pnlSidebar = new Panel();
            siteList = new SiteListViewControl();
            pnlSidebarBottom = new Panel();
            btnAddSite = new Button();
            btnRemoveSite = new Button();

            webView = new WebView2();

            SuspendLayout();

            // ===== 타이틀바 =====
            pnlTitleBar.Dock = DockStyle.Top;
            pnlTitleBar.Height = 36;
            pnlTitleBar.BackColor = Color.FromArgb(28, 28, 28);

            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(12, 9);
            lblTitle.Text = "JHP";

            // timerButton: lblNextAlarm + btnAlarmSettings 대체
            // 좌클릭=타이머 시작/정지, 우클릭=알람설정
            timerButton.Location = new Point(54, 3);
            timerButton.Size = new Size(30, 30);

            lblVolumeValue.AutoSize = true;
            lblVolumeValue.ForeColor = Color.LightGray;
            lblVolumeValue.Location = new Point(100, 10);
            lblVolumeValue.Text = "볼륨 50";
            lblVolumeValue.Cursor = Cursors.IBeam;

            sliderVolume.Location = new Point(158, 11);
            sliderVolume.Size = new Size(170, 20);
            sliderVolume.Minimum = 0;
            sliderVolume.Maximum = 100;

            lblOpacityValue.AutoSize = true;
            lblOpacityValue.ForeColor = Color.LightGray;
            lblOpacityValue.Location = new Point(342, 10);
            lblOpacityValue.Text = "투명도 100%";
            lblOpacityValue.Cursor = Cursors.IBeam;

            sliderOpacity.Location = new Point(430, 11);
            sliderOpacity.Size = new Size(170, 20);
            sliderOpacity.Minimum = 30;
            sliderOpacity.Maximum = 100;

            // 인라인 직접입력 TextBox (기본 숨김, 볼륨/투명도 숫자 클릭 시 표시)
            tbInlineEdit.Visible = false;
            tbInlineEdit.Size = new Size(60, 20);
            tbInlineEdit.BackColor = Color.FromArgb(50, 50, 50);
            tbInlineEdit.ForeColor = Color.White;
            tbInlineEdit.BorderStyle = BorderStyle.FixedSingle;

            btnMinimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMinimize.Location = new Point(862, 3);
            btnMinimize.Type = ControlButton.ButtonType.Minimize;

            btnMaximize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMaximize.Location = new Point(908, 3);
            btnMaximize.Type = ControlButton.ButtonType.Maximize;

            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(954, 3);
            btnClose.Type = ControlButton.ButtonType.Close;

            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(timerButton);
            pnlTitleBar.Controls.Add(lblVolumeValue);
            pnlTitleBar.Controls.Add(sliderVolume);
            pnlTitleBar.Controls.Add(lblOpacityValue);
            pnlTitleBar.Controls.Add(sliderOpacity);
            pnlTitleBar.Controls.Add(tbInlineEdit);
            pnlTitleBar.Controls.Add(btnMinimize);
            pnlTitleBar.Controls.Add(btnMaximize);
            pnlTitleBar.Controls.Add(btnClose);

            // ===== 메뉴바 =====
            pnlMenuBar.Dock = DockStyle.Top;
            pnlMenuBar.Height = 28;
            pnlMenuBar.BackColor = Color.FromArgb(40, 40, 40);

            menuAddSite.Text = "사이트 추가";
            menuAddSite.Tag = ToolStripCommand.ADD_SITE;

            menuTopmost.Text = "항상 위";
            menuTopmost.CheckOnClick = true;
            menuTopmost.Tag = ToolStripCommand.TOPMOST;

            menuHideBorder.Text = "테두리 숨김 (포커스 아웃)";
            menuHideBorder.CheckOnClick = true;
            menuHideBorder.Tag = ToolStripCommand.TOGGLE_HIDE_WINDOW_BORDER;

            menuStrip.BackColor = Color.FromArgb(40, 40, 40);
            menuStrip.ForeColor = Color.White;
            menuStrip.Dock = DockStyle.Fill;
            menuStrip.GripStyle = ToolStripGripStyle.Hidden;
            menuStrip.Items.Add(menuAddSite);
            menuStrip.Items.Add(menuTopmost);
            menuStrip.Items.Add(menuHideBorder);

            pnlMenuBar.Controls.Add(menuStrip);

            // ===== 사이드바 =====
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.Width = 180;
            pnlSidebar.BackColor = Color.FromArgb(30, 30, 30);

            siteList.Dock = DockStyle.Fill;

            pnlSidebarBottom.Dock = DockStyle.Bottom;
            pnlSidebarBottom.Height = 34;
            pnlSidebarBottom.BackColor = Color.FromArgb(40, 40, 40);

            btnAddSite.Dock = DockStyle.Left;
            btnAddSite.Width = 90;
            btnAddSite.FlatStyle = FlatStyle.Flat;
            btnAddSite.FlatAppearance.BorderSize = 0;
            btnAddSite.BackColor = Color.FromArgb(55, 90, 145);
            btnAddSite.ForeColor = Color.White;
            btnAddSite.Text = "추가";
            btnAddSite.UseVisualStyleBackColor = false;

            btnRemoveSite.Dock = DockStyle.Right;
            btnRemoveSite.Width = 90;
            btnRemoveSite.FlatStyle = FlatStyle.Flat;
            btnRemoveSite.FlatAppearance.BorderSize = 0;
            btnRemoveSite.BackColor = Color.FromArgb(60, 60, 60);
            btnRemoveSite.ForeColor = Color.White;
            btnRemoveSite.Text = "삭제";
            btnRemoveSite.UseVisualStyleBackColor = false;

            pnlSidebarBottom.Controls.Add(btnAddSite);
            pnlSidebarBottom.Controls.Add(btnRemoveSite);

            pnlSidebar.Controls.Add(siteList);
            pnlSidebar.Controls.Add(pnlSidebarBottom);

            // ===== 웹뷰 =====
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(24, 24, 24);

            // ===== Form1 =====
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(24, 24, 24);
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(760, 420);
            FormBorderStyle = FormBorderStyle.None;
            Text = "JHP";

            // ⚠️ Dock=Top 컨트롤은 역 z-order (나중에 추가 = 최상단).
            // webView(Fill) → pnlSidebar(Left) → pnlMenuBar(Top, 2번째) → pnlTitleBar(Top, 최상단) 순서로
            // 반드시 마지막에 pnlTitleBar를 추가해야 메뉴바가 타이틀바를 가리지 않음.
            Controls.Add(webView);
            Controls.Add(pnlSidebar);
            Controls.Add(pnlMenuBar);
            Controls.Add(pnlTitleBar);

            ResumeLayout(false);
        }
    }
}
