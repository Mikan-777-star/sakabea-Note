using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WinFormsApp2.NoteApp.UI;
using WinFormsApp2.Services; // MarkdownDocumentなどがここならusingが必要
using WinFormsApp2.Views;
namespace WinFormsApp2
{
    public partial class Form1 : Form, IMainView
    {
        // 必要なフィールド
        private NoteEditorPanel noteEditorPanel = null!;
        private SplitContainer outerSplitter = null!;
        private SplitContainer innerSplitter = null!;
        private SplitContainer leftSplitter = null!;
        private TreeView directoryTreeView = null!;
        private DashboardPanel dashboardPanel = null!; // 型を変更
        private ClosableTabControl noteTabControl = null!; // 型を明示
        private MenuStrip mainMenuStrip = null!;
        private Label CalendarTitleLabel = null!;
        private Label DirectoryTitleLabel = null!;

        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel statusLabel = null!;
        private DarkCalendar calendarControl = null!;
        // private System.Windows.Forms.Timer autoBackupTimer = null!;

        private SettingsService settingsService = null!;

        private ToolStripTextBox searchTextBox = null!;
        private ToolStripButton clearButton = null!;


        private Color _searchBoxTextColor = Color.Black;
        public Form1()
        {
            // 1. 標準の初期化 (Designer.csのもの)
            InitializeComponent();

            // 2. フォームの基本設定
            this.Text = "ダッシュボード型Markdownノートアプリ";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            try
            {
                this.Icon = new Icon(Path.Combine(Directory.GetCurrentDirectory(), "2AFw_K-t_400x400.ico"));
            }
            catch { /* アイコンがなくても死なないように */ }

            // 3. データ管理の初期化

            settingsService = new SettingsService();
            var settings = settingsService.Load();
            string initialPath = settings.LastWorkspacePath ?? Directory.GetCurrentDirectory();

            this.Load += Form1_Load;
            // 4. 手書きUIの構築 (Designer.csから救出したコード)
            InitializeCustomUI();
            /*
            // ★ UI構築後に設定をロードして適用！
            ApplySettings();

            // 5. イベント購読と初期データロード
            SetupEvents();
            LoadFileTree();

            // Form1.cs のコンストラクタ または LoadFileTree() の後あたり

            // 起動時にダッシュボード更新
            UpdateDashboardWithToday();

            CheckForBackups();

            // 6. ウェルカムドキュメントの表示
            OpenWelcomeDocument();
            */
        }

        // IMainViewの実装部分


        // --- IMainView の実装 ---

        // 1. イベントの定義 (Presenterが購読する)
        public event EventHandler LoadRequested;
        public event EventHandler<string> FileSelected;
        public event EventHandler SaveRequested;
        public event EventHandler NewFileRequested;
        public event EventHandler<DateTime> DateSelected;
        public event EventHandler ActiveDocumentChanged;
        public event EventHandler EditorContentChanged;
        public event EventHandler<CancelEventArgs> CloseRequested;
        public event EventHandler<string> GlobalSearchRequested;
        public event EventHandler<LinkClickedEventArgs> DashboardLinkClicked;
        public event EventHandler SearchClearRequested;
        public event EventHandler ThemeChanged;
        public event EventHandler<Image> ImagePasteRequested;
        public event EventHandler ChangeFolderRequested;

        public event EventHandler FileTreeRefreshRequested;

        public event EventHandler ExportHtmlRequested;
        public event EventHandler ExportPdfRequested;

        private Panel _dirHeaderPanel = null!;
        private Label _dirTitleLabel = null!;
        private Button _refreshTreeButton = null!;

        // ※ IsDisposed は Form クラスが元々持っているから実装不要よ（自動的にマッチする）

        // 2. ツリー表示 (さっき教えたやつね)

        public void UpdateFileTree(IEnumerable<FileNodeModel> nodes)
        {
            // 1. ツリーをクリア
            directoryTreeView.Nodes.Clear();
            directoryTreeView.BeginUpdate(); // 描画停止（高速化のため）

            try
            {
                foreach (var modelNode in nodes)
                {
                    // ルートノードの作成
                    TreeNode viewNode = CreateTreeNode(modelNode);
                    directoryTreeView.Nodes.Add(viewNode);
                }

                // ルートレベルは展開しておく
                if (directoryTreeView.Nodes.Count > 0)
                {
                    directoryTreeView.Nodes[0].Expand();
                }
            }
            finally
            {
                directoryTreeView.EndUpdate(); // 描画再開
            }
        }

