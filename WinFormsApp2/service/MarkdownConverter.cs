using Markdig;
using System;
using System.Text;
using Markdig.Syntax; // ★追加: Blockなどの操作用
using Markdig.Renderers; // ★追加: HtmlRenderer用
using Markdig.Renderers.Html; // ★追加: 属性操作用

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
        private const string UnifiedCss = @"
<style>
    /* 1. 色の定義 (CSS変数) */
    :root {
        /* ライトモード用 (デフォルト) */
        --bg-color: #ffffff;
        --text-color: #333333;
        --border-color: #dddddd;
        --pre-bg: #f4f4f4;
        --code-bg: #eeeeee;
        --code-text: #333333;
        --quote-color: #777777;
        --quote-border: #dfe2e5;
        --link-color: #0078d7;
        --header-border: #000000;
        --table-header-bg: #f2f2f2;
    }

    /* ダークモード用 (オーバーライド) */
    body.dark {
        --bg-color: #1e1e1e;
        --text-color: #d4d4d4;
        --border-color: #444444;
        --pre-bg: #2d2d2d;
        --code-bg: #3c3c3c;
        --code-text: #ce9178;
        --quote-color: #808080;
        --quote-border: #606060;
        --link-color: #3794ff;
        --header-border: #555555;
        --table-header-bg: #252526;
    }

    /* 2. スタイルの適用 (変数を使う) */
    body { 
        font-family: 'Segoe UI', 'Meiryo UI', sans-serif;
        font-size: 11pt;
        line-height: 1.6;
        padding: 20px;
        background-color: var(--bg-color); /* 変数使用 */
        color: var(--text-color);          /* 変数使用 */
        transition: background-color 0.2s, color 0.2s; /* じわっと変わる演出 */
    }
    h1 { border-bottom: 2px solid var(--header-border); padding-bottom: 5px; margin-top: 20px; }
    h2 { border-bottom: 1px solid var(--border-color); padding-bottom: 3px; margin-top: 15px; }
    pre {
        background-color: var(--pre-bg);
        border: 1px solid var(--border-color);
        border-left: 3px solid #f36d33;
        font-family: Consolas, monospace;
        padding: 1em;
        overflow: auto;
    }
    code {
        font-family: Consolas, monospace;
        background-color: var(--code-bg);
        color: var(--code-text);
        padding: 2px 4px;
        border-radius: 3px;
    }
    blockquote {
        margin: 0;
        padding: 0 1em;
        color: var(--quote-color);
        border-left: 0.25em solid var(--quote-border);
    }
    table { width: 100%; border-collapse: collapse; margin: 1em 0; }
    th, td { border: 1px solid var(--border-color); padding: 8px; }
    th { background-color: var(--table-header-bg); }
    a { color: var(--link-color); }
    img { max-width: 100%; height: auto; display: block; }
</style>";
        private const string script = @"
    <script>

        /**
 * DOM差分更新ロジック
 * @param {HTMLElement} currentNode - 現在表示されている実際のDOMノード
 * @param {HTMLElement} newNode - 新しいHTMLから生成した仮想DOMノード
 */
function updateElement(currentNode, newNode) {
    // 1. ノードの種類が違う、またはタグ名が違う場合 -> 丸ごと置換
    if (!currentNode || currentNode.nodeType !== newNode.nodeType || currentNode.tagName !== newNode.tagName) {
        if (currentNode) {
            currentNode.replaceWith(newNode.cloneNode(true));
        }
        return;
    }

    // 2. テキストノードの場合 -> 内容が違えば更新
    if (currentNode.nodeType === Node.TEXT_NODE) {
        if (currentNode.textContent !== newNode.textContent) {
            currentNode.textContent = newNode.textContent;
        }
        return;
    }

    // 3. 要素ノードの場合 -> 属性と子要素の同期

    // A. 属性の更新・追加
    // 新しいノードの属性を全てチェック
    for (const attr of newNode.attributes) {
        if (currentNode.getAttribute(attr.name) !== attr.value) {
            currentNode.setAttribute(attr.name, attr.value);
        }
    }
    // 古いノードにあって新しいノードにない属性を削除
    for (let i = currentNode.attributes.length - 1; i >= 0; i--) {
        const attrName = currentNode.attributes[i].name;
        if (!newNode.hasAttribute(attrName)) {
            currentNode.removeAttribute(attrName);
        }
    }

    // B. 子要素の再帰的更新
    const currentChildren = Array.from(currentNode.childNodes);
    const newChildren = Array.from(newNode.childNodes);
    
    const maxLen = Math.max(currentChildren.length, newChildren.length);

    for (let i = 0; i < maxLen; i++) {
        // 新しい子がない -> 古い子を削除
        if (!newChildren[i]) {
            if (currentChildren[i]) {
                currentChildren[i].remove();
            }
            continue;
        }

        // 古い子がない -> 新しい子を追加
        if (!currentChildren[i]) {
            currentNode.appendChild(newChildren[i].cloneNode(true));
            continue;
        }

        // 両方ある -> 再帰的に比較更新
        updateElement(currentChildren[i], newChildren[i]);
    }
}
        // HTML文字列からDOMを生成して、現在のbodyと差分更新する
        window.updateContent = function(newHtml) {
            var wasDark = document.body.classList.contains('dark');
            // 1. 新しいHTMLをパースして仮想DOM(のようなもの)を作る
            var scroollY = window.scrollY; // スクロール位置保存
            var scroollX = window.scrollX;
            console.log(newHtml);
            console.log('Updating content, preserving scroll position:', scroollX, scroollY);
            var parser = new DOMParser();
            var newDoc = parser.parseFromString(newHtml, 'text/html');
            
            // 2. パッチ適用実行
            // document.body そのものではなく、コンテンツのラッパーを更新対象にするのが安全
            var currentContent = document.body;
            var newContent = newDoc.body;

            updateElement(currentContent, newContent);

            if (wasDark) {
                document.body.classList.add('dark');
            } else {
             document.body.classList.remove('dark');
            }
            // スクロール位置復元    
            window.scrollTo(scroollX, scroollY);
        };
        window.setTheme = function(isDark) {
                if (isDark) {
                    document.body.classList.add('dark');
                } else {
                    document.body.classList.remove('dark');
                }
            };
