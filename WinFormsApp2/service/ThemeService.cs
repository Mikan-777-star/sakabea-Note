using System.Drawing;
using Microsoft.Web.WebView2.Core;

namespace WinFormsApp2.Services
{
    public class ThemeService
    {
        public bool IsDarkMode { get; private set; } = false;

        public void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
        }

        // --- パレット定義 ---

        // 背景色
        public Color BackColor => IsDarkMode ? Color.FromArgb(30, 30, 30) : Color.White;
        public Color ControlBackColor => IsDarkMode ? Color.FromArgb(45, 45, 48) : Color.WhiteSmoke;

        // 文字色
        public Color ForeColor => IsDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black;
        public Color MutedForeColor => IsDarkMode ? Color.Gray : Color.DimGray;

        // アクセント（選択色など）
        public Color AccentColor => IsDarkMode ? Color.FromArgb(0, 122, 204) : Color.LightSkyBlue;

        // ボーダー色
        public Color BorderColor => IsDarkMode ? Color.FromArgb(60, 60, 60) : Color.LightGray;

        //タイトルラベルの背景色
        public Color TitleLabelColor => IsDarkMode ? Color.FromArgb(30, 30, 30) : Color.LightCyan;

        // WebView2用のテーマ設定
        public CoreWebView2PreferredColorScheme WebColor => IsDarkMode ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
    }
}