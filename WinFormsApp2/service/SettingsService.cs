using System;
using System.IO;
using System.Text.Json;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsApp2.Services
{
    /// <summary>
    /// 保存したい設定データの構造
    /// </summary>
    public class AppSettings
    {
        // ウィンドウの位置とサイズ
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public FormWindowState WindowState { get; set; }

        // 3つのスプリッターの位置
        public int OuterSplitterDistance { get; set; } // 左パネル幅
        public int LeftSplitterDistance { get; set; }  // カレンダー/ツリー比率
        public int InnerSplitterDistance { get; set; } // エディタ/ダッシュボード比率

        public string? LastWorkspacePath { get; set; } // 追加

        public ThemeService? LastThemeService { get; set; }
        // デフォルト値を生成するメソッド
        public static AppSettings Default()
        {
            return new AppSettings
            {
                X = 100,
                Y = 100,
                Width = 1200,
                Height = 800,
                WindowState = FormWindowState.Normal,
                OuterSplitterDistance = 250,
                LeftSplitterDistance = 250,
                InnerSplitterDistance = 600,
                LastWorkspacePath = Directory.GetCurrentDirectory(),
                LastThemeService =  new ThemeService()
            };
        }
    }

    /// <summary>
    /// 設定の読み書きを担当するサービス
    /// </summary>
    public class SettingsService
    {
        private readonly string _filePath;

        public SettingsService()
        {
            // 実行ファイルと同じ場所に appsettings.json を作成
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings Save Error: {ex.Message}");
            }
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath)) return AppSettings.Default();

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default();
            }
            catch
            {
                return AppSettings.Default();
            }
        }
    }
}