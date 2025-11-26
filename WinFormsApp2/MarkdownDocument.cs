using System;
using System.IO;
// using System.Windows.Forms; // UI依存を完全に削除！

namespace WinFormsApp2
{
    /// <summary>
    /// Markdownファイルのデータとメタデータを管理する軽量なクラス。
    /// UI（TabPage）との連携は行わない。純粋なデータモデル。
    /// </summary>
    public class MarkdownDocument
    {// ★ 追加: バックアップ用のユニークID
        public Guid Id { get; } = Guid.NewGuid();
        private bool _untitled;
        private string _filePath;
        private string _content;
        private bool _isModified;

        // UI（TabPage）への参照は持たない！
        // public TabPage ParentTabPage { get; private set; } // ★削除★

        public string FilePath => _filePath;
        public bool IsUntitled => _untitled;
        public bool IsModified => _isModified;

        /// <summary>
        /// ノートの内容を取得するよ
        /// </summary>
        public string Content => _content;

        /// <summary>
        /// コンストラクタでの改行コード正規化ヘルパーメソッド
        /// </summary>
        private static string NormalizeNewlines(string text)
        {
            // Windows標準のCRLF (\r\n) に統一
            return text?.Replace("\r\n", "\n").Replace("\n", "\r\n") ?? string.Empty;
        }

        /// <summary>
        /// 1. 新規作成（Untitled）コンストラクタ
        /// </summary>
        public MarkdownDocument()
        {
            _untitled = true;
            _filePath = ""; // Untitledの場合はパスを持たない
            _content = "";
            _isModified = true; // 新規作成は常に「未保存」状態
            // ParentTabPageの生成はForm1の責務になる
        }

        /// <summary>
        /// 2. 初期コンテンツを指定して新規作成（Untitled）コンストラクタ
        /// </summary>
        public MarkdownDocument(string initialContent)
        {
            _untitled = true;
            _filePath = "";
            _content = NormalizeNewlines(initialContent);
            _isModified = true; // 内容があるので未保存
        }

        /// <summary>
        /// 3. 日付指定で新規作成または既存ファイルをロードするコンストラクタ (カレンダー用)
        /// </summary>
        public MarkdownDocument(DateTime dt, FileManager fm)
        {
            // パス連結はFileManagerではなく、Path.Combineを使う
            string fileName = $"{dt.Year:D4}-{dt.Month:D2}-{dt.Day:D2}.md";
            _filePath = Path.Combine(fm.CurrentDirectory, fileName);
            _untitled = false;

            if (File.Exists(_filePath))
            {
                _content = NormalizeNewlines(fm.ReadFileContent(_filePath));
                _isModified = false;
            }
            else
            {
                _content = "";
                _isModified = true; // 新規作成とみなし、保存が必要
            }
            // ParentTabPageの生成はForm1の責務になる
        }


        /// <summary>
        /// 4. 既存ファイルをロードするコンストラクタ (ファイルツリー用)
        /// </summary>
        public MarkdownDocument(string filePath, FileManager fm)
        {
            _filePath = filePath;
            _untitled = false;

            if (File.Exists(_filePath))
            {
                _content = NormalizeNewlines(fm.ReadFileContent(_filePath));
                _isModified = false;
            }
            else
            {
                // 既存ファイルとして指定されたパスが存在しない場合は、
                // 通常は例外を投げるべきだが、ここでは空コンテンツで未保存扱いにする。
                // ただし、これが意図した挙動か再検討が必要。
                _content = "";
                _isModified = true;
            }
            // ParentTabPageの生成はForm1の責務になる
        }

        /// <summary>
        /// ノートの内容を設定し、変更フラグを立てるよ！
        /// このメソッドはUI層（Form1）から呼ばれる。
        /// </summary>
        public void UpdateContent(string newContent)
        {
            string normalizedNewContent = NormalizeNewlines(newContent);
            if (_content != normalizedNewContent) // 実際に内容が変わったかチェック
            {
                _content = normalizedNewContent;
                _isModified = true;
                // タブタイトルの更新はForm1の責務
            }
        }

        /// <summary>
        /// ドキュメントが変更されたことをマークするだけのメソッド。
        /// UIからの入力時に頻繁に呼ばれる。
        /// </summary>
        public void MarkAsModified()
        {
            if (!_isModified)
            {
                _isModified = true;
                // タブタイトルの更新はForm1の責務
            }
        }

        /// <summary>
        /// ファイルを保存するよ！
        /// このメソッドはUI層（Form1）から呼ばれる。
        /// </summary>
        /// <returns>保存に成功したかどうか</returns>
        public bool Save(FileManager fm)
        {
            // UI（SaveFileDialog）を呼び出すのはForm1の責務。
            // ここではファイルパスが確定している前提で処理する。
            if (_untitled || string.IsNullOrEmpty(_filePath))
            {
                // 本来、このメソッドが呼ばれる前にForm1がSave Asダイアログでパスを確定させるべき。
                // ここでは便宜的に例外を投げるが、Form1で適切に処理すること。
                throw new InvalidOperationException("Cannot save an untitled document without a file path. Use 'Save As' from UI.");
            }

            try
            {
                fm.SaveFileContent(_filePath, _content);
                _isModified = false;
                _untitled = false; // 保存されたのでUntitledではなくなる
                return true;
            }
            catch (Exception ex)
            {
                // エラーハンドリング (例: ログ出力、ユーザーへのメッセージ表示)
                Console.WriteLine($"Error saving file {_filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ドキュメントの表示名（タブタイトルなどに使用）を取得する。
        /// UIに依存しない形で生成。
        /// </summary>
        public string GetDisplayName()
        {
            string title = _untitled ? "Untitled" : Path.GetFileName(_filePath);
            return _isModified ? title + " *" : title;
        }

        /// <summary>
        /// 新しいファイルパスを設定し、_untitledフラグを更新する（"Save As"後の処理）。
        /// </summary>
        public void SetFilePathAndSaved(string newFilePath)
        {
            _filePath = newFilePath;
            _untitled = false;
            _isModified = false; // 新しいパスで保存されたので未変更
        }
    }
}