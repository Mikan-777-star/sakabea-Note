
using System.ComponentModel;
using System.IO;
using System.Threading;
using WinFormsApp2.Services;
using WinFormsApp2.Views;

namespace WinFormsApp2.Presenters
{
    public class MainPresenter
    {
        // 依存する相手は全てインターフェースかServiceクラス
        private readonly IMainView _view;
        private readonly FileManager _fileManager;
        private readonly BackupManager _backupManager;
        private System.Threading.Timer _backupTimer;
        private readonly SettingsService _settingsService;
        private bool _isSearchMode = false;
        private readonly SearchReportService _searchReportService;
        private  ThemeService _themeService;
        public MainPresenter(IMainView view, FileManager fileManager, BackupManager backupManager)
        {
            _view = view;
            _fileManager = fileManager;
            _backupManager = backupManager;
            _settingsService = new SettingsService();
            _searchReportService = new SearchReportService();
            _themeService = new ThemeService();
            // Viewのイベントを購読（紐づけ）
            _view.LoadRequested += OnViewLoad;
            _view.FileSelected += OnFileSelected;
            _view.SaveRequested += OnSaveRequested;
            _view.NewFileRequested += OnNewFileRequested;
            _view.DateSelected += OnDateSelected;
            _view.ActiveDocumentChanged += OnActiveDocumentChanged;
            _view.EditorContentChanged += OnEditorContentChanged;
            _view.CloseRequested += OnCloseRequested;
            _view.GlobalSearchRequested += OnGlobalSearchRequested;
            _view.DashboardLinkClicked += OnDashboardLinkClicked;
            _view.SearchClearRequested += OnSearchClearRequested;
            _view.ThemeChanged += ThemeChange;
            _view.ImagePasteRequested += OnImagePasteRequested;
            _view.ChangeFolderRequested += OnChangeFolderRequested;
            _view.FileTreeRefreshRequested += OnFileTreeRefreshRequested;

            _backupTimer = new System.Threading.Timer(OnBackupTick, null, 60000, 60000);
        }

        // --- イベントハンドラ ---

        private void OnViewLoad(object? sender, EventArgs e)
        {
            var settings = _settingsService.Load();
            _view.SetWindowSettings(settings);
            if(settings.LastThemeService != null)
            {
                _view.ApplyTheme(settings.LastThemeService);
                _themeService = settings.LastThemeService;
            }
            
            if (settings.LastWorkspacePath == null || !Directory.Exists(settings.LastWorkspacePath))
            {
                settings.LastWorkspacePath = Directory.GetCurrentDirectory();
            }
            _fileManager.ChangeDirectory(settings.LastWorkspacePath);
            _view.UpdateResourcePath(settings.LastWorkspacePath);
            // 5. ツリーとダッシュボードを更新
            LoadFileTree();
            RefreshDashboard();
            LoadFileTree();
            RefreshDashboard();
            CheckForBackups();
            _view.SetResourceBasePath(_fileManager.CurrentDirectory);
            _view.StartConsole(settings.LastWorkspacePath);
        }

        private void OnFileSelected(object? sender, string filePath)
        {
            try
            {
                // ここでファイルを開く処理)
                if (_view.TrySelectTab(filePath))
                {
                    return;
                }
                var fileInfo = new FileInfo(filePath);

                // FileManagerを使って読み込む
                // MarkdownDocumentを作成する
                var doc = new MarkdownDocument(filePath, _fileManager);

                // Viewに「タブを開け」と命令する
                _view.OpenDocumentTab(doc);
            }
            catch (Exception ex)
            {
                _view.ShowError($"ファイルを開けませんでした: {ex.Message}");
            }
        }

