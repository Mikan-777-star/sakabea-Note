using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinFormsApp2.Views;

namespace WinFormsApp2
{
    partial class Form1 : ModernForm
    {


        private void SetupMenu()
        {
            var fileMenu = new ToolStripMenuItem("ファイル(&F)");
            this.mainMenuStrip.Items.Add(fileMenu);

            var newItem = new ToolStripMenuItem("新規作成(&N)") { ShortcutKeys = Keys.Control | Keys.N };
            newItem.Click += (s, e) => OpenNewDocument();
            fileMenu.DropDownItems.Add(newItem);

            var saveItem = new ToolStripMenuItem("上書き保存(&S)") { ShortcutKeys = Keys.Control | Keys.S };
            saveItem.Click += (s, e) => SaveActiveDocument(false); // ★ここが重要：共通メソッドを呼ぶ
            fileMenu.DropDownItems.Add(saveItem);

            var saveAsItem = new ToolStripMenuItem("名前を付けて保存(&A)");
            saveAsItem.Click += (s, e) => SaveActiveDocument(true); // ★ここが重要：共通メソッドを呼ぶ
            fileMenu.DropDownItems.Add(saveAsItem);

            var openFolderItem = new ToolStripMenuItem("フォルダを開く...(&O)");
            // ショートカットは Ctrl+K, Ctrl+O などが一般的だけど、シンプルに Ctrl+Shift+O とかでもいいわ
            openFolderItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.O;
            openFolderItem.Click += OpenWorkspaceFolder;
            fileMenu.DropDownItems.Add(openFolderItem); // ファイルメニューに追加

            var exportMenu = new ToolStripMenuItem("エクスポート(&E)");
            var htmlItem = new ToolStripMenuItem("HTMLとして保存...");
            var pdfItem = new ToolStripMenuItem("PDFとして保存...");

            htmlItem.Click += (s, e) => ExportHtmlRequested?.Invoke(this, EventArgs.Empty);
            pdfItem.Click += (s, e) => ExportPdfRequested?.Invoke(this, EventArgs.Empty);

            exportMenu.DropDownItems.Add(htmlItem);
            exportMenu.DropDownItems.Add(pdfItem);
            fileMenu.DropDownItems.Add(exportMenu); // ファイルメニューの中に入れる

            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("終了(&X)");
            exitItem.Click += (s, e) => this.Close();
            fileMenu.DropDownItems.Add(exitItem);

            // --- 表示メニュー ---
            var viewMenu = new ToolStripMenuItem("表示(&V)");
            this.mainMenuStrip.Items.Add(viewMenu);

            // フォントサイズ拡大
            var zoomInItem = new ToolStripMenuItem("フォント拡大(&I)") { ShortcutKeys = Keys.Control | Keys.Oemplus }; // Ctrl + '+'
            zoomInItem.Click += (s, e) => ChangeFontSize(2.0f);
            viewMenu.DropDownItems.Add(zoomInItem);

            // フォントサイズ縮小
            var zoomOutItem = new ToolStripMenuItem("フォント縮小(&O)") { ShortcutKeys = Keys.Control | Keys.OemMinus }; // Ctrl + '-'
            zoomOutItem.Click += (s, e) => ChangeFontSize(-2.0f);
            viewMenu.DropDownItems.Add(zoomOutItem);

            var themechange = new ToolStripMenuItem("テーマ変更");
            themechange.Click += (s, e) => { ThemeChanged?.Invoke(s, e); };
            viewMenu.DropDownItems.Add(themechange);
            // Form1.cs - InitializeCustomUI

            // 表示メニューあたりに追加
            var paletteItem = new ToolStripMenuItem("コマンドパレット(&P)...");
            paletteItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;

            // Presenterに通知するイベントが必要ね
            // IMainViewに event EventHandler CommandPaletteRequested; を追加して、
            // Presenterで _view.CommandPaletteRequested += (s,e) => ShowCommandPalette(); と繋ぐのが正解。

            // ★簡易実装: ここでイベント発火
            paletteItem.Click += (s, e) => CommandPaletteRequested?.Invoke(this, EventArgs.Empty);

            viewMenu.DropDownItems.Add(paletteItem);
        }
        private void ChangeFontSize(float delta)
        {
            float current = noteEditorPanel.GetFontSize();
            noteEditorPanel.SetFontSize(current + delta);
        }
        private void SaveActiveDocument(bool forceSaveAs)
        {
            // 本来は forceSaveAs の情報をPresenterに渡すべきだけど、
            // 今はとりあえず「保存」イベントを発火させるわ
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
        private void OpenNewDocument()
        {
            // Presenterに「新しい紙をちょうだい！」と頼む
            NewFileRequested?.Invoke(this, EventArgs.Empty);
        }

    }
}
