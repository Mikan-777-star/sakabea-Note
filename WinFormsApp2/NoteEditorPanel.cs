using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading; // Timerのために必須
using System.Threading.Tasks; // Taskのために必須
using System.Windows.Forms;
using WinFormsApp2.NoteApp.UI.WinFormsApp2.NoteApp.UI;
using WinFormsApp2.Services;

namespace WinFormsApp2.NoteApp.UI
{

    namespace WinFormsApp2.NoteApp.UI
    {
        // 標準のRichTextBoxを継承して、IMEの状態を知れるようにする
        public class ExRichTextBox : RichTextBox
        {
            // IMEが作成中（変換中）かどうか
            public bool IsImeComposing { get; private set; } = false;

            private const int WM_IME_STARTCOMPOSITION = 0x010D;
            private const int WM_IME_ENDCOMPOSITION = 0x010E;
            private const int WM_IME_COMPOSITION = 0x010F;

            protected override void WndProc(ref Message m)
            {
                
                switch (m.Msg)
                {
                    case WM_IME_STARTCOMPOSITION:
                        IsImeComposing = true;
                        break;

                    case WM_IME_ENDCOMPOSITION:
                        IsImeComposing = false;
                        break;
                }

                base.WndProc(ref m);
            }
        }
    }
    public class NoteEditorPanel : UserControl
    {
        public SplitContainer SplitterContainer { get; private set; } = null!;

        public SplitContainer downContainer { get; private set; } = null!;
        public ExRichTextBox EditorTextBox { get; private set; } = null!;
        public WebView2 PreviewWebView2 { get; private set; } = null!;

        public EmbeddedTerminalPanel ConsolePanel { get; private set; } = null!; // 型変更
        // サービス
        private readonly MarkdownConverter _markdownConverter;

        // ★ 1. WebView2初期化制御用のゲートキーパー
        // これが「完了」シグナルを出すまで、UpdatePreviewは待機させられるわ。
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();

        // ★ 2. Debounce（間引き）用のタイマー
        private System.Threading.Timer _debounceTimer = null!;
        private const int DEBOUNCE_DELAY_MS = 300; // 300ミリ秒の遅延

        private const string VIRTUAL_HOST_NAME = "app.assets";
        private const string VIRTUAL_HOST_URL = "https://app.assets/";

        // ★ 追加: 検索パネル用のコントロール
        private Panel _searchPanel = null!;
        private TextBox _searchTextBox = null!;
        private Button _btnNext = null!;
        private Button _btnPrev = null!;
        private Button _btnClose = null!;
        private Label _lblStatus = null!;
        public event EventHandler? DocumentContentChanged;
        public string BasePath { get; set; } = Directory.GetCurrentDirectory();
        public bool isDarkMode { get; set; } = false;

        public bool EnableHighlighting { get; set; } = true;
        private string _lastRenderedText = string.Empty;

        private System.Windows.Forms.Timer _scrollWatcher = null!;
        private int _lastScrollPos = -1;
        public NoteEditorPanel()
        {
            _markdownConverter = new MarkdownConverter();
            InitializePanelControls();
            _ = InitializeWebView2Async();
            // ★ 追加: 検索パネルの初期化
            InitializeSearchUI();

            // イベント登録
            EditorTextBox.TextChanged += EditorTextBox_TextChanged;
            EditorTextBox.KeyDown += EditorTextBox_KeyDown;
            // WebView2の初期化を開始（待たない＝Fire and Forget）
            // コンストラクタをブロックしないために、あえてawaitしない
            // ★ マウスホイールイベントを追加
            this.EditorTextBox.MouseWheel += EditorTextBox_MouseWheel;
        }

