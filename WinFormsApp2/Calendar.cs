using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsApp2.Services; // ThemeServiceのために必要

namespace WinFormsApp2.NoteApp.UI
{
    public class DarkCalendar : UserControl
    {
        // 現在表示している年月
        private DateTime _currentMonth;

        // 選択されている日付
        public DateTime SelectedDate { get; private set; }

        // イベント
        public event EventHandler<DateTime>? DateSelected;

        // UI部品
        private Label _lblMonth = null!;
        private Button _btnPrev = null!;
        private Button _btnNext = null!;
        private TableLayoutPanel _grid = null!;
        private Button[] _dayButtons = new Button[42]; // 6週 x 7日
        private Color _sundayColor = Color.Red;
        private Color _saturdayColor = Color.Blue;
        private Color _weekdayColor = Color.Black;
        private Color _trailingColor = Color.Gray;
        private Color _accentColor = Color.DeepSkyBlue;
        private Color _baseBackColor = Color.White;
        private Color _hoverColor = Color.WhiteSmoke;
        public DarkCalendar()
        {
            _currentMonth = DateTime.Today;
            SelectedDate = DateTime.Today;

            InitializeComponent();
            RenderCalendar();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(250, 250);
            this.Padding = new Padding(5);

            // 1. ヘッダーパネル ( <  2025年11月  > )
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

            _btnPrev = new Button { Text = "<", Dock = DockStyle.Left, Width = 30, FlatStyle = FlatStyle.Flat };
            _btnPrev.FlatAppearance.BorderSize = 0;
            _btnPrev.Click += (s, e) => { _currentMonth = _currentMonth.AddMonths(-1); RenderCalendar(); };

            _btnNext = new Button { Text = ">", Dock = DockStyle.Right, Width = 30, FlatStyle = FlatStyle.Flat };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += (s, e) => { _currentMonth = _currentMonth.AddMonths(1); RenderCalendar(); };

            _lblMonth = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            headerPanel.Controls.Add(_lblMonth);
            headerPanel.Controls.Add(_btnPrev);
            headerPanel.Controls.Add(_btnNext);
            this.Controls.Add(headerPanel);

            // 2. 曜日ヘッダーと日付グリッド
            _grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 7, // 1行目:曜日, 2-7行目:日付
                Padding = new Padding(0, 5, 0, 0)
            };

            // カラム比率を均等に
            for (int i = 0; i < 7; i++) _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            for (int i = 0; i < 7; i++) _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));

            // 曜日ラベル
            string[] days = { "日", "月", "火", "水", "木", "金", "土" };
            foreach (var d in days)
            {
                var lbl = new Label { Text = d, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, Margin = new Padding(0) };
                _grid.Controls.Add(lbl);
            }

            // 日付ボタン (42個作っておいて使い回す)
            for (int i = 0; i < 42; i++)
            {
                var btn = new Button
                {
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(1),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += DayButton_Click;
                _dayButtons[i] = btn;
                _grid.Controls.Add(btn);
            }

            this.Controls.Add(_grid);
            headerPanel.BringToFront(); // ヘッダーを上に
        }

        private void RenderCalendar()
        {
            _lblMonth.Text = _currentMonth.ToString("yyyy年 M月");

            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (int)firstDayOfMonth.DayOfWeek;
            DateTime startDate = firstDayOfMonth.AddDays(-offset);

            for (int i = 0; i < 42; i++)
            {
                DateTime date = startDate.AddDays(i);
                Button btn = _dayButtons[i];

                btn.Text = date.Day.ToString();
                btn.Tag = date; // Tagに日付を埋め込む
            }

            // ★重要: 日付をセットした後に、必ず色を塗り直す！
            UpdateButtonColors();
        }

        // ★共通化: 現在の設定色を使ってボタンを塗るメソッド
        private void UpdateButtonColors()
        {
            // ヘッダーの色適用
            this.BackColor = _baseBackColor;
            this.ForeColor = _weekdayColor;
            _lblMonth.ForeColor = _weekdayColor;
            _btnPrev.ForeColor = _weekdayColor;
            _btnNext.ForeColor = _weekdayColor;

            // 曜日ヘッダーの色（ApplyThemeにあったロジック）
            int dayIndex = 0;
            foreach (Control c in _grid.Controls)
            {
                if (c is Label lbl)
                {
                    if (dayIndex == 0) lbl.ForeColor = _sundayColor;
                    else if (dayIndex == 6) lbl.ForeColor = _saturdayColor;
                    else lbl.ForeColor = _trailingColor; // 曜日は少し薄くてもいいかも

                    dayIndex++;
                    if (dayIndex > 6) dayIndex = 0;
                }
                if (dayIndex > 6) break;
            }

            // 日付ボタンの色
            foreach (var btn in _dayButtons)
            {
                if (btn.Tag is DateTime date)
                {
                    bool isSelected = date.Date == SelectedDate.Date;
                    bool isToday = date.Date == DateTime.Today;
                    bool isSameMonth = date.Month == _currentMonth.Month;

                    // 1. 文字の基本色決定
                    Color targetForeColor;
                    if (!isSameMonth)
                    {
                        targetForeColor = _trailingColor;
                    }
                    else if (date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        targetForeColor = _sundayColor;
                    }
                    else if (date.DayOfWeek == DayOfWeek.Saturday)
                    {
                        targetForeColor = _saturdayColor;
                    }
                    else
                    {
                        targetForeColor = _weekdayColor;
                    }

                    // 2. 状態による上書き
                    if (isSelected)
                    {
                        btn.BackColor = _accentColor;
                        btn.ForeColor = Color.White; // 選択時は白文字
                    }
                    else if (isToday)
                    {
                        btn.BackColor = Color.Transparent;
                        btn.ForeColor = _accentColor; // 今日はアクセント色
                        btn.Font = new Font(this.Font, FontStyle.Bold);
                    }
                    else
                    {
                        btn.BackColor = Color.Transparent;
                        btn.ForeColor = targetForeColor;
                        btn.Font = new Font(this.Font, FontStyle.Regular);
                    }

                    btn.FlatAppearance.MouseOverBackColor = _hoverColor;
                }
            }
        }

        private void DayButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime date)
            {
                SelectedDate = date;
                // 選択された月へ移動（必要なら）
                if (date.Month != _currentMonth.Month)
                {
                    _currentMonth = new DateTime(date.Year, date.Month, 1);
                }

                RenderCalendar(); // 再描画（選択色を反映するため）

                // 親に通知
                DateSelected?.Invoke(this, date);
            }
        }

        // ★テーマ適用メソッド// テーマ適用（フィールドを更新して再描画）
        public void ApplyTheme(ThemeService theme)
        {
            // 1. 色の設定をフィールドに保存
            _baseBackColor = theme.BackColor;
            _weekdayColor = theme.ForeColor;
            _trailingColor = theme.MutedForeColor;
            _accentColor = theme.AccentColor;
            _hoverColor = theme.ControlBackColor;

            if (theme.IsDarkMode)
            {
                _sundayColor = Color.LightCoral;
                _saturdayColor = Color.LightSkyBlue;
            }
            else
            {
                _sundayColor = Color.Red;
                _saturdayColor = Color.Blue;
            }

            // 2. 描画メソッドを呼ぶ
            UpdateButtonColors();
        }
    }
}