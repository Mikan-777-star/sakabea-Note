using System;
using WinFormApp2.PluginBase;

namespace TextStatsPlugin
{
    public class TextStatsPlugin : IPlugin
    {
        public string Name => "テキスト統計";
        public string Version => "1.0";

        public void Initialize(IAppController app)
        {
            app.AddMenuItem("Tools", "文字数カウント", (s, e) =>
            {
                // 1. 選択範囲を取得
                string targetText = app.GetSelectedEditorText();
                string targetName = "選択範囲";

                // 2. 選択されてなければ全体を取得
                if (string.IsNullOrEmpty(targetText))
                {
                    targetText = app.GetCurrentEditorText();
                    targetName = "全体";
                }

                // 3. 計算
                int chars = targetText.Length;
                int lines = targetText.Split('\n').Length;
                int words = targetText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

                // 4. 表示
                string msg = $"【{targetName}の統計】\n" +
                             $"文字数: {chars:#,0}\n" +
                             $"単語数: {words:#,0}\n" +
                             $"行数: {lines:#,0}";

                app.ShowMessage(msg.Replace("\n", "  ")); // ステータスバー用

                // 詳細をダイアログで出してもいいわね
                System.Windows.Forms.MessageBox.Show(msg, "テキスト統計");
            });

        }
        public void Dispose()
        {
            //なし
        }
    }
}