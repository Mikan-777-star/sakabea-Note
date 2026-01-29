using WinFormsApp2.NoteApp.UI; // NoteEditorPanelがUserControlになったため、usingが必要
using Microsoft.Web.WebView2.Core;
namespace WinFormsApp2
{
    partial class Form1: ModernForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>

        private void InitializeComponent()
        {/*
            this.SuspendLayout(); // コンポーネント初期化開始
            this.Text = "ダッシュボード型Markdownノートアプリ";
            this.Size = new System.Drawing.Size(1200, 700);
            this.Font = new System.Drawing.Font("Meiryo UI", 9F);
            //this.Icon = Properties.Resources.AppIcon; // アイコン設定
            this.mainMenuStrip = new MenuStrip
            {
                Name = "mainMenuStrip",
                Dock = DockStyle.Top // フォームの最上部に固定するわ
            };
            this.Controls.Add(this.mainMenuStrip);

            // ----------------------------------------------------
            // 0-a. **ファイル メニュー (File ToolStripMenuItem) の作成**
            // ----------------------------------------------------
            ToolStripMenuItem fileMenu = new ToolStripMenuItem
            {
                Text = "ファイル(&F)" // Alt+F でアクセスできるようにするわ
            };
            this.mainMenuStrip.Items.Add(fileMenu);

            // ----------------------------------------------------
            // 0-b. **新規作成メニュー項目 (New ToolStripMenuItem) の作成**
            // ----------------------------------------------------
            ToolStripMenuItem newMenuItem = new ToolStripMenuItem
            {
                Text = "新規作成(&N)",
                ShortcutKeys = Keys.Control | Keys.N,
                ShowShortcutKeys = true
            };
            newMenuItem.Click += NewMenuItem_Click; // イベントハンドラ登録
            fileMenu.DropDownItems.Add(newMenuItem);

            // ----------------------------------------------------
            // 0-c. **上書き保存メニュー項目 (Save ToolStripMenuItem) の作成**
            // ----------------------------------------------------
            ToolStripMenuItem saveMenuItem = new ToolStripMenuItem
            {
                Text = "上書き保存(&S)",
                ShortcutKeys = Keys.Control | Keys.S,
                ShowShortcutKeys = true // メニューに "Ctrl+S" と表示させる
            };
            saveMenuItem.Click += SaveMenuItem_Click; // クリックイベントハンドラを設定
            fileMenu.DropDownItems.Add(saveMenuItem);

            // ----------------------------------------------------
            // 0-d. **名前を付けて保存メニュー項目 (Save As ToolStripMenuItem) の作成**
            // ----------------------------------------------------
            ToolStripMenuItem saveAsMenuItem = new ToolStripMenuItem
            {
                Text = "名前を付けて保存(&A)",
            };
            saveAsMenuItem.Click += SaveAsMenuItem_Click; // イベントハンドラ登録
            fileMenu.DropDownItems.Add(saveAsMenuItem);

            // ----------------------------------------------------
            // 0-e. 【おまけ】終了メニュー項目
            // ----------------------------------------------------
            fileMenu.DropDownItems.Add(new ToolStripSeparator()); // セパレーター（区切り線）

            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("終了(&X)");
            exitMenuItem.Click += (sender, e) => this.Close(); // フォームを閉じる
            fileMenu.DropDownItems.Add(exitMenuItem);

            // ----------------------------------------------------
            // 1. **外側の SplitContainer の設定 (全体を 左 vs 右側全体 に分割)**
            // ----------------------------------------------------
            this.outerSplitter = new SplitContainer
            {
                Name = "outerSplitter",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,      // 左側パネルの初期幅を調整
                FixedPanel = FixedPanel.Panel1      // 左側パネルの幅を優先して保持
            };
            // メニューの下に配置するために、Controls.Add(this.outerSplitter); の前にBringToFrontは不要
            this.Controls.Add(this.outerSplitter);
            this.outerSplitter.BringToFront(); // メニューの下に配置したため、最前面に持ってくる

            // ----------------------------------------------------
            // 2. **左側の SplitContainer (左エリアを 上 vs 下 に分割)**
            // ----------------------------------------------------
            this.leftSplitter = new SplitContainer
            {
                Name = "leftSplitter",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal, // 横方向に分割 (上下)
                SplitterDistance = 200,             // 左上パネルの初期高 (カレンダー+α)
            };
            this.outerSplitter.Panel1.Controls.Add(this.leftSplitter);

            // 2-a. 左上エリア: カレンダー (MonthCalendar) とタイトル
            Label calendarTitleLabel = new Label
            {
                Text = "📅 カレンダー内容",
                Dock = DockStyle.Top,
                Font = new System.Drawing.Font("Meiryo UI", 11F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(5),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 30,
                BackColor = System.Drawing.Color.LightCyan
            };
            this.leftSplitter.Panel1.Controls.Add(calendarTitleLabel);

            MonthCalendar calendarControl = new MonthCalendar
            {
                Name = "calendarControl",
                Dock = DockStyle.Fill, // DockStyle.Fillで残りの領域を埋める
                MaxSelectionCount = 1,
                Font = new System.Drawing.Font("Meiryo UI", 10F)
            };
            calendarControl.DateSelected += CalendarControl_DateSelected;
            this.leftSplitter.Panel1.Controls.Add(calendarControl);
            calendarControl.BringToFront(); // カレンダーをタイトルの下に配置

            // 2-b. 左下エリア: ディレクトリツリー (TreeView) とタイトル
            Label dirTitleLabel = new Label
            {
                Text = "📂 ノートディレクトリ",
                Dock = DockStyle.Top,
                Font = new System.Drawing.Font("Meiryo UI", 11F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(5),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 30,
                BackColor = System.Drawing.Color.LightCyan
            };
            this.leftSplitter.Panel2.Controls.Add(dirTitleLabel);

            this.directoryTreeView = new TreeView
            {
                Name = "directoryTreeView",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
            };
            directoryTreeView.NodeMouseClick += DirectoryTreeView_NodeMouseClick;
            this.leftSplitter.Panel2.Controls.Add(this.directoryTreeView);
            this.directoryTreeView.BringToFront(); // TreeViewをタイトルの下に配置


            // ----------------------------------------------------
            // 3. **内側の SplitContainer の設定 (右側全体を 中央 vs 右 に分割)**
            // ----------------------------------------------------
            this.innerSplitter = new SplitContainer
            {
                Name = "innerSplitter",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700, // 中央のパネルの初期幅を調整 (右側の情報パネルを小さく)
                FixedPanel = FixedPanel.Panel2      // 右側パネルの幅を固定
            };
            this.outerSplitter.Panel2.Controls.Add(this.innerSplitter);

            // ----------------------------------------------------
            // 4. **中央エリア: タブコントロールとNoteEditorPanel**
            // ----------------------------------------------------
            this.noteTabControl = new ClosableTabControl
            {
                Name = "noteTabControl",
                Dock = DockStyle.Top, // TabControlをPanel1の最上部に配置
                Height = 25, // タブヘッダーの高さ
                Padding = new System.Drawing.Point(10, 3)
            };

            this.noteTabControl.SelectedIndexChanged += NoteTabControl_SelectedIndexChanged; // イベント登録
            this.noteTabControl.TabClosing += NoteTabControl_TabClosing; // タブ閉じるイベント登録
            this.innerSplitter.Panel1.Controls.Add(this.noteTabControl);
            //this.noteTabControl.DrawMode = TabDrawMode.OwnerDrawFixed; // カスタム描画モードに設定

            // NoteEditorPanelのインスタンス化 (UserControlなので直接追加)
            this.noteEditorPanel = new NoteEditorPanel(); // 引数なしコンストラクタに変更
            this.noteEditorPanel.Dock = DockStyle.Fill; // 残りの領域を埋める
            // NoteEditorPanelのPaddingは、Form1側で調整
            this.noteEditorPanel.Padding = new Padding(0, 30, 0, 0); // TabControlのヘッダー高さ分

            this.innerSplitter.Panel1.Controls.Add(this.noteEditorPanel);
            //this.noteEditorPanel.BringToFront(); // NoteEditorPanelをタブコントロールより手前に

            // ----------------------------------------------------
            // 5. **右側エリア: 本日の予定/情報 (Panel)**
            // ----------------------------------------------------
            this.rightInfoPanel = new Panel
            {
                Name = "rightInfoPanel",
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.MistyRose, // 可愛いピンク色
                Padding = new Padding(10)
            };

            // 右側の情報コンテンツ (画像に合わせて)
            Label todoTitle = new Label
            {
                Text = "本日の予定",
                Dock = DockStyle.Top,
                Font = new System.Drawing.Font("Meiryo UI", 12F, System.Drawing.FontStyle.Bold),
                Height = 30,
            };
            Label dateLabel = new Label
            {
                Text = "本日の日付 (年/月/日)",
                Dock = DockStyle.Top,
                Padding = new Padding(0, 10, 0, 5)
            };
            Label scheduleLabel = new Label
            {
                Text = "予定 (要資格登録)",
                Dock = DockStyle.Top,
                Padding = new Padding(0, 15, 0, 5)
            };

            // コントロールをパネルに追加
            this.rightInfoPanel.Controls.Add(scheduleLabel);
            this.rightInfoPanel.Controls.Add(dateLabel);
            this.rightInfoPanel.Controls.Add(todoTitle);

            // Zオーダーを調整
            scheduleLabel.BringToFront();
            dateLabel.BringToFront();
            todoTitle.BringToFront();

            this.innerSplitter.Panel2.Controls.Add(this.rightInfoPanel);

            this.ResumeLayout(false); // コンポーネント初期化終了
            this.PerformLayout();
       */}
        #endregion
        // Designer.csに移動したフィールド宣言
        //private ClosableTabControl noteTabControl; // ノートタブコントロール
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
            noteEditorPanel.DragEve += (s, e) =>
            {
                DragEve?.Invoke(s, e);
            };
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


        }
    }
}