        private void OnSaveRequested(object? sender, EventArgs e)
        {
            try
            {
                // 1. Viewからアクティブなドキュメントをもらう
                var doc = _view.GetActiveDocument();
                if (doc == null) return;

                // Viewから最新のテキストを取得して反映
                doc.UpdateContent(_view.GetCurrentEditorContent());

                // 3. ドキュメントに変更がないなら何もしない（負荷軽減）
                if (!doc.IsModified) return;

                // 4. 保存実行 (MarkdownDocument.Save メソッドは FileManager を要求する仕様でしたね)
                //    ここで本来は「名前を付けて保存(Untitledの場合)」の分岐が必要だけど、まずは上書き保存を通しましょう。

                if (doc.IsUntitled)
                {// Viewから最新のテキストを取得して反映
                    SaveAs(doc);
                    return;
                }

                if (doc.Save(_fileManager))
                {
                    _view.SetStatusMessage($"保存しました: {doc.GetDisplayName()} ({DateTime.Now:HH:mm:ss})"); // ← こうする

                    _backupManager.DeleteBackup(doc);
                    _view.UpdateTabTitle(doc);
                }
                else
                {
                    _view.ShowError("保存に失敗しました。");
                }
            }
            catch (Exception ex)
            {
                _view.ShowError($"保存エラー: {ex.Message}");
            }
        }
        // ★追加: 保存ロジックの共通化（名前を付けて保存）
        private void SaveAs(MarkdownDocument doc)
        {
            // 1. Viewに「どこに保存する？」と聞かせる
            string defaultName = doc.IsUntitled ? "Untitled.md" : Path.GetFileName(doc.FilePath);
            string? newPath = _view.AskUserForSavePath(defaultName);

            // 2. キャンセルされたら終了
            if (string.IsNullOrEmpty(newPath)) return;

            // 3. ドキュメントに新しいパスを設定（これはMarkdownDocument側のメソッドが必要ね）
            //    MarkdownDocument.cs に SetFilePathAndSaved みたいなメソッドがあったはず
            doc.SetFilePathAndSaved(newPath);

            // 4. 保存実行
            //    SetFilePathAndSaved で IsModified=false になっちゃう実装なら、
            //    Save() を呼ぶ必要がないか、あるいは Save() の中で書き込むか。
            //    以前の MarkdownDocument.Save の実装を見ると、書き込みを行っているから、
            //    「パス設定」→「Save()呼び出し」の流れが確実ね。

            //    ※ ここで少し MarkdownDocument の実装を見直す必要があるかも。
            //       SetFilePathAndSaved は「保存済み状態にする」メソッドだったわね。
            //       だから、ここでファイル書き込みを行う必要があるわ。

            try
            {
                // FileManagerを使って保存
                _fileManager.SaveFileContent(doc.FilePath, doc.Content);

                _view.UpdateTabTitle(doc);

                _backupManager.DeleteBackup(doc);
                // 成功したらメッセージ
                _view.ShowMessage($"保存しました: {Path.GetFileName(newPath)}");


                // TODO: タブのタイトルを新しいファイル名に更新する
                // _view.UpdateTabTitle(doc); // みたいなのが必要になるわ
            }
            catch (Exception ex)
            {
                _view.ShowError($"保存できませんでした: {ex.Message}");
            }
        }
        // --- ロジック ---

        private void LoadFileTree()
        {
            try
            {
                string rootPath = _fileManager.CurrentDirectory;

                // 再帰的にディレクトリ構造を読み込んで、FileNodeModelのリストを作る
                // 以前 Form1 にあったロジックを、TreeNode ではなく FileNodeModel を使うように書き換えるの。
                var rootNodes = new List<FileNodeModel>();

                var rootDir = new DirectoryInfo(rootPath);
                if (rootDir.Exists)
                {
                    var rootNode = new FileNodeModel
                    {
                        Name = rootDir.Name,
                        FullPath = rootDir.FullName,
                        IsDirectory = true
                    };

                    // ★ここが宿題よ：再帰メソッドを呼び出して子要素を埋める
                    PopulateNode(rootDir, rootNode);

                    rootNodes.Add(rootNode);
                }

                // Viewにデータを渡して「表示しろ」と命令
                _view.UpdateFileTree(rootNodes);
            }
            catch (Exception ex)
            {
                _view.ShowError($"ツリー読み込みエラー: {ex.Message}");
            }
        }

