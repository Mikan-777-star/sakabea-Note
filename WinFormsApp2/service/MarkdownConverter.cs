using Markdig;
using System;
using System.Text;

namespace WinFormsApp2.Services
{
    /// <summary>
    /// MarkdownテキストをHTMLに変換する責務を持つクラス。
    /// </summary>
    public class MarkdownConverter
    {
        private readonly MarkdownPipeline _pipeline;

        // CSSは将来的に外部ファイルやリソースから読み込むべきだけど、
        // 今はここに定数として定義しておくわ。
        private const string DefaultCss = @"
<style>
    body { 
        font-family: 'Segoe UI', 'Meiryo UI', sans-serif;
        font-size: 11pt;
        line-height: 1.6;
        padding: 20px;
        color: #333;
        background-color: #fff;
    }
    h1 { border-bottom: 2px solid #000; padding-bottom: 5px; margin-top: 20px; }
    h2 { border-bottom: 1px solid #ccc; padding-bottom: 3px; margin-top: 15px; }
    pre {
        background-color: #f4f4f4;
        border: 1px solid #ddd;
        border-left: 3px solid #f36d33;
        font-family: Consolas, monospace;
        padding: 1em;
        overflow: auto;
    }
    code {
        font-family: Consolas, monospace;
        background-color: #eee;
        padding: 2px 4px;
        border-radius: 3px;
    }
    blockquote {
        margin: 0;
        padding: 0 1em;
        color: #777;
        border-left: 0.25em solid #dfe2e5;
    }
    table {
        width: 100%;
        border-collapse: collapse;
        margin: 1em 0;
    }
    th, td {
        border: 1px solid #ddd;
        padding: 8px;
    }
    th { background-color: #f2f2f2; }
    img { max-width: 100%; height: auto; }
</style>";

        // ★ 追加: ダークモード用CSS
        private const string DarkCss = @"
<style>
    body { 
        font-family: 'Segoe UI', 'Meiryo UI', sans-serif;
        font-size: 11pt;
        line-height: 1.6;
        padding: 20px;
        color: #d4d4d4; /* 文字色 */
        background-color: #1e1e1e; /* 背景色 */
    }
    h1 { border-bottom: 2px solid #555; padding-bottom: 5px; margin-top: 20px; color: #569cd6; }
    h2 { border-bottom: 1px solid #444; padding-bottom: 3px; margin-top: 15px; color: #4ec9b0; }
    pre {
        background-color: #2d2d2d;
        border: 1px solid #444;
        border-left: 3px solid #f36d33;
        color: #d4d4d4;
        padding: 1em;
        overflow: auto;
    }
    code {
        font-family: Consolas, monospace;
        background-color: #3c3c3c;
        color: #ce9178;
        padding: 2px 4px;
        border-radius: 3px;
    }
    blockquote {
        margin: 0;
        padding: 0 1em;
        color: #808080;
        border-left: 0.25em solid #606060;
    }
    table { width: 100%; border-collapse: collapse; margin: 1em 0; }
    th, td { border: 1px solid #444; padding: 8px; }
    th { background-color: #252526; }
    a { color: #3794ff; }
    img { max-width: 100%; height: auto; }
</style>";
        public MarkdownConverter()
        {
            // パイプラインの構築はコストがかかるから、コンストラクタで一度だけ行うのが定石よ。
            // UseAdvancedExtensions() を使うと、表(Table)や取り消し線などが有効になるわ。
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        /// <summary>
        /// Markdownテキストを受け取り、CSS付きの完全なHTML文字列を返す
        /// </summary>
        public string ToHtml(string markdownContent, string virtualHostUrl, bool isDarkMode)
        {
            if (string.IsNullOrEmpty(markdownContent)) return "";

            // 1. MarkdownをHTMLフラグメントに変換
            string htmlBody = Markdown.ToHtml(markdownContent, _pipeline);
            // CSSを切り替える
            string cssToUse = isDarkMode ? DarkCss : DefaultCss;
            // 2. 完全なHTMLドキュメントとして組み立てる
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine($"  <base href=\"{virtualHostUrl}\">");
            sb.AppendLine(cssToUse);
            sb.AppendLine("</head>");
            sb.AppendLine($"<body class=\"{(isDarkMode ? "dark" : "light")}\">");
            sb.AppendLine(htmlBody);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}