        private void InitializeSearchUI()
        {
            // 1. 検索パネルの作成 (最初は非表示)
            _searchPanel = new Panel
            {
                Dock = DockStyle.Top, // エディタの上に配置
                Height = 35,
                BackColor = Color.WhiteSmoke,
                Visible = false, // デフォルトは非表示
                Padding = new Padding(5),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 2. UI部品の作成
            var label = new Label { Text = "検索:", AutoSize = true, Location = new Point(5, 8) };

            _searchTextBox = new TextBox { Location = new Point(45, 5), Width = 200 };
            _searchTextBox.KeyDown += SearchTextBox_KeyDown; // Enterで検索するためのイベント
                                                             // ★ 追加: 文字が変わるたびにインクリメンタルサーチを実行
                                                             // 第2引数を true (isTyping) にするのがポイント
            _searchTextBox.TextChanged += (s, e) => PerformSearch(true, true);

            _btnNext = new Button { Text = "次へ(↓)", Location = new Point(255, 4), Width = 60, Height = 25 };
            _btnNext.Click += (s, e) => PerformSearch(true,false); // true = 次へ

            _btnPrev = new Button { Text = "前へ(↑)", Location = new Point(320, 4), Width = 60, Height = 25 };
            _btnPrev.Click += (s, e) => PerformSearch(false,false); // false = 前へ

            _btnClose = new Button { Text = "×", Location = new Point(390, 4), Width = 25, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => HideSearchPanel();

            _lblStatus = new Label { Text = "", AutoSize = true, Location = new Point(425, 8), ForeColor = Color.Red };

            // 3. パネルに追加
            _searchPanel.Controls.AddRange(new Control[] { label, _searchTextBox, _btnNext, _btnPrev, _btnClose, _lblStatus });

            // 4. エディタパネル（TextBoxがある場所）に追加
            // EditorTextBoxより先に追加することで、Dock=Topが効いて上に表示される
            this.SplitterContainer.Panel1.Controls.Add(_searchPanel);
            // 順序調整：Panelを一番上に持ってくる（Controlsのインデックス0にする）
            this.SplitterContainer.Panel1.Controls.SetChildIndex(_searchPanel, 0);
        }

        /// <summary>
        /// エディタ上でのキー操作イベント
        /// </summary>
        private void EditorTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            // Ctrl + F で検索パネルを表示
            if (e.Control && e.KeyCode == Keys.F)
            {
                e.SuppressKeyPress = true; // ビープ音などを防ぐ
                ShowSearchPanel();
            }
            // Ctrl + V が押されたら...
            if (e.Control && e.KeyCode == Keys.V)
            {
                // クリップボードに画像があるかチェック
                if (Clipboard.ContainsImage())
                {
                    // 画像を取得
                    Image? img = Clipboard.GetImage();
                    if (img != null)
                    {
                        // 標準の貼り付け処理をキャンセル（これをしないと画像がそのままRichTextBoxに入ろうとして変になる）
                        e.SuppressKeyPress = true;
                        e.Handled = true;

                        // 親(Form1)に「画像来たわよ！」と伝える
                        ImagePasteRequested?.Invoke(this, img);

                        img.Dispose();
                    }
                }
                // テキストの場合は何もしない（標準のペースト動作に任せる）
            }
        }

        /// <summary>
        /// 検索ボックス上でのキー操作イベント
        /// </summary>
        private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                // Shift+Enterなら前へ、Enterなら次へ
                // 第2引数は false (入力中ではなく移動アクション)
                PerformSearch(!e.Shift, false);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HideSearchPanel();
            }
            if (e.KeyCode == Keys.Tab && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true; // 元の「\t」入力をキャンセル

                if (e.Shift)
                {
                    // Shift + Tab: 逆インデント（カーソル直前のスペースを削除）
                    RemoveIndentation();
                }
                else
                {
                    // Tab: スペース4つを挿入
                    InsertText("    ");
                }
            }
        }

        // ★追加: 簡易逆インデント機能
        // カーソルの直前にあるスペースを最大4つまで削除する
        private void RemoveIndentation()
        {
            int start = EditorTextBox.SelectionStart;
            if (start == 0) return;

            // テキスト全体を取得せずに済むよう、少し工夫してもいいけど
            // RichTextBoxはTextプロパティへのアクセスが少し重いので注意。
            // ここではシンプルに現在のテキストを参照するわ。
            string text = EditorTextBox.Text;

            int spacesToDelete = 0;

            // 最大4文字遡ってチェック
            for (int i = 1; i <= 4; i++)
            {
                int idx = start - i;
                if (idx < 0) break;

                if (text[idx] == ' ') spacesToDelete++;
                else break; // スペース以外に当たったらそこでストップ
            }

            if (spacesToDelete > 0)
            {
                // 削除実行
                EditorTextBox.Select(start - spacesToDelete, spacesToDelete);
                EditorTextBox.SelectedText = "";
            }
        }