        private void PopulateNode(DirectoryInfo dirInfo, FileNodeModel parentNode)
        {
            try
            {
                // 1. ディレクトリの処理
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    // 隠し属性チェック & assetsフォルダ除外
                    bool isHidden = (subDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                    if (isHidden) continue;

                    if (subDir.Name.Equals("assets", StringComparison.OrdinalIgnoreCase)) continue;

                    var dirNode = new FileNodeModel
                    {
                        Name = subDir.Name,
                        FullPath = subDir.FullName,
                        IsDirectory = true
                    };

                    // 再帰呼び出し（深さを掘る）
                    PopulateNode(subDir, dirNode);

                    // 空のフォルダを表示したくないならここで children.Count チェックを入れる手もあるけど、
                    // 今回はフォルダがあれば追加する方針でいくわ。
                    parentNode.Children.Add(dirNode);
                }

                // 2. ファイルの処理 (*.md 限定)
                foreach (var file in dirInfo.GetFiles("*.md"))
                {
                    bool isHidden = (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                    if (isHidden) continue;

                    var fileNode = new FileNodeModel
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false
                    };
                    parentNode.Children.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // アクセス権がないフォルダは無視（またはエラーノードを追加）
                // ここでは単にスキップするわ
            }
        }

        private void OnNewFileRequested(object? sender, EventArgs e)
        {
            try
            {
                // 1. 空のドキュメントを作成 (MarkdownDocumentの引数なしコンストラクタ)
                var newDoc = new MarkdownDocument();

                // 2. Viewに表示させる
                _view.OpenDocumentTab(newDoc);
            }
            catch (Exception ex)
            {
                _view.ShowError($"新規作成エラー: {ex.Message}");
            }
        }

        private void OnDateSelected(object? sender, DateTime date)
        {
            try
            {
                // 1. 日付からファイルパスを決定 (YYYY-MM-DD.md)
                string fileName = $"{date:yyyy-MM-dd}.md";
                string filePath = Path.Combine(_fileManager.CurrentDirectory, fileName);


                if (_view.TrySelectTab(filePath))
                {
                    return;
                }

                // 2. 既に開いているかチェック (タブに存在するか確認するロジックがあればここで使う)
                //    (簡易的に、ファイルの存在チェックに進むわ)

                if (File.Exists(filePath))
                {
                    // A. 存在するなら普通に開く
                    var doc = new MarkdownDocument(filePath, _fileManager);
                    _view.OpenDocumentTab(doc);
                }
                else
                {
                    // B. 存在しないならテンプレートから新規作成
                    string initialContent = GenerateTemplateContent(date);

                    // 新規ドキュメントとして作成（まだファイルは作らない、メモリ上だけ）
                    var newDoc = new MarkdownDocument(initialContent);

                    _fileManager.SaveFileContent(filePath, initialContent); // ファイル作成！
                    var doc = new MarkdownDocument(filePath, _fileManager); // それを開く

                    _view.OpenDocumentTab(doc);
                }
            }
            catch (Exception ex)
            {
                _view.ShowError($"カレンダーノートエラー: {ex.Message}");
            }
        }

        // 元々Form1にあったロジックをここに移植
        private string GenerateTemplateContent(DateTime date)
        {
            string templatePath = System.IO.Path.Combine(_fileManager.CurrentDirectory, "templates", "daily_template.md");
            string content = "";

            if (System.IO.File.Exists(templatePath))
            {
                content = System.IO.File.ReadAllText(templatePath);
            }
            else
            {
                content = "# 📅 日報: {date}\n\n- [ ] タスク1\n- [ ] タスク2";
            }

            content = content.Replace("{date}", date.ToString("yyyy/MM/dd"));
            content = content.Replace("{day_of_week}", date.ToString("ddd"));

            return content;
        }

        private void OnEditorContentChanged(object? sender, EventArgs e)
        {
            // 1. アクティブなドキュメントを取得
            var doc = _view.GetActiveDocument();
            if (doc == null) return;

            // 2. ★重要: Viewのエディタ内容をModelに反映させる
            //    これによって doc.IsModified が true になり、
            //    doc.GetDisplayName() が "*" 付きの文字列を返すようになるわ。
            string currentText = _view.GetCurrentEditorContent();
            doc.UpdateContent(currentText);

            // 3. Viewに「タイトルを更新して」と命令
            _view.UpdateTabTitle(doc);
            RefreshDashboard();
        }

        private void OnActiveDocumentChanged(object? sender, EventArgs e)
        {
            var doc = _view.GetActiveDocument();
            if (doc == null) return;

            // 例: ウィンドウタイトルを変える（Viewにメソッド追加が必要だけど、今はログ出しで確認）
            System.Diagnostics.Debug.WriteLine($"Active doc changed to: {doc.GetDisplayName()}");

            RefreshDashboard();
            // ★ここが将来の拡張ポイント
            // 「もし今日の日付のファイルなら、ダッシュボードを更新する」
            // 「変更フラグがあるなら、保存ボタンを有効化する」
            // といったビジネスロジックはここに書くの。
        }

        private void RefreshDashboard()
        {
            if (_isSearchMode) return;
            try
            {
                DateTime today = DateTime.Today;
                string todayFileName = $"{today:yyyy-MM-dd}.md";
                string todayFilePath = System.IO.Path.Combine(_fileManager.CurrentDirectory, todayFileName);

                string title = $"📅 本日の予定 ({today:MM/dd})";
                string content = "";

                // 1. 今アクティブなドキュメントを取得
                var activeDoc = _view.GetActiveDocument();

                // 2. 「今開いているファイル」が「今日のファイル」と一致するかチェック
                //    (ファイルパス、または保存前ならファイル名で判定したいけど、
                //     Untitledの場合はパスがないから、意図通り動かすには工夫がいるわね。
                //     ここでは「パスが一致する場合」のみリアルタイム反映とするわ)

                if (activeDoc != null &&
                    !activeDoc.IsUntitled &&
                    System.IO.Path.GetFileName(activeDoc.FilePath) == todayFileName)
                {
                    // A. 今日のファイルを編集中 -> エディタの最新内容を表示
                    content = _view.GetCurrentEditorContent();
                }
                else
                {
                    // B. 別のファイルを見ている、または今日のファイルが開かれていない
                    //    -> ディスクから読み込む (ファイルがなければテンプレート)

                    if (System.IO.File.Exists(todayFilePath))
                    {
                        // ここでFileManagerを使うのが筋だけど、例外処理入れたり面倒なら直接読んでもいいわ
                        // でも一貫性のため _fileManager を使うのがベスト
                        content = _fileManager.ReadFileContent(todayFilePath);
                    }
                    else
                    {
                        // ファイルもない -> テンプレート表示
                        content = GenerateTemplateContent(today);
                    }
                }

                // 3. Viewに更新命令
                _view.UpdateDashboard(title, content);
            }
            catch (Exception ex)
            {
                // ダッシュボード更新エラーでダイアログ出すとうるさいから、ログ出し程度に
                System.Diagnostics.Debug.WriteLine($"Dashboard error: {ex.Message}");
            }
        }

        private void OnBackupTick(object? state)
        {
            // ここはバックグラウンドスレッド。
            // View (TextBoxとか) を直接触ると「クロススレッド操作」で怒られる。
            // だから InvokeOnUI を使って、UIスレッドにお願いしに行くの。

            _view.InvokeOnUI(async () =>
            {
                await RunBackupProcess();
            });
        }

        // バックアップの実行ロジック
        private async Task RunBackupProcess()
        {
            try
            {
                // 1. まず、現在アクティブなタブの内容を最新化する
                //    (非アクティブなタブは Deselecting で同期済みだけど、アクティブなやつだけは未同期だから)
                var activeDoc = _view.GetActiveDocument();
                if (activeDoc != null)
                {
                    activeDoc.UpdateContent(_view.GetCurrentEditorContent());
                }

                // 2. 全ドキュメントを取得
                var allDocs = _view.GetAllDocuments();

                // 3. 変更があるものだけバックアップ
                foreach (var doc in allDocs)
                {
                    // IsModifiedがtrue（未保存）のものだけ対象
                    if (doc.IsModified)
                    {
                        // BackupManagerは非同期でファイル書き込みをするからUIをブロックしない
                        await _backupManager.SaveBackupAsync(doc);

                        // ログ出し (確認用)
                        System.Diagnostics.Debug.WriteLine($"Backed up: {doc.GetDisplayName()}");
                    }
                }
            }
            catch (Exception ex)
            {
                // バックアップ失敗でエラーダイアログを出すと鬱陶しいので、ログだけ
                System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
            }
        }

        private void CheckForBackups()
        {
            try
            {
                // 1. バックアップファイルを全取得
                var backupFiles = _backupManager.GetBackupFiles();
                if (backupFiles.Length == 0) return;

                // 2. ユーザーに聞く
                if (_view.ConfirmAction($"前回、正しく終了されなかったファイルが {backupFiles.Length} 件あります。\n復元しますか？"))
                {
                    foreach (var backupPath in backupFiles)
                    {
                        RestoreFromBackup(backupPath);
                    }
                }
                else
                {
                    // 「いいえ」ならゴミとして捨てる
                    foreach (var path in backupFiles) _backupManager.DeleteBackupFile(path);
                }
            }
            catch (Exception ex)
            {
                _view.ShowError($"復元チェック中にエラー: {ex.Message}");
            }
        }

        private void RestoreFromBackup(string backupPath)
        {
            // BackupManager.LoadBackup は (originalPath, content) を返す仕様だったわね
            var (originalPath, content) = _backupManager.LoadBackup(backupPath);
            if (content == null || originalPath == null) return;

            // ドキュメントとして復元
            var doc = new MarkdownDocument(content);

            if (originalPath != "UNTITLED")
            {
                // 元のパスがあった場合、それを設定してあげる
                // (ただし、まだ保存はされていない状態 = IsModified は true のままにしておくこと！)

                // MarkdownDocumentにパスだけセットするメソッドが必要ね。
                // もし SetFilePathAndSaved しかないなら、呼んだあとに MarkAsModified() すればいいわ。
                doc.SetFilePathAndSaved(originalPath);
                doc.MarkAsModified();
            }

            // タブを開く
            _view.OpenDocumentTab(doc);

            // 復元に使ったバックアップファイルは消す？
            // いや、ユーザーがこれを見て「やっぱり保存しない」と閉じるまでは、
            // 次回のタイマーで上書きされるまで残しておいてもいいけど、
            // ここでメモリ上に展開されたから、一旦ファイルは消して、
            // また1分後に新しいバックアップが作られるサイクルに乗せるのが綺麗ね。
            _backupManager.DeleteBackupFile(backupPath);
        }

        private void OnCloseRequested(object? sender, CancelEventArgs e)
        {
            // 1. 未保存ドキュメントがあるかチェック
            var allDocs = _view.GetAllDocuments();
            var dirtyDocs = allDocs.Where(d => d.IsModified).ToList();

            if (dirtyDocs.Count > 0)
            {
                // ユーザーに聞く
                string msg = $"{dirtyDocs.Count} 件の未保存ファイルがあります。\n保存して終了しますか？";
                // ConfirmActionはYes/Noしか返さないから、Cancelも含めた3択ダイアログをViewに出させるメソッドが欲しいけど
                // ここでは簡易的に ConfirmAction (Yes/No) で実装するわ。
                // 本来は `ConfirmSaveOnExit` みたいな専用メソッドをViewに作るべきよ。

                // 簡易実装: 
                // Yes -> 全保存試行 -> 失敗したら終了キャンセル
                // No  -> 保存せず終了 (バックアップも消すべき？まぁ残ってても次回復元で聞かれるだけだからOK)
                // Cancel (×ボタンとか) -> 終了キャンセル

                // ※Viewに `DialogResult ShowSaveConfirm(...)` を追加するのが一番だけど、
                // 今ある `ConfirmAction` を使うなら「保存しますか？ (Yes=保存, No=破棄)」とするわ。

                if (_view.ConfirmAction(msg))
                {
                    // 「はい」を選んだ -> 全て保存
                    foreach (var doc in dirtyDocs)
                    {
                        // Untitledの場合はここで保存ダイアログが出る（SaveAsロジック再利用）
                        // 本当は SaveAs を public にするか、共通ロジックを切り出すべき。
                        // ここでは SaveAs を呼び出す形にするわ。

                        if (doc.IsUntitled)
                        {
                            SaveAs(doc);
                        }
                        else
                        {
                            if (doc.Save(_fileManager))
                            {
                                _backupManager.DeleteBackup(doc);
                            }
                        }

                        // 保存キャンセルや失敗で、まだIsModifiedなら...
                        if (doc.IsModified)
                        {
                            // ユーザーがキャンセルしたとみなして、アプリ終了も中断
                            e.Cancel = true;
                            return;
                        }
                    }
                }
                // 「いいえ」を選んだ場合は、何もしない（e.Cancel = false のまま）＝ 保存せずに閉じる
            }

            // 2. 無事に終了できるなら、設定を保存する
            if (!e.Cancel)
            {
                var settings = _view.GetWindowSettings();
                settings.LastWorkspacePath = _fileManager.CurrentDirectory; // パスも保存
                settings.LastThemeService = _themeService; // テーマ状態も保存
                _settingsService.Save(settings);
            }
        }
        private async void OnGlobalSearchRequested(object? sender, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keyword == "検索...") return;

            try
            {
                //_view.ShowMessage("検索中..."); // 本当はステータスバーが欲しいわね
                _isSearchMode = true; // ★検索モードON！
                _view.SetStatusMessage($"'{keyword}' を検索中...");

                // 1. Modelで検索実行 (非同期)
                var results = await _fileManager.SearchAllFilesAsync(keyword);

                _view.SetStatusMessage($"検索完了: {results.Count} 件見つかりました。");

                // 2. 結果をHTMLに変換
                string htmlreport = _searchReportService.GenerateHtmlReport(keyword, results,_themeService.IsDarkMode);

                // 3. ダッシュボードに表示
                _view.UpdateDashboard($"検索結果: {keyword}", htmlreport);
            }
            catch (Exception ex)
            {
                _view.ShowError($"検索エラー: {ex.Message}");
            }
        }

