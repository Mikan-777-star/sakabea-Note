using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinFormsApp2.Views;

namespace WinFormsApp2
{
    partial class Form1 : Form
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
    }
}
