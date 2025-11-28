using System;
using System.Threading.Tasks;
using WinFormApp2.PluginBase;
using LocalAiPlugin.Services; // OllamaManagerの名前空間
using System.Text.Json;
using System.Text;

namespace LocalAiPlugin
{
    public class LocalAiPlugin : IPlugin
    {
        public string Name => "Local AI Assistant";
        public string Version => "1.0";

        private readonly HttpClient _client = new HttpClient();
        private OllamaManager _ollamaManager = null!;
        private IAppController _app = null!;

        public void Initialize(IAppController app)
        {
            _app = app;
            _ollamaManager = new OllamaManager();

            // 1. メニュー追加
            app.AddMenuItem("Tools", "AI要約 (Local)", OnSummarizeClicked, "Ctrl+Alt+I");

            // 2. ★Ollama起動！ (Fire and Forget)
            // Initializeは同期メソッドだから、待たずに裏で走らせる
            _ = StartOllamaAsync();
        }

        private async Task StartOllamaAsync()
        {
            _app.ShowMessage("プラグイン: AIエンジンを起動中...");
            await _ollamaManager.StartAsync();

            if (await _ollamaManager.IsOllamaRunning())
            {
                _app.ShowMessage("プラグイン: AI準備完了 (Ollama)");
            }
            else
            {
                _app.ShowMessage("プラグイン: AI起動失敗");
            }
        }

        private async void OnSummarizeClicked(object sender, EventArgs e)
        {
            /// 1. 選択範囲を取得
            string originalText = _app.GetSelectedEditorText();
            if (string.IsNullOrWhiteSpace(originalText))
            {
                _app.ShowMessage("テキストを選択してください。");
                return;
            }

            _app.ShowMessage("ローカルAIが思考中...");

            try
            {
                // 2. Ollama (ローカル) に投げる
                string result = await QueryOllamaAsync(originalText);

                // 2. 出力テキストを作成
                // 選択範囲があった場合、「原文 + 改行 + 要約」という新しい文字列を作る
                string newText;
                if (!string.IsNullOrEmpty(originalText))
                {
                    // 原文を残しつつ、下に追記するスタイル
                    newText = $"{originalText}\n\n> **AI Summary:**\n{result}\n";
                }
                else
                {
                    // 選択なしなら要約だけ
                    newText = $"\n> **AI Summary:**\n{result}\n";
                }

                // 3. ★一撃で置換する！
                // 「原文」を「原文+要約」で上書きすることで、
                // Ctrl+Z を押した時に「原文+要約」が消えて「原文」に戻るという完璧な挙動になるわ。
                _app.InsertTextAtCursor(newText);

                _app.ShowMessage("要約が完了しました。");
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"AIエラー: {ex.Message}\nOllamaは起動していますか？");
                _app.ShowMessage("AI処理に失敗しました。");
            }
        }

        private async Task<string> QueryOllamaAsync(string input)
        {
            // プロンプト作成
            var requestData = new
            {
                model = "phi3", // 事前に ollama pull phi3 しておくこと
                prompt = $"summarize the following text in Japanese using bullet points:\n\n{input}",
                stream = false // ストリーミングなしで一括取得
            };

            string json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ローカルホストへの通信！これは外部APIではない！
            var response = await _client.PostAsync("http://localhost:11434/api/generate", content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();

            // レスポンスパース (Ollamaの形式: { "response": "...", ... })
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("response").GetString() ?? "";
        }

        // ★後始末: アプリ終了時に呼ばれる
        public void Dispose()
        {
            // Ollamaを道連れにする
            _ollamaManager?.Dispose();
        }
    }
}