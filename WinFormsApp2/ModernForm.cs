using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsApp2
{
    public class ModernForm : Form
    {

            // --- テーマ色の定義 ---
            private struct ThemeColors
            {
                public Color Background;   // メイン背景
                public Color TitleBar;     // タイトルバー背景
                public Color Text;         // 文字色
                public Color Border;       // ウィンドウ外枠
                public Color ButtonHover;  // ボタンのホバー色
                public Color ButtonText;   // ボタンの文字色
            }

            // ライトモード用パレット (モダンなオフホワイト系)
            private readonly ThemeColors LightTheme = new ThemeColors
            {
                Background = Color.FromArgb(248, 248, 248),
                TitleBar = Color.FromArgb(230, 230, 230),
                Text = Color.FromArgb(40, 40, 40),
                Border = Color.FromArgb(180, 180, 180),
                ButtonHover = Color.FromArgb(210, 210, 210),
                ButtonText = Color.Black
            };

            // ダークモード用パレット (モダンなダークグレー系)
            private readonly ThemeColors DarkTheme = new ThemeColors
            {
                Background = Color.FromArgb(32, 32, 32),
                TitleBar = Color.FromArgb(45, 45, 48),
                Text = Color.FromArgb(220, 220, 220),
                Border = Color.FromArgb(60, 60, 60),
                ButtonHover = Color.FromArgb(65, 65, 68),
                ButtonText = Color.White
            };

            private ThemeColors CurrentTheme;
            public bool IsDarkMode { get; private set; }

            // UIパーツ
            private Panel titleBar;
            private Button closeButton;
            private Button maxButton;
            private Button minButton;

            // リサイズ用グリップ幅
            private const int GripSize = 10;

            public ModernForm()
            {
                // --- 基本設定 ---
                this.FormBorderStyle = FormBorderStyle.None; // 枠なし
                this.DoubleBuffered = true; // チラつき完全防止
                this.ResizeRedraw = true;   // リサイズ時の再描画
                                            //this.Padding = new Padding(1); // 境界線を描く隙間を作る
            this.Padding = new Padding(1, 0, 1, 1);
            // 初期テーマ適用 (とりあえずライトモード。後でSetThemeで変えられる)
            ApplyTheme(false);
                Load += (s, e) => InitializeModernTitleBar();
            }

            // テーマ切り替え (これを呼ぶだけで全部変わる)
            public void SetTheme(bool isDark)
            {
                ApplyTheme(isDark);

                // フォーム自体の色更新
                this.BackColor = CurrentTheme.Background;
                this.Invalidate(); // 枠線再描画

                // タイトルバー更新
                if (titleBar != null)
                {
                    titleBar.BackColor = CurrentTheme.TitleBar;
                    titleBar.Invalidate(); // タイトル文字再描画

                    // ボタンの色更新
                    UpdateButtonColor(closeButton);
                    UpdateButtonColor(maxButton);
                    UpdateButtonColor(minButton);
                }
            }

            protected void ApplyTheme(bool isDark)
            {
                IsDarkMode = isDark;
                CurrentTheme = isDark ? DarkTheme : LightTheme;
            }

            private void InitializeModernTitleBar()
            {
                titleBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 32,
                    BackColor = CurrentTheme.TitleBar,
                    Padding = new Padding(10, 0, 0, 0)
                };

                // ドラッグ移動機能
                titleBar.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left && WindowState != FormWindowState.Maximized)
                    {
                        ReleaseCapture();
                        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                    }
                    // ダブルクリックで最大化は titleBar.DoubleClick で実装してもいい
                };

                // ダブルクリックで最大化
                titleBar.DoubleClick += (s, e) => ToggleMaximize();

                // タイトル文字描画 (Labelを使わずPaintで描く＝最速・最高品質)
                titleBar.Paint += (s, e) =>
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        this.Text,
                        this.Font,
                        new Rectangle(15, 0, titleBar.Width - 140, titleBar.Height),
                        CurrentTheme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    );
                };

                // テキスト変更時にタイトルバーも更新
                this.TextChanged += (s, e) => titleBar.Invalidate();

                // ボタン生成
                closeButton = CreateTitleButton("✕", (s, e) => Close(), true); // 閉じるボタンは赤くしてもいい
                maxButton = CreateTitleButton("☐", (s, e) => ToggleMaximize());
                minButton = CreateTitleButton("—", (s, e) => WindowState = FormWindowState.Minimized);

                // 追加 (右詰めなので追加順に注意: 最小化 -> 最大化 -> 閉じる の逆)
                titleBar.Controls.Add(closeButton);
                titleBar.Controls.Add(maxButton);
                titleBar.Controls.Add(minButton);

                this.Controls.Add(titleBar);
            }

            private Button CreateTitleButton(string text, EventHandler onClick, bool isClose = false)
            {
                var btn = new Button
                {
                    Text = text,
                    Dock = DockStyle.Right,
                    Width = 46,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI Symbol", 9f),
                    TabStop = false // フォーカス枠を出さない
                };

                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseDownBackColor = isClose ? Color.DarkRed : CurrentTheme.ButtonHover;
                btn.FlatAppearance.MouseOverBackColor = isClose ? Color.Red : CurrentTheme.ButtonHover;

                btn.Click += onClick;
                UpdateButtonColor(btn); // 初期色設定

                if (isClose)
                {
                    // 閉じるボタンだけホバー時に文字を白くするなどの特別扱い
                    btn.MouseEnter += (s, e) => btn.ForeColor = Color.White;
                    btn.MouseLeave += (s, e) => btn.ForeColor = CurrentTheme.ButtonText;
                }

                return btn;
            }

            private void UpdateButtonColor(Button btn)
            {
                if (btn == null) return;
                btn.ForeColor = CurrentTheme.ButtonText;
                // 閉じるボタン以外はテーマ色に従う
                if (btn != closeButton)
                {
                    btn.FlatAppearance.MouseOverBackColor = CurrentTheme.ButtonHover;
                    btn.FlatAppearance.MouseDownBackColor = CurrentTheme.ButtonHover;
                }
            }

            private void ToggleMaximize()
            {
                WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
                maxButton.Text = (WindowState == FormWindowState.Maximized) ? "⧉" : "☐";
            }

            // 境界線の描画 (フラットデザインの命)
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                // 最大化時は枠線を描かない
                if (WindowState != FormWindowState.Maximized)
                {
                    using (var pen = new Pen(CurrentTheme.Border, 1))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                    }
                }
            }

            // --- Win32 API & WndProc ---
            [DllImport("user32.dll")] public static extern bool ReleaseCapture();
            [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            private const int WM_NCLBUTTONDOWN = 0xA1;
            private const int HTCAPTION = 0x2;

            protected override void WndProc(ref Message m)
            {
                // リサイズロジック (以前教えたものと同じ)
                const int WM_NCHITTEST = 0x84;
                const int HTCLIENT = 1;
                const int HTLEFT = 10;
                const int HTRIGHT = 11;
                const int HTTOP = 12;
                const int HTTOPLEFT = 13;
                const int HTTOPRIGHT = 14;
                const int HTBOTTOM = 15;
                const int HTBOTTOMLEFT = 16;
                const int HTBOTTOMRIGHT = 17;

                if (m.Msg == WM_NCHITTEST)
                {
                    int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                    int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                    Point pt = PointToClient(new Point(x, y));
                    Size clientSize = ClientSize;

                    // 最大化時はリサイズ判定しない
                    if (WindowState == FormWindowState.Maximized)
                    {
                        base.WndProc(ref m);
                        return;
                    }

                    bool l = pt.X <= GripSize;
                    bool r = pt.X >= clientSize.Width - GripSize;
                    bool t = pt.Y <= GripSize;
                    bool b = pt.Y >= clientSize.Height - GripSize;

                    if (l && t) { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if (r && t) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    if (l && b) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    if (r && b) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    if (l) { m.Result = (IntPtr)HTLEFT; return; }
                    if (r) { m.Result = (IntPtr)HTRIGHT; return; }
                    if (t) { m.Result = (IntPtr)HTTOP; return; }
                    if (b) { m.Result = (IntPtr)HTBOTTOM; return; }
                }

                base.WndProc(ref m);
            }

            // スナップ機能（Aero Snap）を有効にするおまじない
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.Style |= 0x00040000; // WS_THICKFRAME
                    cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                    return cp;
                }
            }
        }
}
