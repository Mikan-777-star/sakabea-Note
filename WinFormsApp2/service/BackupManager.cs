using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsApp2.Services
{
    public class BackupManager
    {
        private readonly string _backupDir;

        public BackupManager()
        {
            // アプリの実行フォルダに "backups" フォルダを作る
            _backupDir = Path.Combine(Directory.GetCurrentDirectory(), "backups");
            if (!Directory.Exists(_backupDir))
            {
                Directory.CreateDirectory(_backupDir);
            }
        }

        /// <summary>
        /// ドキュメントのバックアップを非同期で保存する
        /// ファイル名: {GUID}.bak
        /// 中身: 1行目に元のファイルパス、2行目以降に本文
        /// </summary>
        public async Task SaveBackupAsync(MarkdownDocument doc)
        {
            if (!doc.IsModified) return; // 変更がなければバックアップ不要

            string backupPath = GetBackupPath(doc);
            string originalPath = doc.IsUntitled ? "UNTITLED" : doc.FilePath;

            // バックアップファイル形式:
            // Line 1: Original File Path (復元時に使う)
            // Line 2~: Content
            string contentToSave = $"{originalPath}\n{doc.Content}";

            try
            {
                // 非同期で書き込む（UIを止めない！）
                await File.WriteAllTextAsync(backupPath, contentToSave, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // バックアップ失敗でアプリを落とすべきではないので、ログ出し程度に留める
                System.Diagnostics.Debug.WriteLine($"Backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 正規保存されたら、バックアップは不要なので消す
        /// </summary>
        public void DeleteBackup(MarkdownDocument doc)
        {
            string backupPath = GetBackupPath(doc);
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); } catch { }
            }
        }

        /// <summary>
        /// 起動時に、残っているバックアップファイル（＝クラッシュして消せなかったファイル）を全て取得する
        /// </summary>
        public string[] GetBackupFiles()
        {
            return Directory.GetFiles(_backupDir, "*.bak");
        }

        /// <summary>
        /// バックアップファイルの中身を読み込む
        /// </summary>
        public (string? originalPath, string? content) LoadBackup(string backupFilePath)
        {
            // 1行目とそれ以降を分ける
            var lines = File.ReadAllLines(backupFilePath, Encoding.UTF8);
            if (lines.Length == 0) return (null, null);

            string originalPath = lines[0];
            // 2行目以降を結合して本文に戻す
            string content = string.Join("\n", lines, 1, lines.Length - 1);

            return (originalPath, content);
        }

        // 復元完了後などにファイルを消す用
        public void DeleteBackupFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private string GetBackupPath(MarkdownDocument doc)
        {
            return Path.Combine(_backupDir, $"{doc.Id}.bak");
        }
    }
}