        // ヘルパーメソッド: モデルからTreeNodeを再帰的に作る
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
        // 3. タブ操作
        public void OpenDocumentTab(MarkdownDocument document)
        {
            // 既に開いているかチェックするロジックはPresenterの責任にするのが理想だけど、
            // View側で「同じタブがあったら選択する」くらいの便宜は図ってもいいわ。
            foreach (TabPage page in noteTabControl.TabPages)
            {
                if (page.Tag == document) // インスタンスが同じなら
                {
                    noteTabControl.SelectedTab = page;
                    return;
                }
            }

            // 新しいタブを作成
            TabPage newPage = new TabPage(document.GetDisplayName());
            newPage.Tag = document; // タグにドキュメントを持たせる
            noteTabControl.TabPages.Add(newPage);
            noteTabControl.SelectedTab = newPage;

            // エディタに内容を表示
            noteEditorPanel.DisplayDocument(document);
        }

        public void CloseDocumentTab(MarkdownDocument document)
        {
            foreach (TabPage page in noteTabControl.TabPages)
            {
                if (page.Tag == document)
                {
                    noteTabControl.TabPages.Remove(page);
                    return;
                }
            }
        }

        // 4. エディタ操作
        public string GetCurrentEditorContent()
        {
            return noteEditorPanel.GetCurrentEditorText();
        }

        public void SetEditorContent(string content)
        {
            // NoteEditorPanelにそういうメソッドがないなら、TextBoxを直接触るかメソッド追加ね
            // ここでは DisplayDocument を使うのが手っ取り早いか、直接セットするか。
            // 今回は暫定的にこうするわ：
            if (noteTabControl.SelectedTab?.Tag is MarkdownDocument doc)
            {
                doc.UpdateContent(content); // モデル更新（本来はPresenterの仕事だが、即時反映のため）
                noteEditorPanel.DisplayDocument(doc);
            }
        }

