using System.Text;
using System.Text.RegularExpressions; // 正規表現でハイライト置換するために必要
using System.Web; // System.Web.HttpUtility用 (なければ System.Net.WebUtility)

namespace WinFormsApp2.Services
{
    public class SearchReportService
    {
        /// <summary>
        /// 検索結果をモダンなHTMLレポートに変換する
        /// </summary>
        public string GenerateHtmlReport(string keyword, List<SearchResult> results, bool isDarkMode)
        {
            // 1. カラーパレットの定義 (ThemeServiceと合わせると統一感が出るわ)
            string bgColor = isDarkMode ? "#1e1e1e" : "#ffffff";
            string textColor = isDarkMode ? "#d4d4d4" : "#333333";
            string cardBgColor = isDarkMode ? "#2d2d2d" : "#f9f9f9"; // カードの背景
            string cardBorder = isDarkMode ? "#3e3e3e" : "#e0e0e0";
            string linkColor = isDarkMode ? "#3794ff" : "#0078d7";
            string codeBgColor = isDarkMode ? "#1e1e1e" : "#ffffff"; // スニペット部分の背景
            string metaColor = isDarkMode ? "#858585" : "#666666";
            string highlightBg = "#f1c40f"; // ハイライトは黄色（黒文字）で目立たせる
            string highlightText = "#000000";

            var sb = new StringBuilder();
            /*
             * と思ったんだけど、よく考えたらCSS書いてくれてるから、それに合わせればいいだけなのでは？
            // 2. CSS定義
            sb.AppendLine($@"
            <style>
                body {{
                    font-family: 'Segoe UI', 'Meiryo UI', sans-serif;
                    background-color: {bgColor};
                    color: {textColor};
                    line-height: 1.6;
                    padding: 20px;
                    margin: 0;
                }}
                h2 {{
                    border-bottom: 2px solid {linkColor};
                    padding-bottom: 10px;
                    margin-top: 0;
                    font-size: 1.4em;
                    color: {linkColor};
                }}
                .meta {{ color: {metaColor}; font-size: 0.9em; margin-bottom: 20px; }}
                ul {{ list-style-type: none; padding: 0; }}
                li {{
                    background-color: {cardBgColor};
                    border: 1px solid {cardBorder};
                    border-radius: 6px;
                    padding: 12px;
                    margin-bottom: 12px;
                    transition: transform 0.1s;
                }}
                li:hover {{
                    border-color: {linkColor}; /* ホバー時に枠線を強調 
                }}
                a {{
                    text-decoration: none;
                    color: {linkColor};
                    font-weight: bold;
                    display: block;
                    margin-bottom: 6px;
                    font-size: 1.1em;
                }}
                a:hover {{ text-decoration: underline; }}
                .line-number {{
                    font-size: 0.8em;
                    color: {metaColor};
                    font-weight: normal;
                    margin-left: 8px;
                }}
                .snippet {{
                    font-family: Consolas, 'Courier New', monospace;
                    background-color: {codeBgColor};
                    padding: 8px;
                    border-radius: 4px;
                    font-size: 0.95em;
                    color: {textColor};
                    border: 1px solid {cardBorder};
                    white-space: pre-wrap; /* 折り返しあり 
                    word-break: break-all;
                }}
                mark {{
                    background-color: {highlightBg};
                    color: {highlightText};
                    border-radius: 2px;
                    padding: 0 2px;
                    font-weight: bold;
                }}
            </style>");
            */
            // 3. HTMLボディ生成
            sb.Append($"<h2>🔍 '{HttpUtility.HtmlEncode(keyword)}' の検索結果</h2>");

            if (results.Count == 0)
            {
                sb.Append($"<p class='meta'>一致するノートは見つかりませんでした。</p>");
            }
            else
            {
                sb.Append($"<p class='meta'><b>{results.Count}</b> 件のヒット</p>");
                sb.Append("<ul>");

                foreach (var item in results)
                {
                    // リンク生成
                    string encodedPath = HttpUtility.UrlEncode(item.FilePath);
                    string encodedKeyword = HttpUtility.UrlEncode(keyword);
                    string link = $"app://open/?path={encodedPath}&keyword={encodedKeyword}&LineNumber={item.LineNumber}";

                    // キーワードハイライト処理
                    // 正規表現を使って、大文字小文字を無視して置換し、<mark>タグで囲む
                    // 元のテキストの文字種（大文字小文字）を維持するためにRegexを使うわ
                    string safeContent = HttpUtility.HtmlEncode(item.LineContent);
                    string safeKeyword = Regex.Escape(keyword); // 正規表現のエスケープ

                    string highlightedContent = Regex.Replace(
                        safeContent,
                        safeKeyword,
                        m => $"<mark>{m.Value}</mark>",
                        RegexOptions.IgnoreCase
                    );

                    sb.Append("<li>");
                    sb.Append($"<a href='{link}'>");
                    sb.Append($"📄 {HttpUtility.HtmlEncode(item.FileName)}");
                    sb.Append($"<span class='line-number'>Line {item.LineNumber}</span>");
                    sb.Append("</a>");

                    sb.Append($"<div class='snippet'>{highlightedContent}</div>");
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
            }

            return sb.ToString();
        }
    }
}