        // ダッシュボードのリンクが踏まれたら、そのファイルを開く
        private void OnDashboardLinkClicked(object? sender, LinkClickedEventArgs path)
        {
            // 既存のメソッドを再利用！
            // 既に開いていればタブ切り替え、なければ開くロジックがOnFileSelectedにあるわよね？
            // 同じロジックを呼べばいいの。
            // ※OnFileSelectedをpublicにするか、共通メソッド(OpenFile)を切るのが綺麗だけど、今は直接呼ぶわ。

            OnFileSelected(this, path.Path, path.Keyword, path.LineNumber);
        }
        private void OnFileSelected(object? sender, string filePath, string? keyword = null, int line = 0)
        {
            try
            {
                // 1. 既に開いているタブを選択
                if (_view.TrySelectTab(filePath))
                {
                    // ★追加: タブ切り替え後にハイライト実行
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        // View経由でエディタを操作する必要があるわね。
                        // IMainViewにメソッドを追加するか、
                        // NoteEditorPanelを直接触れないから、Viewに命令する形にする。
                        _view.HighlightEditorText(keyword, line);
                    }
                    return;
                }

                // 2. 新規オープン
                var doc = new MarkdownDocument(filePath, _fileManager);
                _view.OpenDocumentTab(doc);

                // ★追加: 開いた直後にハイライト
                if (!string.IsNullOrEmpty(keyword))
                {
                    _view.HighlightEditorText(keyword, line);
                }
            }
            catch (Exception ex)
            {
                _view.ShowError($"ファイルを開けませんでした: {ex.Message}");
            }
        }
        private void OnSearchClearRequested(object? sender, EventArgs e)
        {
            _isSearchMode = false; // ★検索モード解除
            _view.ClearSearchBox(); // 箱も空にする

            _view.SetStatusMessage("検索を終了しました。");

            // 即座にいつもの画面に戻す
            RefreshDashboard();
        }
        public void ToggleTheme()
        {
            // 1. サービスの状態を切り替え
            _themeService.ToggleTheme();

            // 2. Viewの見た目を更新
            _view.ApplyTheme(_themeService);

            // 3. コンテンツ（HTML）の再生成
            //    今のMarkdownConverter.ToHtmlに isDarkMode を渡すように修正して、
            //    ダッシュボードとプレビューをリロードする

            RefreshDashboard(); // これでダッシュボードは現在のテーマで再描画される

            // エディタのプレビュー更新
            // NoteEditorPanel側で「強制プレビュー更新」するメソッドが必要ね。
            // _view.RefreshPreview(); みたいな。
            // あるいは、簡易的に OnEditorContentChanged(null, null); を呼んで
            // 擬似的に更新イベントを起こすのもアリ（ちょっと乱暴だけど）。
            OnEditorContentChanged(this, EventArgs.Empty);
        }
        
