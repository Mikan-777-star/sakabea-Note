using WinFormsApp2.NoteApp.UI; // NoteEditorPanelがUserControlになったため、usingが必要
using Microsoft.Web.WebView2.Core;
namespace WinFormsApp2
{
    partial class Form1: Form
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
    }
}