        private void ShowSearchPanel()
        {
            _searchPanel.Visible = true;
            _searchTextBox.Focus();
            _searchTextBox.SelectAll(); // 入力済みの文字があれば全選択
        }

        private void HideSearchPanel()
        {
            _searchPanel.Visible = false;
            EditorTextBox.Focus(); // エディタにフォーカスを戻す
        }
        /// <summary>
        /// 検索のコアロジック
        /// </summary>
        /// <param name="searchNext">trueなら下方向、falseなら上方向</param>
        /// <param name="isTyping">trueなら入力中のリアルタイム検索（現在の選択範囲を含めて検索）</param>
        private void PerformSearch(bool searchNext, bool isTyping = false)
        {
            string keyword = _searchTextBox.Text;

            // キーワードが空になったら、検索状態（ステータス）をリセットして終わる
            if (string.IsNullOrEmpty(keyword))
            {
                _lblStatus.Text = "";
                return;
            }

            string content = EditorTextBox.Text;
            int startIndex;
            int foundIndex = -1;

            StringComparison comparison = StringComparison.OrdinalIgnoreCase;

            if (searchNext)
            {
                // --- 次を検索 (下方向) ---
                if (isTyping)
                {
                    // ★ここが修正点：入力中は「現在のカーソル位置（選択範囲の先頭）」から探す
                    // こうしないと "app" と打った時、"a" が選択された状態で "p" を打つと "pp" を後ろに探しに行ってしまう
                    startIndex = EditorTextBox.SelectionStart;
                }
                else
                {
                    // ボタン押下時は「選択範囲の末尾」から探して、次へ進む
                    startIndex = EditorTextBox.SelectionStart + EditorTextBox.SelectionLength;
                }

                foundIndex = content.IndexOf(keyword, startIndex, comparison);

                // 見つからない場合、先頭から再検索 (Wrap around)
                if (foundIndex == -1)
                {
                    foundIndex = content.IndexOf(keyword, 0, comparison);
                    _lblStatus.Text = "先頭から検索";
                }
                else
                {
                    _lblStatus.Text = "";
                }
            }
            else
            {
                // --- 前を検索 (上方向) ---
                // 前検索はボタン操作のみ想定なので、ロジックは昨日のまま
                startIndex = EditorTextBox.SelectionStart - 1;
                if (startIndex < 0) startIndex = content.Length;

                foundIndex = content.LastIndexOf(keyword, startIndex, comparison);

                if (foundIndex == -1)
                {
                    foundIndex = content.LastIndexOf(keyword, content.Length - 1, comparison);
                    _lblStatus.Text = "末尾から検索";
                }
                else
                {
                    _lblStatus.Text = "";
                }
            }

            // 結果の処理
            if (foundIndex != -1)
            {
                EditorTextBox.Select(foundIndex, keyword.Length);
                EditorTextBox.ScrollToCaret();
            }
            else
            {
                // 入力中で見つからない場合は、赤字で教えてあげると親切ね
                _lblStatus.Text = "見つかりません";
            }
        }

