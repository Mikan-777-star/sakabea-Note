using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinFormsApp2.Services;

namespace WinFormsApp2.NoteApp.UI
{
    public class CommandPaletteForm : Form
    {
        private TextBox _inputBox;
        private ListBox _resultList;
        private List<AppCommand> _allCommands;

        // 選択されたコマンドを返すプロパティ
        public AppCommand? SelectedCommand { get; private set; }

        public CommandPaletteForm(List<AppCommand> commands, bool isDark)
        {
            _allCommands = commands;

            // --- フォーム設定 ---
            this.FormBorderStyle = FormBorderStyle.None; // 枠なし
            this.StartPosition = FormStartPosition.CenterParent; // 親の中央
            this.Size = new Size(500, 300);
            this.ShowInTaskbar = false;
            this.KeyPreview = true; // キー入力をフォームで受け取る

            // 色設定
            Color bg = isDark ? Color.FromArgb(30, 30, 30) : Color.White;
            Color fg = isDark ? Color.White : Color.Black;
            Color border = isDark ? Color.FromArgb(0, 122, 204) : Color.DeepSkyBlue; // 枠線

            this.BackColor = border; // 枠線の色になる（Paddingで中身を縮めるため）
            this.Padding = new Padding(2); // 2pxの枠線

            // --- 入力ボックス ---
            _inputBox = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 12F),
                BorderStyle = BorderStyle.None,
                BackColor = bg,
                ForeColor = fg,
                PlaceholderText = "> コマンドを入力..." // .NET Core以降なら使える
            };
            _inputBox.TextChanged += (s, e) => FilterCommands();
            _inputBox.KeyDown += InputBox_KeyDown;

            // --- リストボックス ---
            _resultList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.None,
                BackColor = bg,
                ForeColor = fg,
                IntegralHeight = false // ぴったり埋める
            };
            _resultList.DoubleClick += (s, e) => ExecuteSelection();

            // パネル（中身のコンテナ）
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bg,
                Padding = new Padding(10)
            };
            contentPanel.Controls.Add(_resultList);
            contentPanel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = bg }); // 余白
            contentPanel.Controls.Add(_inputBox);

            this.Controls.Add(contentPanel);

            // 初期フィルタ
            FilterCommands();
        }

        // キー操作の制御
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // ESCで閉じる
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            // 上下キーでリスト移動
            if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                if (_resultList.SelectedIndex < _resultList.Items.Count - 1)
                    _resultList.SelectedIndex++;
            }
            else if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                if (_resultList.SelectedIndex > 0)
                    _resultList.SelectedIndex--;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // ビープ音消し
                ExecuteSelection();
            }
        }

        private void FilterCommands()
        {
            string query = _inputBox.Text;
            _resultList.BeginUpdate();
            _resultList.Items.Clear();

            var matches = _allCommands
                .Where(c => c.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var cmd in matches)
            {
                _resultList.Items.Add(cmd);
            }

            if (_resultList.Items.Count > 0) _resultList.SelectedIndex = 0;
            _resultList.EndUpdate();
        }

        private void ExecuteSelection()
        {
            if (_resultList.SelectedItem is AppCommand cmd)
            {
                SelectedCommand = cmd;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        // 表示されたら入力欄にフォーカス
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _inputBox.Focus();
        }
    }
}