window.syncToLine = function(targetLine) {
            // line属性を持つ全要素を取得
            var elements = document.querySelectorAll('[line]');
            
            if (elements.length === 0) return;

            // ターゲット行に一番近い（直後の）要素を探す
            // (空行などはタグにならないから、完全一致しないことがあるため)
            var bestElement = null;
            var minDiff = Infinity;

            for (var i = 0; i < elements.length; i++) {
                var line = parseInt(elements[i].getAttribute('line'));
                
                // ターゲット行以降で、一番近いものを選ぶ
                if (line >= targetLine) {
                    var diff = line - targetLine;
                    if (diff < minDiff) {
                        minDiff = diff;
                        bestElement = elements[i];
                    }
                }
            }

            // 見つかったらスクロール
            if (bestElement) {
                // スムーズだと遅れるから 'auto' で瞬時に飛ばす
                bestElement.scrollIntoView({ behavior: 'auto', block: 'start' });
            }
        };
    </script>";
        public MarkdownConverter()
        {
            // パイプラインの構築はコストがかかるから、コンストラクタで一度だけ行うのが定石よ。
            // UseAdvancedExtensions() を使うと、表(Table)や取り消し線などが有効になるわ。
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UsePreciseSourceLocation()
                .Build();
        }

        /// <summary>
        /// Markdownテキストを受け取り、CSS付きの完全なHTML文字列を返す
        /// </summary>
        public string ToHtml(string markdownContent, string virtualHostUrl, bool isDarkMode, bool isnewHTML = true)
        {
            // 1. ボディの中身（HTMLタグ群）を作る
            string htmlBody = ConvertToHtmlBody(markdownContent);

            // 2. 外枠（CSSとか）をつけて返す
            if (isnewHTML)
            {
                return WrapHtml(htmlBody, virtualHostUrl, isDarkMode);
            }
            else
            {
                return htmlBody;
            }
        }

        // ★追加: 中身だけを返すメソッド（JS更新用）
        public string ConvertToHtmlBody(string markdownContent)
        {
            if (string.IsNullOrEmpty(markdownContent)) return "";

            // 1. パースしてドキュメントツリーを作る
            var document = Markdown.Parse(markdownContent, _pipeline);

            // 2. 全てのブロック要素を巡回して、line属性を追加する
            // document.Descendants<Block>() で、段落や見出しなどのブロック要素を全部取れるわ
            foreach (var block in document.Descendants<Block>())
            {
                // HTML属性に line="行番号" を追加
                // (MarkdigのLineは0始まり。エディタ側も0始まりで統一すれば計算不要よ)
                block.GetAttributes().AddProperty("line", block.Line.ToString());
            }

            // 3. HTMLにレンダリング
            using (var writer = new StringWriter())
            {
                var renderer = new HtmlRenderer(writer);
                _pipeline.Setup(renderer);
                renderer.Render(document);

                return writer.ToString();
            }
        }

        // ヘルパー: ガワを作る
        private string WrapHtml(string htmlBody, string virtualHostUrl, bool isDarkMode)
        {
            string cssToUse = UnifiedCss;
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine($"  <base href=\"{virtualHostUrl}\">");
            sb.AppendLine(cssToUse);

            // ★ポイント: スクロール位置を保存・復元するJSを埋め込んでおく
            // これがないと、更新のたびに一番上に戻されちゃうわよ
            sb.AppendLine(script);

            sb.AppendLine("</head>");
            sb.AppendLine($"<body class=\"{(isDarkMode ? "dark" : "light")}\">");
            sb.AppendLine(htmlBody);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
        // MarkdownConverter.cs

        // ★追加: エクスポート用のクリーンなHTMLを生成
        public string ToExportHtml(string markdownContent, bool isDarkMode)
        {
            string htmlBody = ConvertToHtmlBody(markdownContent);

            // 編集用JSを含まない、シンプルなラッパーを使う
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            // CSSはそのまま使う（見た目は統一したいからね）
            sb.AppendLine(UnifiedCss);
            sb.AppendLine("  <style>");
            sb.AppendLine("    /* 印刷/PDF用の調整 */");
            sb.AppendLine("    @media print {");
            sb.AppendLine("      body { background-color: #fff; color: #000; }"); // インク節約のため白背景強制もアリだけど、今回は見た目重視でそのまま
            sb.AppendLine("      pre { page-break-inside: avoid; }"); // コードブロックの途中で改ページさせない
            sb.AppendLine("    }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            // クラスだけセットして、即座に反映させる
            sb.AppendLine($"<body class=\"{(isDarkMode ? "dark" : "light")}\">");
            sb.AppendLine(htmlBody);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}