        /// <summary>
        /// 非同期でWebView2を初期化し、完了シグナルを出す
        /// </summary>
        private async Task InitializeWebView2Async()
        {
            try
            {
                // エンジンの初期化
                await PreviewWebView2.EnsureCoreWebView2Async(null);

                // 設定（JavaScript無効化など）
                if (PreviewWebView2.CoreWebView2 != null)
                {
                    var settings = PreviewWebView2.CoreWebView2.Settings;
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
                    //PreviewWebView2.CoreWebView2.Settings.IsScriptEnabled = false;
                    // PreviewWebView2.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
                    // ★ セキュリティの壁を越える魔法の1行
                    // "https://app.assets/" へのアクセスを、カレントディレクトリへのアクセスに変換する
                    // これでローカル画像が表示できるようになるわ。
                    //PreviewWebView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    //    VIRTUAL_HOST_NAME,
                    //    BasePath,
                    //    CoreWebView2HostResourceAccessKind.Allow
                    //);
                    PreviewWebView2.CoreWebView2.AddWebResourceRequestedFilter(
                    $"{VIRTUAL_HOST_URL}*",
                    CoreWebView2WebResourceContext.All
                );

                    // イベントハンドラでファイルを返す
                    PreviewWebView2.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                    PreviewWebView2.CoreWebView2.Navigate("about:blank");
                }

                // ★ 初期化完了を通知！これでゲートが開くわ。
                _webViewInitializedTcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                // 初期化に失敗した場合も、永久に待たせないように例外をセットするか、falseを返す
                MessageBox.Show($"WebView2の初期化に失敗しました。\n{ex.Message}");
                _webViewInitializedTcs.TrySetResult(false);
            }
            PreviewWebView2.NavigationStarting += PreviewWebView2_NavigationStarting;
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
                    string headers = $"Content-Type: {mimeType}\nCache-Control: public, max-age=31536000";
                    // 200 OK を返す
                    e.Response = PreviewWebView2.CoreWebView2.Environment.CreateWebResourceResponse(
                        stream,
                        200,
                        "OK",
                        $"Content-Type: {mimeType}\r\n" + 
                        headers
                    );
                }
                catch
                {
                    // エラー時は 404 Not Found
                    e.Response = PreviewWebView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
                }
            }
            else
            {
                e.Response = PreviewWebView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
            }
        }
        private void PreviewWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // ここが戦略の分かれ目。
            // 「http」や「https」で始まるリンクだけを外部で開くように制限する。
            // これをしないと、ローカルのHTMLを表示しようとした時までキャンセルされて真っ白になるわ。
            if (e.Uri.StartsWith("http://") || e.Uri.StartsWith("https://"))
            {
                //if(e.Uri.IndexOf("abehiroshi") >= 0)
                //{
                //    return;
                //}else 
                    // 1. WebView2内での遷移をキャンセル
                    e.Cancel = true;

                // 2. デフォルトブラウザでURLを開く
                // .NET Core/.NET 5以降は UseShellExecute = true が必須
                try
                {
                    Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                }
                catch (System.Exception ex)
                {
                    // ブラウザが見つからない等のエラーハンドリングは必須。
                    // 黙って落ちるアプリは二流よ。
                    Debug.WriteLine($"外部ブラウザ起動エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// テキスト変更時のイベントハンドラ（Debounce処理）
        /// </summary>
        private void EditorTextBox_TextChanged(object? sender, EventArgs e)
        {
            // 1. まず、外部（Form1）への通知は即座に行う（保存フラグのため）
            DocumentContentChanged?.Invoke(this, EventArgs.Empty);

            // 2. プレビュー更新のタイマーをリセットする
            // 既存のタイマーがあれば停止し、新しい時間をセットする
            _debounceTimer?.Change(DEBOUNCE_DELAY_MS, Timeout.Infinite);
        }

        /// <summary>
        /// タイマーから呼ばれるコールバック（別スレッドで実行される）
        /// </summary>
        private void OnDebounceTimerTick(object? state)
        {
            // UIスレッドで実行する必要があるため、Invokeを使用するわ
            // Disposedされていたら何もしない（アプリ終了時の例外防止）
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.Invoke((Action)(async () =>
            {
                await UpdatePreviewAsync(EditorTextBox.Text);
                if (EditorTextBox.IsImeComposing)
                {
                    // まだ変換中だから塗らない。
                    // ただし、変換確定後に塗られないと困るから、タイマーを少し延長して再予約する。
                    _debounceTimer.Change(500, System.Threading.Timeout.Infinite);
                    return;
                }
                bool isDark = EditorTextBox.BackColor.R < 128;
                ApplySyntaxHighlighting(isDark);
            }));
        }

        /// <summary>
        /// 非同期でプレビューを更新する
        /// </summary>
        /// 

        bool needsFullReload = true;
        private async Task UpdatePreviewAsync(string markdown)
        {
            // 1. 初期化チェック
            bool isReady = await _webViewInitializedTcs.Task;
            if (!isReady || PreviewWebView2.CoreWebView2 == null) return;

            // 2. ★重要: テキストが変わってないなら何もしない（無駄な処理をカット）
            if (markdown == _lastRenderedText) return;
            _lastRenderedText = markdown;

            // 3. 状況に応じて更新方法を変える

            bool isDark = EditorTextBox.BackColor.R < 128; // 簡易判定
            if (needsFullReload)
            {
                // A. フルロード (NavigateToString)
                // 現在のテーマ状態を取得する必要があるわね。
                // 簡易的に背景色で判定するか、外部から IsDarkMode を渡す設計にするか。
                // ここでは「とりあえず更新」するわ。
                string html = _markdownConverter.ToHtml(markdown, VIRTUAL_HOST_URL, isDark);
                PreviewWebView2.CoreWebView2.NavigateToString(html);
                needsFullReload = false; // 次回からはスマート更新に切り替え
            }
            else
            {
                // B. スマート更新 (JavaScript Injection)
                // ボディの中身だけ変換
                string htmlBody = _markdownConverter.ToHtml(markdown, VIRTUAL_HOST_URL, isDark , false);

                // ★超重要: C#の文字列をJSに渡すときは、必ずJSONエスケープすること！
                // これをしないと、本文に " とか改行が入った瞬間にJSエラーで死ぬわ。
                string safeJson = JsonSerializer.Serialize(htmlBody);

                // 定義しておいた window.updateContent 関数を呼ぶ
                // ExecuteScriptAsync は非同期でJSを実行するわ
                await PreviewWebView2.CoreWebView2.ExecuteScriptAsync($"window.updateContent({safeJson});");
            }
        }

        // ...

        // 強制リフレッシュ用（テーマ切り替え時などに呼ぶ）
        public void ForceRefreshPreview()
        {
            _lastRenderedText = ""; // 強制的に更新させるためにリセット
                                    // Debounceタイマーを即時発火させるか、直接呼ぶ
            OnDebounceTimerTick(null);
        }

        public void DisplayDocument(MarkdownDocument doc)
        {
            // テキストボックスへの代入はTextChangedを発火させる。
            // つまり、自動的にDebounce経由でプレビューも更新されるわ。
            EditorTextBox.Text = doc.Content;

            // ただし、ファイルを開いた直後は即座にプレビューを見せたい場合もあるわね。
            // その場合はここで直接呼んでもいいけれど、基本はイベントに任せればいいわ。
        }

        public string GetCurrentEditorText()
        {
            return EditorTextBox.Text;
        }

        private void InitializePanelControls()
        {
            // ... (UI生成コードは変更なし) ...
            this.Dock = DockStyle.Fill;
            this.downContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(this.downContainer);
            this.SplitterContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 450,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.downContainer.Panel1.Controls.Add(this.SplitterContainer);

            this.EditorTextBox = new ExRichTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 10F),
                BackColor = Color.White,
                AllowDrop = true,
                AcceptsTab = true

            };
            this.EditorTextBox.DragEnter += EditorTextBox_DragEnter;
            this.EditorTextBox.DragDrop += EditorTextBox_DragDrop;
            this.SplitterContainer.Panel1.Controls.Add(this.EditorTextBox);

            this.PreviewWebView2 = new WebView2
            {
                Dock = DockStyle.Fill
            };
            this.SplitterContainer.Panel2.Controls.Add(PreviewWebView2);


            this.ConsolePanel = new EmbeddedTerminalPanel // 生成変更
            {
                Dock = DockStyle.Fill
            };
            this.downContainer.Panel2.Controls.Add(this.ConsolePanel);
            // タイマーのインスタンス化（最初は停止状態）
            _debounceTimer = new System.Threading.Timer(OnDebounceTimerTick, null, Timeout.Infinite, Timeout.Infinite);

            _scrollWatcher = new System.Windows.Forms.Timer();
            _scrollWatcher.Interval = 50;
            _scrollWatcher.Tick += ScrollWatcher_Tick;
            _scrollWatcher.Start(); // 常に回しておく
        }

        // --- ドラッグ＆ドロップの実装 ---

        private void EditorTextBox_DragEnter(object? sender, DragEventArgs e)
        {
            // ファイルがドラッグされている場合のみ受け入れる
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void EditorTextBox_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            object? data = e.Data.GetData(DataFormats.FileDrop);
            if(data == null) return;
            string[] files = (string[])data;
            if (files == null || files.Length == 0) return;

            string sourcePath = files[0];
            string originalFileName = Path.GetFileName(sourcePath);

            // 拡張子チェック
            string ext = Path.GetExtension(originalFileName).ToLower();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".bmp" && ext != ".webp")
            {
                return;
            }

            // 1. サニタイズ
            string safeFileName = originalFileName.Replace(" ", "_");

            // ★ 変更点: assetsフォルダのパスを構築
            string assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets");

            // ★ フォルダがなければ作成（これがHousekeepingよ）
            if (!Directory.Exists(assetsDir))
            {
                Directory.CreateDirectory(assetsDir);
            }

            // ★ 保存先パスを assets 内に変更
            string destPath = Path.Combine(assetsDir, safeFileName);

            try
            {
                if (!File.Exists(destPath))
                {
                    File.Copy(sourcePath, destPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"画像の取り込みに失敗しました: {ex.Message}");
                return;
            }

            EditorTextBox.Focus();
            int selectionIndex = EditorTextBox.SelectionStart;

            // ★ 変更点: Markdownパスに "assets/" を付与
            // これで標準的な相対パス構造になるわ。
            string insertText = $"![{originalFileName}](assets/{safeFileName})";

            EditorTextBox.Text = EditorTextBox.Text.Insert(selectionIndex, insertText);
            EditorTextBox.SelectionStart = selectionIndex + insertText.Length;
        }

        // UserControlが破棄されるときの処理（重要）
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // タイマーを確実に破棄する。これを忘れるとメモリリークの原因になるわ。
                _scrollWatcher?.Stop();
                _scrollWatcher?.Dispose();
                _debounceTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        public void Clear()
        {
            EditorTextBox.Text = "";
        }

        /// <summary>
        /// 外部からフォントサイズを変更するメソッド
        /// </summary>
        public void SetFontSize(float size)
        {
            // サイズ制限（小さすぎ/大きすぎ防止）
            if (size < 8) size = 8;
            if (size > 72) size = 72;

            var currentFont = EditorTextBox.Font;
            EditorTextBox.Font = new Font(currentFont.FontFamily, size, currentFont.Style);
        }

        public float GetFontSize()
        {
            return EditorTextBox.Font.Size;
        }

        private void EditorTextBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                float currentSize = EditorTextBox.Font.Size;
                // ホイールの回転量(e.Delta)に応じて増減
                // Deltaは通常120単位。120で+2ptくらいが妥当かしら。
                float newSize = currentSize + (e.Delta > 0 ? 2f : -2f);

                SetFontSize(newSize);

                // イベントを処理したことにする（スクロールさせないため）
                ((HandledMouseEventArgs)e).Handled = true;
            }
        }
        private void NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            //新しいウィンドウを開かなくする
            e.Handled = true;
            
        }

        public void ScrollToAndHighlight(string keyword, int lineNumber)
        {
            if (string.IsNullOrEmpty(keyword)) return;

            // 1. 前回のハイライトを消す（全体を白背景に戻す）
            int originalIndex = EditorTextBox.SelectionStart;
            int originalLength = EditorTextBox.SelectionLength;

            EditorTextBox.SelectAll();
            EditorTextBox.SelectionBackColor = EditorTextBox.BackColor; // 背景色リセット
            EditorTextBox.DeselectAll();

            // 2. キーワードを検索してハイライト
            // ここでは簡易的に「ファイル先頭から検索」するわね。
            // 行番号(lineNumber)を使うなら、EditorTextBox.Lines[lineNumber-1] の位置を計算する必要があるけど、
            // RichTextBoxの Find 機能を使うのが手っ取り早いわ。

            int index = EditorTextBox.Find(keyword, RichTextBoxFinds.None);

            try
            {
                int lineIndex = lineNumber - 1;
                if (lineIndex < 0 || lineIndex >= EditorTextBox.Lines.Length) return;

                // ★修正: GetFirstCharIndexFromLine は使わない（折り返しでズレるため）
                // 代わりに、前の行までの文字数を全部足して、自力で開始位置を計算するわ。

                int lineStartIndex = 0;
                // Linesプロパティは毎回生成されるので、一度変数に受ける（パフォーマンス対策）
                var lines = EditorTextBox.Lines;

                for (int i = 0; i < lineIndex; i++)
                {
                    // 行の長さ + 改行コード(\n)の分(1文字) を足していく
                    lineStartIndex += lines[i].Length + 1;
                }

                // 4. その行のテキストを取得
                string lineText = lines[lineIndex];

                // 5. 行の中でキーワードを探す
                int indexInLine = lineText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);

                if (indexInLine >= 0)
                {
                    // 6. 絶対位置を計算して選択
                    int finalSelectionStart = lineStartIndex + indexInLine;

                    EditorTextBox.Select(finalSelectionStart, keyword.Length);
                    EditorTextBox.SelectionBackColor = Color.Yellow; // 画像では緑だけど、黄色に戻るわよ

                    EditorTextBox.ScrollToCaret();
                    EditorTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Highlight error: {ex.Message}");
            }
        }
        // UpdatePreviewAsync も修正して、ToHtml に isDark を渡すこと！
        // （ThemeServiceの参照が必要になるから、フィールドで持っておくか、引数で渡すか）

        // NoteEditorPanel.cs

        // イベント定義
        public event EventHandler<Image>? ImagePasteRequested;

       
       

        // テキスト挿入メソッド (IMainViewの実装用)
        public void InsertText(string text)
        {
            // カーソル位置に挿入
            EditorTextBox.SelectedText = text;

            // カーソル位置は自動的に進むから、手動設定は不要な場合が多いけど、
            // 念のためフォーカスとスクロールだけケアしておくわ。
            EditorTextBox.ScrollToCaret();
            EditorTextBox.Focus();
        }
        public async void SetTheme(CoreWebView2PreferredColorScheme color)
        {
            await _webViewInitializedTcs.Task;
            PreviewWebView2.CoreWebView2.Profile.PreferredColorScheme = color;
        }
        public void ApplyThemeToConsole(bool isDark)
        {
            ConsolePanel.ApplyTheme(isDark);
        }
        public async void ApplyThemeToPreview(bool isDark)
        {
            if (PreviewWebView2.CoreWebView2 != null)
            {
                // JS関数を呼ぶだけ。HTMLのリロードは発生しない！
                await PreviewWebView2.CoreWebView2.ExecuteScriptAsync($"window.setTheme({isDark.ToString().ToLower()});");
            }
        }
        public void StartConsole(string? path = null)
        {

            this.ConsolePanel.StartTerminal(path);
        }

        private void BeginUpdate()
        {
            NativeMethods.SendMessage(EditorTextBox.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUpdate()
        {
            NativeMethods.SendMessage(EditorTextBox.Handle, NativeMethods.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            EditorTextBox.Invalidate(); // 強制再描画
        }

        // ★追加: シンタックスハイライトの実行
        // 引数 isDarkMode は ThemeService から渡してもらうか、パネルで保持するか。
        // ここでは引数で受け取る設計にするわ。
        private void ApplySyntaxHighlighting(bool isDarkMode)
        {
            if (!EnableHighlighting || string.IsNullOrEmpty(EditorTextBox.Text)) return;

            // 1. カーソル位置とスクロール位置の保存（これをしないと勝手にスクロールしちゃう）
            int originalIndex = EditorTextBox.SelectionStart;
            int originalLength = EditorTextBox.SelectionLength;
            int scrollPos = NativeMethods.GetScrollPos(EditorTextBox.Handle, NativeMethods.SB_VERT);
            // ★重要: 描画停止！
            BeginUpdate();

            try
            {
                string text = EditorTextBox.Text;

                // 2. 色の定義 (ThemeServiceと合わせると良い)
                Color defaultColor = isDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black;
                Color headerColor = isDarkMode ? Color.FromArgb(86, 156, 214) : Color.Blue;      // 青
                Color boldColor = isDarkMode ? Color.FromArgb(206, 145, 120) : Color.DarkRed;    // 赤茶
                Color quoteColor = isDarkMode ? Color.FromArgb(106, 153, 85) : Color.Green;      // 緑
                Color codeColor = isDarkMode ? Color.FromArgb(220, 220, 170) : Color.Purple;     // 紫
                Color linkColor = isDarkMode ? Color.FromArgb(78, 201, 176) : Color.DarkCyan;    // 水色

                // 3. 全体を一旦リセット (デフォルト色に戻す)
                EditorTextBox.SelectAll();
                EditorTextBox.SelectionColor = defaultColor;
                // 太字などのスタイルもリセットしたい場合はここでFontも戻すけど、今回は色だけ。

                // 4. 正規表現で塗っていく
                // ※順番重要: 範囲が大きいものや、競合しそうなものをどう扱うか。
                // 今回はシンプルに上書きしていくわ。

                // 見出し (# Header)
                HighlightRegex(@"^#{1,6}\s.*$", headerColor, text);

                // 太字 (**Bold**)
                HighlightRegex(@"\*\*.*?\*\*", boldColor, text);

                // 引用 (> Quote)
                HighlightRegex(@"^>.*$", quoteColor, text);

                // コードブロック (``` ... ```) 
                // SingleLineモードにしないと複数行マッチしないので注意
                HighlightRegex(@"```[\s\S]*?```", codeColor, text);

                // インラインコード (`code`)
                HighlightRegex(@"`.*?`", codeColor, text);

                // リンク ([Title](Url))
                HighlightRegex(@"\[.*?\]\(.*?\)", linkColor, text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Highlight Error: {ex.Message}");
            }
            finally
            {
                // 5. カーソル位置を復元
                EditorTextBox.Select(originalIndex, originalLength);
                EditorTextBox.SelectionColor = isDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black; // 入力色を戻す
                NativeMethods.SetScrollPos(EditorTextBox.Handle, NativeMethods.SB_VERT, scrollPos, true);
                IntPtr wParam = (IntPtr)((scrollPos << 16) | NativeMethods.SB_THUMBPOSITION);
                NativeMethods.SendMessage(EditorTextBox.Handle, NativeMethods.WM_VSCROLL, wParam, IntPtr.Zero);
                // ★描画再開！
                EndUpdate();
            }
        }

        // 正規表現ヘルパー
        private void HighlightRegex(string pattern, Color color, string text)
        {
            // Multiline: ^と$を行頭・行末にマッチさせる
            Regex regex = new Regex(pattern, RegexOptions.Multiline);
            MatchCollection matches = regex.Matches(text);

            foreach (Match m in matches)
            {
                EditorTextBox.Select(m.Index, m.Length);
                EditorTextBox.SelectionColor = color;
            }
        }
        public void ForceHighlight(bool isDark)
        {
            ApplySyntaxHighlighting(isDark);
        }

        private async void ScrollWatcher_Tick(object? sender, EventArgs e)
        {
            if (PreviewWebView2.CoreWebView2 == null) return;

            // 1. 現在の「見た目上の」一番上の行インデックスを取得
            int firstVisibleVisualLine = (int)NativeMethods.SendMessage(EditorTextBox.Handle, NativeMethods.EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);

            // 2. その行の先頭文字が、テキスト全体の何文字目かを取得
            int charIndex = EditorTextBox.GetFirstCharIndexFromLine(firstVisibleVisualLine);

            // 3. その文字が「論理的に（Markdown的に）」何行目かを取得 (0始まり)
            int logicalLineIndex = EditorTextBox.GetLineFromCharIndex(charIndex);

            // 前回と同じ行なら何もしない
            if (logicalLineIndex == _lastScrollPos) return;
            _lastScrollPos = logicalLineIndex;

            // 4. WebView2に「この行を表示しろ」と命令 (JS側は属性値に合わせて調整、通常は0始まりか1始まりか確認)
            // Markdigのline属性は通常 0始まり よ。
            try
            {
                await PreviewWebView2.CoreWebView2.ExecuteScriptAsync($"window.syncToLine({logicalLineIndex});");
            }
            catch { }
        }
        // ★追加: 現在のプレビューをPDFとして保存
        public async Task SaveAsPdfAsync(string filePath)
        {
            if (PreviewWebView2.CoreWebView2 == null) return;

            try
            {
                // 印刷設定 (A4, 縦向きなど)
                var printSettings = PreviewWebView2.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.Orientation = CoreWebView2PrintOrientation.Portrait;
                // printSettings.ShouldPrintBackgrounds = true; // 背景色を印刷するかどうか（デフォルトfalseの場合が多いのでtrue推奨）

                // PDF生成実行
                await PreviewWebView2.CoreWebView2.PrintToPdfAsync(filePath, printSettings);
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF生成失敗: {ex.Message}");
            }
        }
    }
}