        // 5. ダイアログ表示
        public void ShowError(string message)
        {
            MessageBox.Show(message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowMessage(string message)
        {
            MessageBox.Show(message, "通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public bool ConfirmAction(string message)
        {
            return MessageBox.Show(message, "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // --- イベントの発火 ---
        // 既存のイベントハンドラの中身を書き換えて、イベントを発行するようにするの。

        // フォームロード時
        private void Form1_Load(object sender, EventArgs e)
        {
            // Presenterに「準備できたわよ」と伝える
            LoadRequested?.Invoke(this, EventArgs.Empty);
        }

        // ツリークリック時 (名前はDesignerの設定に合わせてね)
        private void DirectoryTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is string path)
            {
                if(e.Node.ImageKey == "folder")
                {
                    return; // フォルダクリック時は選択イベントを発行しない
                }
                // Presenterに「このファイルが選ばれたわ」と伝える
                FileSelected?.Invoke(this, path);
            }
        }

        // 保存メニュークリック時
        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            // Presenterに「保存して！」と伝える
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Designer.cs に書かれていた手書きコードをここに隔離したわ。
        /// これでデザイナーが壊れる心配はない。
        /// </summary>
        private void InitializeCustomUI()
        {
            // --- MenuStrip ---
            this.mainMenuStrip = new MenuStrip { Dock = DockStyle.Top };
            this.Controls.Add(this.mainMenuStrip);
            SetupMenu(); // メニュー構築ロジックも分離

            // --- Splitters ---
            this.outerSplitter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 300, FixedPanel = FixedPanel.Panel1 };
            this.Controls.Add(this.outerSplitter);
            this.outerSplitter.BringToFront();

            this.leftSplitter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 200 };
            this.outerSplitter.Panel1.Controls.Add(this.leftSplitter);

            this.innerSplitter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 700, FixedPanel = FixedPanel.Panel2 };
            this.outerSplitter.Panel2.Controls.Add(this.innerSplitter);

            // --- Left Panel Controls (Calendar / Tree) ---
            this.CalendarTitleLabel = CreateHeaderLabel("📅 カレンダー内容");
            this.leftSplitter.Panel1.Controls.Add(this.CalendarTitleLabel);

            this.calendarControl = new DarkCalendar { Dock = DockStyle.Fill };
            this.calendarControl.DateSelected += CalendarControl_DateSelected;
            this.leftSplitter.Panel1.Controls.Add(this.calendarControl);
            this.calendarControl.BringToFront();

            //this.DirectoryTitleLabel = CreateHeaderLabel("📂 ノートディレクトリ");
            //this.leftSplitter.Panel2.Controls.Add(this.DirectoryTitleLabel);
            // 1. ヘッダーパネル作成
            _dirHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.LightCyan, // 初期色
                Padding = new Padding(0)
            };

            // 2. 更新ボタン作成 (右寄せ)
            _refreshTreeButton = new Button
            {
                Text = "↻", // リロード記号
                Dock = DockStyle.Right,
                Width = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Symbol", 12F, FontStyle.Bold) // 記号が綺麗に出るフォント
            };
            _refreshTreeButton.FlatAppearance.BorderSize = 0;
            _refreshTreeButton.Click += (s, e) => FileTreeRefreshRequested?.Invoke(this, EventArgs.Empty); // イベント発火

            // 3. タイトルラベル作成 (残りスペースを埋める)
            _dirTitleLabel = new Label
            {
                Text = "📂 ノートディレクトリ",
                Dock = DockStyle.Fill,
                Font = new Font("Meiryo UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            // 4. パネルに配置 (Dock=Rightのボタンを先に追加するのがコツよ)
            _dirHeaderPanel.Controls.Add(_refreshTreeButton);
            _dirHeaderPanel.Controls.Add(_dirTitleLabel);

            // 5. 左ペインに追加
            this.leftSplitter.Panel2.Controls.Add(_dirHeaderPanel);
            this.directoryTreeView = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
            this.directoryTreeView.NodeMouseClick += DirectoryTreeView_NodeMouseClick;
            this.leftSplitter.Panel2.Controls.Add(this.directoryTreeView);
            this.directoryTreeView.BringToFront();

            // --- Center Panel (Tab & Editor) ---
            this.noteTabControl = new ClosableTabControl { Dock = DockStyle.Top, Height = 25, Padding = new Point(10, 3) };
            this.innerSplitter.Panel1.Controls.Add(this.noteTabControl);
            this.noteTabControl.Deselecting += NoteTabControl_Deselecting;
            this.noteTabControl.SelectedIndexChanged += NoteTabControl_SelectedIndexChanged;
            // NoteEditorPanelの生成
            this.noteEditorPanel = new NoteEditorPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 30, 0, 0) };
            this.innerSplitter.Panel1.Controls.Add(this.noteEditorPanel);
            this.noteEditorPanel.DocumentContentChanged += NoteEditorPanel_DocumentContentChanged; 
            this.noteEditorPanel.ImagePasteRequested += (s, img) => ImagePasteRequested?.Invoke(this, img);
            //this.noteEditorPanel.BringToFront(); // タブより手前に来ないように注意、順序はAdd順に依存するが念のため確認が必要

            // --- Right Panel (Info) ---
            //this.rightInfoPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.MistyRose, Padding = new Padding(10) };
            //this.rightInfoPanel.Controls.Add(new Label { Text = "本日の予定", Dock = DockStyle.Top, Font = new Font("Meiryo UI", 12F, FontStyle.Bold), Height = 30 });
            //this.innerSplitter.Panel2.Controls.Add(this.rightInfoPanel);
            this.dashboardPanel = new DashboardPanel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            this.innerSplitter.Panel2.Controls.Add(this.dashboardPanel);
            this.searchTextBox = new ToolStripTextBox();
            this.searchTextBox.Alignment = ToolStripItemAlignment.Right;
            this.searchTextBox.Size = new Size(200, 25);
            this.searchTextBox.Text = "検索...";
            this.searchTextBox.ForeColor = Color.Gray;

            // Enterキーで検索
            this.searchTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(this.searchTextBox.Text))
                {
                    GlobalSearchRequested?.Invoke(this, this.searchTextBox.Text);
                    e.SuppressKeyPress = true;
                }
            };