        private void ThemeChange(object? sender, EventArgs e)
        {
            ToggleTheme();
        }
        public void Dispose()
        {
            // タイマーを止めて破棄する
            if (_backupTimer != null)
            {
                _backupTimer.Change(Timeout.Infinite, Timeout.Infinite); // 停止
                _backupTimer.Dispose(); // 破棄
                _backupTimer = null;
            }

            // ※ Serviceクラスなどが IDisposable を持っているなら、ここですべて呼ぶ
            // _searchReportService がもし IDisposable なら呼ぶ (今は違うから不要)
        }
        private void OnImagePasteRequested(object? sender, System.Drawing.Image image)
        {
            try
            {
                // 1. 保存先フォルダ (assets) の準備
                string assetsDir = System.IO.Path.Combine(_fileManager.CurrentDirectory, "assets");
                if (!System.IO.Directory.Exists(assetsDir))
                {
                    System.IO.Directory.CreateDirectory(assetsDir);
                }

                // 2. ユニークなファイル名を生成 (image-20251125-123456.png)
                string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                string savePath = System.IO.Path.Combine(assetsDir, fileName);

                // 3. 画像を保存
                // ※Bitmap形式で保存すると容量がデカくなるからPNG推奨
                image.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);

                // 4. エディタにMarkdownリンクを挿入
                // 相対パスで記述するのがポイント
                string markdownLink = $"![{fileName}](assets/{fileName})";
                _view.InsertTextAtCursor(markdownLink);

                _view.SetStatusMessage($"画像を貼り付けました: {fileName}");
            }
            catch (Exception ex)
            {
                _view.ShowError($"画像の保存に失敗しました: {ex.Message}");
            }
        }
        private void OnChangeFolderRequested(object? sender, EventArgs e)
        {
            // 1. ユーザーに新しい場所を聞く
            string? newPath = _view.AskUserForFolder(_fileManager.CurrentDirectory);
            if (string.IsNullOrEmpty(newPath)) return; // キャンセル

            // 2. 全てのタブを閉じる（保存確認付き）
            //    終了時の OnCloseRequested と似たロジックが必要ね。
            //    共通化するのがベストだけど、ここでは簡易的に書くわ。

            var allDocs = _view.GetAllDocuments();
            var dirtyDocs = allDocs.Where(d => d.IsModified).ToList();

            if (dirtyDocs.Count > 0)
            {
                if (_view.ConfirmAction($"{dirtyDocs.Count} 件の未保存ファイルがあります。\n保存してフォルダを移動しますか？"))
                {
                    // 保存処理 (SaveAs呼び出しなど)
                    foreach (var doc in dirtyDocs)
                    {
                        if (doc.IsUntitled) SaveAs(doc);
                        else { doc.Save(_fileManager); _backupManager.DeleteBackup(doc); }
                    }
                }
                else
                {
                    // 「いいえ」= 保存せずに移動 = 何もしない
                }
            }

            // 3. タブを全削除（View側でクリア）
            //    IMainViewに CloseAllTabs() が必要ね。
            //    とりあえずループで消す命令を出すわ。
            foreach (var doc in allDocs) _view.CloseDocumentTab(doc);

            try
            {
                // 4. フォルダ変更
                _fileManager.ChangeDirectory(newPath);
                _view.UpdateResourcePath(newPath);
                // 5. ツリーとダッシュボードを更新
                LoadFileTree();
                RefreshDashboard();

                // 6. 設定保存（次回ここから開くため）
                //    終了時にも保存してるけど、念のためここでも。
                var settings = _view.GetWindowSettings();
                settings.LastWorkspacePath = newPath;
                _settingsService.Save(settings);
                _view.RestartConsole(newPath);
                _view.SetStatusMessage($"ワークスペースを移動しました: {newPath}");
            }
            catch (Exception ex)
            {
                _view.ShowError($"移動エラー: {ex.Message}");
            }
        }
        private void OnFileTreeRefreshRequested(object? sender, EventArgs e)
        {
            // ツリーを再読み込み
            LoadFileTree();

            // 完了感を出すためにステータスバー更新
            _view.SetStatusMessage("ファイルツリーを更新しました。");
        }
    }
}