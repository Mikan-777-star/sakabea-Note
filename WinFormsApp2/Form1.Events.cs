using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinFormsApp2.Views;

namespace WinFormsApp2
{
    partial class Form1 : ModernForm
    {
        private TreeNode CreateTreeNode(FileNodeModel model)
        {
            TreeNode node = new TreeNode(model.Name);
            node.Tag = model.FullPath; // ★重要: クリック時にパスを取り出すため

            // アイコンを変えるならここで設定
            node.ImageKey = model.IsDirectory ? "folder" : "file";
            // 子要素があれば再帰的に追加
            foreach (var childModel in model.Children)
            {
                TreeNode childNode = CreateTreeNode(childModel);
                node.Nodes.Add(childNode);
            }

            return node;
        }
        private Label CreateHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Font = new Font("Meiryo UI", 11F, FontStyle.Bold),
                Padding = new Padding(5),
                AutoSize = false,
                Height = 30,
                BackColor = Color.LightCyan,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
        private void CalendarControl_DateSelected(object? sender, DateTime date)
        {
            // Presenterに「この日が選ばれたわよ！」と伝えるだけ。
            // ファイルがあるかとか、テンプレートがどうとか、一切考えない。
            DateSelected?.Invoke(this, date);
        }
        private void NoteTabControl_Deselecting(object? sender, TabControlCancelEventArgs e)
        {
            // 1. 離れるタブのドキュメントを取得
            if (e.TabPage?.Tag is MarkdownDocument doc)
            {
                // 2. エディタの最新内容をドキュメントに書き戻す（これを忘れると編集が消える！）
                string currentText = noteEditorPanel.GetCurrentEditorText();
                doc.UpdateContent(currentText);
            }
        }
        private void NoteTabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 1. 新しく選ばれたタブのドキュメントを取得
            if (noteTabControl.SelectedTab?.Tag is MarkdownDocument doc)
            {
                // 2. ドキュメントの内容をエディタに表示
                noteEditorPanel.DisplayDocument(doc);

                // 3. Presenterに報告「切り替わったわよ」
                ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private void NoteEditorPanel_DocumentContentChanged(object? sender, EventArgs e)
        {
            // Presenterに「文字が変わったわよ」と伝える
            EditorContentChanged?.Invoke(this, EventArgs.Empty);
        }
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Presenterに「閉じていいか？」と聞く。
            // 引数の e (CancelEventArgs) を渡すことで、Presenter側で e.Cancel = true できるようにする。
            CloseRequested?.Invoke(this, e);
        }

        private void OpenWorkspaceFolder(object? sender, EventArgs e)
        {
            // MessageBox... は削除して、イベント発火！
            ChangeFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 文字列からショートカットキーを生成してメニューアイテムに適用する
        /// </summary>
        /// <param name="menuItem">対象のメニューアイテム</param>
        /// <param name="shortcutString">"Ctrl+S" などの文字列表現</param>
        public void ApplyShortcut(ToolStripMenuItem menuItem, string shortcutString)
        {
            if (menuItem == null) throw new ArgumentNullException(nameof(menuItem));
            if (string.IsNullOrWhiteSpace(shortcutString)) return;

            try
            {
                // TypeDescriptorを使って、文字列をKeys列挙体に変換する
                // これが最も標準的で強力なコンバーターよ
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(Keys));

                // ConvertFromString は "Ctrl+S" を Keys.Control | Keys.S に変換してくれる
                Keys keys = (Keys)converter.ConvertFromString(shortcutString);

                // メニューにセット
                menuItem.ShortcutKeys = keys;

                // ユーザーにショートカットを表示するかどうか（基本はtrueでしょ）
                menuItem.ShowShortcutKeys = true;
            }
            catch (Exception ex)
            {
                // ここで握りつぶすか、ログを吐くかはあんたの設計次第だけど、
                // 無効な文字列でアプリをクラッシュさせるのだけは避けなさい。
                Console.WriteLine($"警告: ショートカット '{shortcutString}' の解析に失敗したわ。理由: {ex.Message}");

                // 失敗時はショートカットなしにする等のフォールバック
                menuItem.ShortcutKeys = Keys.None;
            }
        }
    }
}
