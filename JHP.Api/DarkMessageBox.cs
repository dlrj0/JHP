namespace JHP.Api;

// 앱의 어두운 창(알람 설정/사이트 추가)과 같은 색의 메시지 상자. MessageBox.Show와 같은 형태로 쓴다.
// 아이콘은 그리지 않는다(icon 인수는 호출부 호환용). 시스템 타이틀바는 DarkTitleBar로 어둡게 맞춘다.
public static class DarkMessageBox
{
    public static DialogResult Show(IWin32Window? owner, string text, string caption = "JHP",
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        using var form = new MessageForm(text, caption, buttons);
        return owner is null ? form.ShowDialog() : form.ShowDialog(owner);
    }

    private sealed class MessageForm : Form
    {
        private readonly DialogResult _closeResult;
        private static readonly bool TestOffscreen = Environment.GetEnvironmentVariable("JHP_OFFSCREEN") == "1";

        protected override bool ShowWithoutActivation => TestOffscreen;

        protected override CreateParams CreateParams   // 자동 테스트에서는 활성화되지 않는 창으로 만든다(WS_EX_NOACTIVATE)
        {
            get
            {
                var cp = base.CreateParams;
                if (TestOffscreen) cp.ExStyle |= 0x08000000;
                return cp;
            }
        }


        public MessageForm(string text, string caption, MessageBoxButtons buttons)
        {
            Text = caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;
            Font = new Font("Malgun Gothic", 9F);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var results = buttons switch
            {
                MessageBoxButtons.OKCancel => new[] { DialogResult.OK, DialogResult.Cancel },
                MessageBoxButtons.YesNo => new[] { DialogResult.Yes, DialogResult.No },
                MessageBoxButtons.YesNoCancel => new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel },
                _ => new[] { DialogResult.OK },
            };
            // Esc/X: 취소 버튼이 있으면 취소, 예/아니오만 있으면 아니오, 확인만 있으면 확인 (MessageBox와 같은 규칙)
            _closeResult = results.Contains(DialogResult.Cancel) ? DialogResult.Cancel
                : results.Contains(DialogResult.No) ? DialogResult.No
                : DialogResult.OK;

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 18, 20, 14),
            };
            var label = new Label
            {
                Text = text.Length > 600 ? text[..600] + "…" : text,   // 아주 긴 예외 메시지로 버튼이 화면 밖으로 밀리지 않게
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                MinimumSize = new Size(240, 0),
                ForeColor = Color.White,
                UseMnemonic = false,   // 주소의 &가 밑줄 단축키로 처리되지 않게
                Margin = new Padding(0, 0, 0, 18),
            };
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            layout.Controls.Add(label, 0, 0);
            layout.Controls.Add(row, 0, 1);

            Button? first = null;
            foreach (var result in results.Reverse())   // RightToLeft 흐름이라 뒤에서부터 넣어 화면에는 왼쪽부터 보이게 한다
            {
                var button = new Button
                {
                    Text = result switch
                    {
                        DialogResult.OK => "확인",
                        DialogResult.Cancel => "취소",
                        DialogResult.Yes => "예",
                        DialogResult.No => "아니오",
                        _ => result.ToString(),
                    },
                    DialogResult = result,
                    Width = 84,
                    Height = 28,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    Margin = new Padding(8, 0, 0, 0),
                    UseVisualStyleBackColor = false,
                };
                button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
                row.Controls.Add(button);
                if (result == results[0]) first = button;
            }

            Controls.Add(layout);
            if (first is not null)
            {
                AcceptButton = first;                                             // Enter = 첫 버튼(확인/예)
                ActiveControl = first;                                            // 역순으로 넣은 탓에 첫 탭 스톱이 마지막 버튼이므로 초기 포커스를 직접 지정
            }
            CancelButton = row.Controls.OfType<Button>().FirstOrDefault(b => b.DialogResult == _closeResult);

            // 자식 컨트롤을 모두 추가한 뒤에 배율 모드를 지정한다 (앞에서 하면 빈 폼에만 적용됨)
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 타이틀바 X로 닫을 때도 Esc와 같은 결과를 돌려준다
            if (e.CloseReason == CloseReason.UserClosing && DialogResult == DialogResult.Cancel && _closeResult != DialogResult.Cancel)
                DialogResult = _closeResult;
            base.OnFormClosing(e);
        }
    }
}
