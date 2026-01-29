using System;
using System.Collections.Generic;
using System.ComponentModel;
using WinFormsApp2.Services;

namespace WinFormsApp2.Views
{
    // PresenterがViewに対して「これをやれ」と命令できるリストよ
    public interface IMainView
    {
        // イベント: ユーザーのアクションをPresenterに通知するためのフック
        event EventHandler LoadRequested; // フォームロード時
        event EventHandler<string> FileSelected; // ツリーでファイルが選ばれた時
        event EventHandler SaveRequested; // 保存が要求された時
        event EventHandler NewFileRequested;//新規作成のリクエストが来たときにトリガするメソッド軍
        event EventHandler<DateTime> DateSelected; //日付選択がされたときにトリガするメソッド
        event EventHandler ActiveDocumentChanged;
        event EventHandler EditorContentChanged;
        event EventHandler ThemeChanged;//テーマを更新するときにトリガさせる関数
        MarkdownDocument? GetActiveDocument();

        IEnumerable<MarkdownDocument> GetAllDocuments();

        // ユーザーに保存先ファイルのパスを尋ねる
        // キャンセルされたら null を返す
        string? AskUserForSavePath(string defaultFileName);

        // プロパティ: 状態の取得・設定
        // フォームが閉じるのを防ぐかどうかのフラグなどをやり取りするイメージ
        bool IsDisposed { get; }

        // メソッド: Viewに対する命令
        // 具体的なコントロール（TreeViewなど）は引数に出さない！
        // 「データのリストを渡すから、あとはよしなに表示しろ」というスタイル。
        void UpdateFileTree(IEnumerable<FileNodeModel> nodes);

        // タブ操作
        void OpenDocumentTab(MarkdownDocument document);
        void CloseDocumentTab(MarkdownDocument document);
        bool TrySelectTab(string filePath);

        void UpdateTabTitle(MarkdownDocument document);

        // エディタ操作
        string GetCurrentEditorContent();
        void SetEditorContent(string content);

        // ユーザーへのフィードバック
        void ShowError(string message);
        void ShowMessage(string message);
        bool ConfirmAction(string message); // Yes/Noを聞く

        //ダッシュボードの更新
        public void UpdateDashboard(string title, string content);

        void InvokeOnUI(Action action);

        // ★追加: アプリが閉じようとしている時のイベント (キャンセル可能)
        event EventHandler<CancelEventArgs> CloseRequested;

        // ★追加: ウィンドウの設定（位置・サイズ）をセットする
        void SetWindowSettings(AppSettings settings);

        // ★追加: 現在のウィンドウ設定を取得する
        AppSettings GetWindowSettings();

        // ★追加: 全文検索リクエスト
        event EventHandler<string> GlobalSearchRequested;

        // ★追加: ダッシュボード内のリンククリック通知（これをPresenterが購読する）

        event EventHandler<LinkClickedEventArgs> DashboardLinkClicked;

        void HighlightEditorText(string keyword, int line);

        void SetStatusMessage(string message);

        event EventHandler SearchClearRequested;

        void ClearSearchBox();

        void ApplyTheme(ThemeService theme);

        event EventHandler<Image> ImagePasteRequested;

        void InsertTextAtCursor(string text);

        string? AskUserForFolder(string currentPath);
        event EventHandler ChangeFolderRequested;

        void SetResourceBasePath(string path);
        void UpdateResourcePath(string newPath);

        void StartConsole(string? path = null);

        event EventHandler FileTreeRefreshRequested;

        void RestartConsole(string newPath);

        // イベント追加
        event EventHandler ExportHtmlRequested;
        event EventHandler ExportPdfRequested;

        // パスを聞くメソッド (フィルター指定可能に拡張、または別メソッド作成)
        // 今回は既存の AskUserForSavePath を拡張するか、新しいのを作るか。
        // 拡張性を考えて新しいのを作りましょう。
        string? AskUserForExportPath(string defaultName, string filter);

        // PDF保存命令 (Presenter -> View -> Panel)
        Task ExportPdfToPath(string path);
        public void AddPluginMenu(string path, string text, EventHandler action, string shortcut = "");

        public string GetSelectedEditorText();

        event EventHandler CommandPaletteRequested;


        event DragEventHandler DragEve;
        void Close();
    }

    // ツリー表示用の軽量なデータモデル（ViewModel的なもの）
    public class FileNodeModel
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public List<FileNodeModel> Children { get; set; } = new List<FileNodeModel>();
    }
}