using System;
using System.IO;
using System.Text;
using System.Linq; // 必要に応じて追加

namespace WinFormsApp2
{
    public class SearchResult
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public int LineNumber { get; set; }
        public string LineContent { get; set; } = "";
    }
    public class FileManager
    {
        // 現在のディレクトリを保持するフィールド
        public string CurrentDirectory { get; private set; }

        // コンストラクタ (引数なし)
        

        // コンストラクタ (引数あり)
        public FileManager(string path)
        {
            ChangeDirectory(path);
        }

        // ★追加: ディレクトリ変更メソッド
        public void ChangeDirectory(string newPath)
        {
            if (!Directory.Exists(newPath))
            {
                throw new DirectoryNotFoundException($"Path not found: {newPath}");
            }
            CurrentDirectory = newPath;
        }

        // 指定された拡張子のファイルをすべて取得する汎用メソッド
        // Markdownファイルだけでなく、将来的に他の種類のファイルも扱えるように汎用化
        public string[] GetFiles(string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            // try-catchを削除し、そのまま投げる。
            // 呼び出し元（Form1など）で権限エラーなどをハンドリングして、
            // 「フォルダにアクセスできません」アイコンを表示するのが正しいUIよ。
            return Directory.GetFiles(CurrentDirectory, searchPattern, searchOption);
        }

        // Markdownファイルをすべて取得するメソッド (GetFilesのラッパー)
        public string[] GetMarkdownFiles()
        {
            return GetFiles("*.md", SearchOption.AllDirectories); // 全サブディレクトリを検索
        }

        // ファイルの内容を読み込むメソッド
        public string ReadFileContent(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            // usingを使うことで、確実にストリームを閉じる
            using (var sr = new StreamReader(filePath, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // ファイルの内容を保存するメソッド
        public void SaveFileContent(string filePath, string content)
        {
            // File.WriteAllTextは、ファイルが存在しない場合は自動的に作成する
            // 既存ファイルが存在する場合は上書きする
            // ★以前の File.Create(filePath); は不要で、ファイルロックの原因だった
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        // 新しいファイルを空の内容で作成するメソッド (オプション)
        // 基本的にはSaveFileContentで新規作成も兼ねるため、独立したCreateNewFileは不要なケースが多い
        public void CreateNewFile(string fileName)
        {
            string fullPath = Path.Combine(CurrentDirectory, fileName);
            if (File.Exists(fullPath))
            {
                throw new IOException($"File already exists: {fullPath}");
            }
            // 空のファイルを作成し、即座に閉じる
            File.WriteAllText(fullPath, string.Empty, Encoding.UTF8);
        }

        // ファイルパスがカレントディレクトリ内にあるかをチェックするユーティリティ
        public bool IsPathInCurrentDirectory(string fullPath)
        {
            // GetFullPathで正規化してから比較する
            string normalizedCurrentDir = Path.GetFullPath(CurrentDirectory);
            string normalizedFullPath = Path.GetFullPath(fullPath);
            return normalizedFullPath.StartsWith(normalizedCurrentDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 全てのMarkdownファイルを検索し、ヒットした行を返す
        /// </summary>
        public async Task<List<SearchResult>> SearchAllFilesAsync(string keyword)
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            // 全.mdファイルを取得
            var files = GetMarkdownFiles(); // 既存メソッド

            // 重い処理になる可能性があるからTaskで包む
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        // 行ごとに読み込んでチェック
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(new SearchResult
                                {
                                    FilePath = file,
                                    FileName = Path.GetFileName(file),
                                    LineNumber = i + 1,
                                    LineContent = lines[i].Trim()
                                });

                                // 1ファイルにつき数件で止める？いや、全部出すわよ。
                            }
                        }
                    }
                    catch { /* 読み込めないファイルは無視 */ }
                }
            });

            return results;
        }
    }
}