            // プレースホルダー処理
            this.searchTextBox.Enter += (s, e) => { if (this.searchTextBox.Text == "検索...") { this.searchTextBox.Text = ""; this.searchTextBox.ForeColor = _searchBoxTextColor; } };
            this.searchTextBox.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(this.searchTextBox.Text)) { this.searchTextBox.Text = "検索..."; this.searchTextBox.ForeColor = Color.Gray; } };

            // ★追加: ×ボタン
            this.clearButton = new ToolStripButton("×");
            clearButton.Alignment = ToolStripItemAlignment.Right;
            clearButton.ToolTipText = "検索をクリアして閉じる";
            clearButton.DisplayStyle = ToolStripItemDisplayStyle.Text; // 文字のみ表示
            clearButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold); // 太字で見やすく

            // クリックイベント
            clearButton.Click += (s, e) =>
            {
                SearchClearRequested?.Invoke(this, EventArgs.Empty);
            };

            // 順番が大事よ。「右寄せ」同士の場合、先に追加した方が「より右」に行くか「左」に来るかはライブラリ次第だけど、
            // 通常は Right属性のアイテムは追加順に左へ並んでいくことが多いわ。
            // [×] [検索ボックス] という並びにしたいなら、×を先に追加するか、順序を調整して。
            // ここでは [検索ボックス] [×] の順に左から並ぶように、追加順序を意識するわ。

            this.mainMenuStrip.Items.Add(clearButton); // 先に×を追加（一番右）
            this.mainMenuStrip.Items.Add(this.searchTextBox); // 次に検索箱（その左）


            this.statusStrip = new StatusStrip();
            this.statusLabel = new ToolStripStatusLabel
            {
                Text = "Ready",
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.statusStrip.Items.Add(this.statusLabel);

            // フォームに追加（Dock=Bottomなので、他のコントロールより先に追加してもいいけど、
            // WinFormsのDock順序は「後から追加したものが内側」になるから、
            // 最下部に張り付けたいなら、他のDock=Fillなコントロールより「前」に追加しないと隠れる可能性があるわ。
            // でも一番安全なのは、Controls.Add の順番を最後にすることね。）

            this.Controls.Add(this.statusStrip);
            // ※もしステータスバーが埋もれて見えない場合は、
            //this.Controls.SetChildIndex(this.statusStrip, 0); //などを試して最前面に持ってくること。
            // --- イベント購読 ---
            // ダッシュボードのリンククリックを中継
            this.dashboardPanel.LinkClicked += (s, path) => DashboardLinkClicked?.Invoke(this, path);
            this.FormClosing += Form1_FormClosing;
            // 1. マネージャー初期化
            /**
            // 2. 自動バックアップタイマーの設定
            autoBackupTimer = new System.Windows.Forms.Timer();
            autoBackupTimer.Interval = 60000; // 60秒ごとに実行（お好みで短くしてもいいわ）
            autoBackupTimer.Tick += AutoBackupTimer_Tick;
            autoBackupTimer.Start();
            */
        }

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

        }
        // 1. UI部品を作るだけのヘルパーメソッド (これはViewにあって正解)
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

        // 2. フォントサイズ変更 (UI操作なのでViewにあってOK)
        private void ChangeFontSize(float delta)
        {
            float current = noteEditorPanel.GetFontSize();
            noteEditorPanel.SetFontSize(current + delta);
        }

        // 3. ロジック呼び出しのスタブ (Presenterへの橋渡し)

        // 保存処理
        private void SaveActiveDocument(bool forceSaveAs)
        {
            // 本来は forceSaveAs の情報をPresenterに渡すべきだけど、
            // 今はとりあえず「保存」イベントを発火させるわ
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        // 新規作成
        private void OpenNewDocument()
        {
            // Presenterに「新しい紙をちょうだい！」と頼む
            NewFileRequested?.Invoke(this, EventArgs.Empty);
        }

       


        // カレンダー選択
        // カレンダー選択イベントハンドラ
        private void CalendarControl_DateSelected(object? sender, DateTime date)
        {
            // Presenterに「この日が選ばれたわよ！」と伝えるだけ。
            // ファイルがあるかとか、テンプレートがどうとか、一切考えない。
            DateSelected?.Invoke(this, date);
        }

        // 自動バックアップタイマー
        private void AutoBackupTimer_Tick(object? sender, EventArgs e)
        {
            // 自動保存も「保存リクエスト」の一種とみなしてPresenterに通知
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
        public MarkdownDocument? GetActiveDocument()
        {
            if (noteTabControl.SelectedTab != null && noteTabControl.SelectedTab.Tag is MarkdownDocument doc)
            {
                return doc;
            }
            return null;
        }

        // IMainViewの実装
        public string? AskUserForSavePath(string defaultFileName)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*";
                sfd.FileName = defaultFileName;
                // sfd.InitialDirectory = ... // 必要なら前回のパスなどを設定

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    return sfd.FileName;
                }
            }
            return null; // キャンセル時
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

        public void UpdateDashboard(string title, string content)
        {
            dashboardPanel.UpdateDashboard(title, content);
        }
        private void NoteEditorPanel_DocumentContentChanged(object? sender, EventArgs e)
        {
            // Presenterに「文字が変わったわよ」と伝える
            EditorContentChanged?.Invoke(this, EventArgs.Empty);
        }
        public bool TrySelectTab(string filePath)
        {
            // 正規化（念のためフルパスにしておく）
            string targetPath = System.IO.Path.GetFullPath(filePath);

            foreach (TabPage page in noteTabControl.TabPages)
            {
                // タグに入っているMarkdownDocumentを取り出す
                if (page.Tag is MarkdownDocument doc)
                {
                    // Untitledのやつはパスがないからスキップ
                    if (doc.IsUntitled) continue;

                    // パス比較 (大文字小文字を無視)
                    if (string.Equals(System.IO.Path.GetFullPath(doc.FilePath), targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // 見つけた！そのタブを選択状態にする
                        noteTabControl.SelectedTab = page;
                        return true; // 「あったよ！」と報告
                    }
                }
            }

            return false; // 「なかったわ」
        }

        // IMainViewの実装

        public IEnumerable<MarkdownDocument> GetAllDocuments()
        {
            var list = new List<MarkdownDocument>();
            foreach (TabPage page in noteTabControl.TabPages)
            {
                if (page.Tag is MarkdownDocument doc)
                {
                    list.Add(doc);
                }
            }
            return list;
        }

        public void InvokeOnUI(Action action)
        {
            // フォームが既に死んでいたら何もしない
            if (this.IsDisposed || !this.IsHandleCreated) return;

            // UIスレッド上で実行
            this.Invoke(action);
        }

        public void UpdateTabTitle(MarkdownDocument document)
        {
            foreach (TabPage page in noteTabControl.TabPages)
            {
                // Tagに入っているドキュメントと一致するか確認
                if (page.Tag == document)
                {
                    // GetDisplayName() は "タイトル *" または "タイトル" を返すはずよね
                    string newTitle = document.GetDisplayName();

                    // チラつき防止: 文字列が変わった時だけセットする
                    if (page.Text != newTitle)
                    {
                        page.Text = newTitle;
                    }
                    return;
                }
            }
        }
        // 1. 終了リクエストのイベント発火
        // (Designerのプロパティ画面で、FormClosingイベントにこのメソッドを割り当てるか、コンストラクタで += すること！)
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Presenterに「閉じていいか？」と聞く。
            // 引数の e (CancelEventArgs) を渡すことで、Presenter側で e.Cancel = true できるようにする。
            CloseRequested?.Invoke(this, e);
        }

        // 2. 設定の適用 (起動時)
        public void SetWindowSettings(AppSettings settings)
        {
            // サイズ復元ロジック
            if (settings.Width > 0 && settings.Height > 0)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(settings.X, settings.Y);
                this.Size = new Size(settings.Width, settings.Height);
            }

            // 最大化状態の復元
            if (settings.WindowState != FormWindowState.Minimized)
            {
                this.WindowState = settings.WindowState;
            }

            // スプリッター位置の復元
            try
            {
                if (settings.OuterSplitterDistance > 0) outerSplitter.SplitterDistance = settings.OuterSplitterDistance;
                if (settings.LeftSplitterDistance > 0) leftSplitter.SplitterDistance = settings.LeftSplitterDistance;
                if (settings.InnerSplitterDistance > 0) innerSplitter.SplitterDistance = settings.InnerSplitterDistance;
            }
            catch { /* 無視 */ }
        }

        // 3. 設定の取得 (終了時)
        public AppSettings GetWindowSettings()
        {
            var settings = new AppSettings();

            // ウィンドウ状態
            settings.WindowState = this.WindowState;

            // 位置とサイズ (最大化時は RestoreBounds を使うのが鉄則)
            if (this.WindowState == FormWindowState.Normal)
            {
                settings.X = this.Location.X;
                settings.Y = this.Location.Y;
                settings.Width = this.Size.Width;
                settings.Height = this.Size.Height;
            }
            else
            {
                settings.X = this.RestoreBounds.X;
                settings.Y = this.RestoreBounds.Y;
                settings.Width = this.RestoreBounds.Width;
                settings.Height = this.RestoreBounds.Height;
            }

            // スプリッター位置
            settings.OuterSplitterDistance = outerSplitter.SplitterDistance;
            settings.LeftSplitterDistance = leftSplitter.SplitterDistance;
            settings.InnerSplitterDistance = innerSplitter.SplitterDistance;

            // 最後に開いていたパスも保存したいならここでFileManagerから取得して入れる
            // settings.LastWorkspacePath = ... (今回はPresenterでFileManagerから取るから空でいいわ)

            return settings;
        }
        public void HighlightEditorText(string keyword, int line)
        {
            // 行番号は今回は省略して、キーワード検索で飛ぶ
            noteEditorPanel.ScrollToAndHighlight(keyword, line);
        }

        public void SetStatusMessage(string message)
        {
            // UIスレッドで安全に更新
            if (this.statusStrip.InvokeRequired)
            {
                this.Invoke(new Action(() => this.statusLabel.Text = message));
            }
            else
            {
                this.statusLabel.Text = message;
            }
        }

        public void ClearSearchBox()
        {
            this.searchTextBox.Text = "";
            // プレースホルダー状態に戻すならここで行う
            this.searchTextBox.ForeColor = Color.Black;
        }

        // Form1.cs

        public void ApplyTheme(ThemeService theme)
        {

            // 1. フォーム全体
            this.BackColor = theme.BackColor;
            this.ForeColor = theme.ForeColor;

            // 2. メニューバーとステータスバー
            // WinFormsのメニューは RenderMode を System にしないと色が変えにくい場合があるけど
            // プロパティ設定で頑張るわ
            mainMenuStrip.RenderMode = ToolStripRenderMode.System;
            statusStrip.RenderMode = ToolStripRenderMode.System;

            mainMenuStrip.BackColor = theme.ControlBackColor;
            mainMenuStrip.ForeColor = theme.ForeColor;
            statusStrip.BackColor = theme.ControlBackColor;
            statusStrip.ForeColor = theme.ForeColor;

            // 3. ツリービュー
            directoryTreeView.BackColor = theme.BackColor;
            directoryTreeView.ForeColor = theme.ForeColor;

            // 4. エディタパネル (NoteEditorPanelにメソッド追加が必要ね)
            // noteEditorPanel.ApplyTheme(theme); を呼ぶ形にする
            noteEditorPanel.EditorTextBox.BackColor = theme.BackColor;
            noteEditorPanel.EditorTextBox.ForeColor = theme.ForeColor;
            noteEditorPanel.SetTheme(theme.WebColor);
            noteEditorPanel.isDarkMode = theme.IsDarkMode;
            noteEditorPanel.ApplyThemeToConsole(theme.IsDarkMode);

            // 5. タブコントロール
            noteTabControl.CustomBackColor = theme.BackColor;
            noteTabControl.CustomForeColor = theme.ForeColor;
            noteTabControl.CustomBorderColor = theme.BorderColor;
            noteTabControl.CustomAccentColor = theme.AccentColor;
            noteTabControl.Invalidate(); // 再描画を強制！

            // 6. 各種スプリッター
            // SplitterはBackColorを設定すれば境界線の色が変わるわ
            outerSplitter.BackColor = theme.BorderColor;
            innerSplitter.BackColor = theme.BorderColor;
            leftSplitter.BackColor = theme.BorderColor;

            // パネルの中身の色も合わせる
            outerSplitter.Panel1.BackColor = theme.BackColor;
            outerSplitter.Panel2.BackColor = theme.BackColor;

            //ダッシュボードパネル色変更
            dashboardPanel.BackColor = theme.BackColor;
            dashboardPanel.ForeColor = theme.ForeColor;
            dashboardPanel.SetTheme(theme.WebColor, theme.IsDarkMode);
            dashboardPanel.isDarkMode = theme.IsDarkMode;

            // 7. ラベル類
            CalendarTitleLabel.BackColor = theme.TitleLabelColor;

            // 8. カレンダー
            calendarControl.ApplyTheme(theme);

            //9. 検索欄
            if (theme.IsDarkMode)
            {
                searchTextBox.BackColor = Color.FromArgb(50, 50, 50);
                searchTextBox.BorderStyle = BorderStyle.FixedSingle;
                _searchBoxTextColor = Color.White; // ★黒モードなら文字は白
            }
            else
            {
                searchTextBox.BackColor = Color.White;
                searchTextBox.BorderStyle = BorderStyle.Fixed3D;
                _searchBoxTextColor = Color.Black; // ★白モードなら文字は黒
            }

            // ★重要: 今の状態に合わせて即座に色を反映
            if (searchTextBox.Text == "検索...")
            {
                // プレースホルダー表示中なら、色はグレーで固定
                searchTextBox.ForeColor = Color.Gray;
            }
            else
            {
                // 入力済みなら、正しい文字色を適用
                searchTextBox.ForeColor = _searchBoxTextColor;
            }

            clearButton.ForeColor = theme.ForeColor; 
            
            _dirHeaderPanel.BackColor = theme.IsDarkMode ? theme.ControlBackColor : Color.LightCyan;
            _dirTitleLabel.ForeColor = theme.ForeColor;

            _refreshTreeButton.ForeColor = theme.IsDarkMode ? theme.AccentColor : Color.DimGray;
            _refreshTreeButton.FlatAppearance.MouseOverBackColor = theme.IsDarkMode ? Color.FromArgb(60, 60, 60) : Color.White;
            _refreshTreeButton.FlatAppearance.MouseDownBackColor = theme.IsDarkMode ? Color.FromArgb(40, 40, 40) : Color.LightGray;

            // コンソールへの適用
            noteEditorPanel.ApplyThemeToConsole(theme.IsDarkMode);

            // ★追加: エディタのハイライト再適用
            noteEditorPanel.ForceHighlight(theme.IsDarkMode);

            noteEditorPanel.ApplyThemeToPreview(theme.IsDarkMode);

        }
        public void InsertTextAtCursor(string text)
        {
            noteEditorPanel.InsertText(text);
        }
        // Form1.cs

        // IMainViewの実装
        public string? AskUserForFolder(string currentPath)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "ワークスペースとして開くフォルダを選択してください";
                dialog.SelectedPath = currentPath;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        // メニュークリック時のイベント
        private void OpenWorkspaceFolder(object? sender, EventArgs e)
        {
            // MessageBox... は削除して、イベント発火！
            ChangeFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        // Form1.cs

        public void UpdateResourcePath(string newPath)
        {
            // 両方のパネルの BasePath プロパティを更新する
            // ※ NoteEditorPanel と DashboardPanel に public string BasePath { get; set; } がある前提よ
            noteEditorPanel.BasePath = newPath;
            dashboardPanel.BasePath = newPath;
        }

        public void SetResourceBasePath(string path)
        {
            noteEditorPanel.BasePath = path;
            dashboardPanel.BasePath = path;
        }

        public void StartConsole(string? path = null)
        {
            // 念のため、まだハンドルがないなら作らせる
            if (!this.IsHandleCreated) this.CreateControl();

            noteEditorPanel.StartConsole(path);
        }

        public void RestartConsole(string newPath)
        {
            noteEditorPanel.ConsolePanel.RestartTerminal(newPath);
        }

        public string? AskUserForExportPath(string defaultName, string filter)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.FileName = defaultName;
                sfd.Filter = filter;
                if (sfd.ShowDialog() == DialogResult.OK) return sfd.FileName;
            }
            return null;
        }

        public async Task ExportPdfToPath(string path)
        {
            await noteEditorPanel.SaveAsPdfAsync(path);
        }

        // Form1.cs
        public void AddPluginMenu(string path, string text, EventHandler action)
        {
            // 簡易実装: "プラグイン" メニューを作ってそこに入れる
            var pluginMenu = mainMenuStrip.Items["PluginMenu"] as ToolStripMenuItem;
            if (pluginMenu == null)
            {
                pluginMenu = new ToolStripMenuItem("プラグイン(&P)") { Name = "PluginMenu" };
                mainMenuStrip.Items.Add(pluginMenu);
            }

            var item = new ToolStripMenuItem(text);
            item.Click += action;
            pluginMenu.DropDownItems.Add(item);
        }
    }
}
