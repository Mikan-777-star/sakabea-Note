using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinFormsApp2.Services;

namespace WinFormsApp2.NoteApp.UI
{
    public class DashboardPanel : UserControl
    {
        public WebView2 _webView { get; private set; } = null!;
        private Label _titleLabel = null!;
        private readonly MarkdownConverter _markdownConverter = null!;
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();

        private const string VIRTUAL_HOST_NAME = "app.assets";
        private const string VIRTUAL_HOST_URL = "https://app.assets/";

        public event EventHandler<LinkClickedEventArgs>? LinkClicked;
        public string BasePath { get; set; } = Directory.GetCurrentDirectory();
        public bool isDarkMode { get; set; } = false;

        public DashboardPanel()
        {
            _markdownConverter = new MarkdownConverter();
            InitializeUI();
            _ = InitializeWebView2Async();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Padding = new Padding(10);

            // 1. タイトルラベル (日付などを表示)
            _titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Text = "Select a date...",
                Font = new Font("Meiryo UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 2. プレビュー用WebView2
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
            };

            // コントロール追加 (順序重要: Fillを最後に追加しないと重なる場合がある)
            this.Controls.Add(_webView);
            this.Controls.Add(_titleLabel);
            _webView.BringToFront(); // タイトルの下に配置したいが、Dockの都合上、単純に追加
            _titleLabel.BringToFront(); // タイトルを一番上に
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);

                if (_webView.CoreWebView2 != null)
                {
                    var settings = _webView.CoreWebView2.Settings;
                    //_webView.CoreWebView2.Settings.IsScriptEnabled = false;
                    //
                    //
                    // 2. ブラウザ標準のショートカットキーを無効化
                    // (Ctrl+P, Ctrl+F, F5, F12 などが効かなくなる)
                    settings.AreBrowserAcceleratorKeysEnabled = false;

                    // 3. 右クリックメニューを無効化
                    // (「検証」とか「印刷」とか出させない)
                    settings.AreDefaultContextMenusEnabled = false;

                    // 4. Ctrl+ホイールでのズームを無効化
                    // (勝手に拡大縮小されるのを防ぐ)
                    settings.IsZoomControlEnabled = false;

                    // 5. ステータスバー（リンクにマウスを乗せた時の左下の表示）を無効化
                    settings.IsStatusBarEnabled = false;
                    _webView.CoreWebView2.AddWebResourceRequestedFilter(
                        $"{VIRTUAL_HOST_URL}*",
                        CoreWebView2WebResourceContext.All
                    );
                    // 画像表示のための仮想ホストマッピング (NoteEditorPanelと同じ)
                    //_webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    //    VIRTUAL_HOST_NAME,
                    //    BasePath,
                    //    CoreWebView2HostResourceAccessKind.Allow
                    //);

                    _webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                    _webView.CoreWebView2.Navigate("about:blank");
                    _webView.NavigationStarting += WebView_NavigationStarting;
                }
                _webViewInitializedTcs.TrySetResult(true);
            }
            catch
            {
                _webViewInitializedTcs.TrySetResult(false);
            }
        }

        public async void SetTheme(CoreWebView2PreferredColorScheme color,bool isDark)
        {
            await _webViewInitializedTcs.Task;
            _webView.CoreWebView2.Profile.PreferredColorScheme = color;
                // JS関数を呼ぶだけ。HTMLのリロードは発生しない！
             await _webView.CoreWebView2.ExecuteScriptAsync($"window.setTheme({isDark.ToString().ToLower()});");
        
        }
        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            // 1. URLから相対パスを解析する
            // 例: https://app.assets/assets/image.png -> assets/image.png
            var uri = new Uri(e.Request.Uri);
            string relativePath = uri.AbsolutePath.TrimStart('/');

            // 2. ローカルの絶対パスに変換
            // URLデコードを忘れずに（スペースが %20 になってたりするから）
            string localPath = Path.Combine(BasePath, System.Web.HttpUtility.UrlDecode(relativePath));

            // 3. ファイルが存在すれば返す
            if (File.Exists(localPath))
            {
                try
                {
                    // ファイルを開く（読み取り専用・共有モード）
                    FileStream stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // レスポンスを作成 (MIMEタイプは簡易判定)
                    string mimeType = "image/png"; // デフォルト
                    string ext = Path.GetExtension(localPath).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg") mimeType = "image/jpeg";
                    if (ext == ".gif") mimeType = "image/gif";
                    if (ext == ".svg") mimeType = "image/svg+xml";

                    // 200 OK を返す
                    e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        stream,
                        200,
                        "OK",
                        $"Content-Type: {mimeType}"
                    );
                }
                catch
                {
                    // エラー時は 404 Not Found
                    e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
                }
            }
            else
            {
                e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
            }
        }
        /// <summary>
        /// 外部からコンテンツを更新するためのメソッド
        /// </summary>
        /// 
        private string _lastRenderedText = string.Empty;
        bool needsFullReload = true;
        public async void UpdateDashboard(string title, string markdown)
        {
            _titleLabel.Text = title;

            // WebViewの初期化待ち
            bool isReady = await _webViewInitializedTcs.Task;
            if (!isReady || _webView.CoreWebView2 == null) return;

            // 2. ★重要: テキストが変わってないなら何もしない（無駄な処理をカット）
            if (markdown == _lastRenderedText) return;
            _lastRenderedText = markdown;

            // 3. 状況に応じて更新方法を変える

            // テーマ適用直後などでURLが "about:blank" の場合、または初回はフルロードが必要
            string currentSource = _webView.Source.ToString();
            bool isDark = _webView.BackColor.R < 128; // 簡易判定
            if (needsFullReload)
            {
                // A. フルロード (NavigateToString)
                // 現在のテーマ状態を取得する必要があるわね。
                // 簡易的に背景色で判定するか、外部から IsDarkMode を渡す設計にするか。
                // ここでは「とりあえず更新」するわ。
               
                string html = _markdownConverter.ToHtml(markdown, VIRTUAL_HOST_URL, isDark);
                _webView.CoreWebView2.NavigateToString(html);
                needsFullReload = false;
            }
            else
            {
                // B. スマート更新 (JavaScript Injection)
                // ボディの中身だけ変換

                // ★超重要: C#の文字列をJSに渡すときは、必ずJSONエスケープすること！
                // これをしないと、本文に " とか改行が入った瞬間にJSエラーで死ぬわ。
                string htmlBody = _markdownConverter.ToHtml(markdown, VIRTUAL_HOST_URL, isDark, false);


                string safeJson = JsonSerializer.Serialize(htmlBody);

                // 定義しておいた window.updateContent 関数を呼ぶ
                // ExecuteScriptAsync は非同期でJSを実行するわ
                await _webView.CoreWebView2.ExecuteScriptAsync($"window.updateContent({safeJson});");
            }
        }
        
        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // "app://open/" で始まるリンクなら、アプリ側で処理してブラウザ遷移を止める
            if (e.Uri.StartsWith("app://open/"))
            {
                e.Cancel = true;
                var uri = new Uri(e.Uri);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

                string path = query["path"];
                string keyword = query["keyword"]; // ★取得
                int lineStr = int.Parse(query["LineNumber"]);

                if (!string.IsNullOrEmpty(path))
                {
                    LinkClicked?.Invoke(this, new LinkClickedEventArgs { Path = path, Keyword = keyword, LineNumber  = lineStr});
                }
            }
        }

    }

}
