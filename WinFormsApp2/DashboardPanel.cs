using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
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
                    _webView.CoreWebView2.Settings.IsScriptEnabled = false;
                    //
                    //
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

        public async void SetTheme(CoreWebView2PreferredColorScheme color)
        {
            await _webViewInitializedTcs.Task;
            _webView.CoreWebView2.Profile.PreferredColorScheme = color;
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
        public async void UpdateDashboard(string title, string markdownContent)
        {
            _titleLabel.Text = title;

            // WebViewの初期化待ち
            bool isReady = await _webViewInitializedTcs.Task;
            if (!isReady || _webView.CoreWebView2 == null) return;

            // コンテンツが空の場合は案内を表示
            if (string.IsNullOrWhiteSpace(markdownContent))
            {
                string emptyHtml = "<html><body style='font-family: Meiryo UI; color: #888;'>No content for this day.</body></html>";
                _webView.CoreWebView2.NavigateToString(emptyHtml);
            }
            else
            {
                // MarkdownをHTMLに変換して表示
                string html = _markdownConverter.ToHtml(markdownContent, VIRTUAL_HOST_URL, isDarkMode);
                _webView.CoreWebView2.NavigateToString(html);
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
