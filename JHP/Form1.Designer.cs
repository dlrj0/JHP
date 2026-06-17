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

        private Panel pnlTitleBar;
        private Label lblTitle;
        private Label lblNextAlarm;
        private Label lblVolumeValue;
        private Label lblOpacityValue;
        private NSlider sliderVolume;
        private NSlider sliderOpacity;
        private Button btnAlarmSettings;
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

            pnlTitleBar = new Panel();
            lblTitle = new Label();
            lblNextAlarm = new Label();
            lblVolumeValue = new Label();
            lblOpacityValue = new Label();
            sliderVolume = new NSlider();
            sliderOpacity = new NSlider();
            btnAlarmSettings = new Button();
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

            lblNextAlarm.AutoSize = true;
            lblNextAlarm.ForeColor = Color.FromArgb(150, 200, 255);
            lblNextAlarm.Location = new Point(64, 10);
            lblNextAlarm.Text = "⏱ 클릭하여 시작";
            lblNextAlarm.Cursor = Cursors.Hand;

            lblVolumeValue.AutoSize = true;
            lblVolumeValue.ForeColor = Color.LightGray;
            lblVolumeValue.Location = new Point(330, 10);
            lblVolumeValue.Text = "볼륨 50";

            sliderVolume.Location = new Point(388, 11);
            sliderVolume.Size = new Size(90, 20);
            sliderVolume.Minimum = 0;
            sliderVolume.Maximum = 100;

            lblOpacityValue.AutoSize = true;
            lblOpacityValue.ForeColor = Color.LightGray;
            lblOpacityValue.Location = new Point(490, 10);
            lblOpacityValue.Text = "투명도 100%";

            sliderOpacity.Location = new Point(570, 11);
            sliderOpacity.Size = new Size(90, 20);
            sliderOpacity.Minimum = 30;
            sliderOpacity.Maximum = 100;

            btnAlarmSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAlarmSettings.BackColor = Color.FromArgb(28, 28, 28);
            btnAlarmSettings.FlatStyle = FlatStyle.Flat;
            btnAlarmSettings.FlatAppearance.BorderSize = 0;
            btnAlarmSettings.ForeColor = Color.White;
            btnAlarmSettings.Location = new Point(818, 4);
            btnAlarmSettings.Size = new Size(36, 28);
            btnAlarmSettings.Text = "⏰";
            btnAlarmSettings.UseVisualStyleBackColor = false;

            // ControlButton은 Dock=Right 사용 (Anchor+Location 사용 시 리사이즈에서 잘림)
            btnClose.Dock = DockStyle.Right;
            btnClose.Type = ControlButton.ButtonType.Close;

            btnMaximize.Dock = DockStyle.Right;
            btnMaximize.Type = ControlButton.ButtonType.Maximize;

            btnMinimize.Dock = DockStyle.Right;
            btnMinimize.Type = ControlButton.ButtonType.Minimize;

            // Dock=Right는 AddRange 순서대로 오른쪽→왼쪽 배치
            // Close가 맨 오른쪽이 되려면 Close 먼저
            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(lblNextAlarm);
            pnlTitleBar.Controls.Add(lblVolumeValue);
            pnlTitleBar.Controls.Add(sliderVolume);
            pnlTitleBar.Controls.Add(lblOpacityValue);
            pnlTitleBar.Controls.Add(sliderOpacity);
            pnlTitleBar.Controls.Add(btnAlarmSettings);
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

            // Controls.Add 순서가 핵심:
            // Dock=Fill/Left는 먼저, Dock=Top은 나중에 추가할수록 위에 표시됨
            Controls.Add(webView);       // Fill (가장 먼저)
            Controls.Add(pnlSidebar);    // Left
            Controls.Add(pnlMenuBar);    // Top (타이틀바 아래)
            Controls.Add(pnlTitleBar);   // Top (마지막 = 최상단)

            ResumeLayout(false);
        }
    }
}
