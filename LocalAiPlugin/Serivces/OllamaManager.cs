using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace LocalAiPlugin.Services
{
    public class OllamaManager : IDisposable
    {
        private Process? _ollamaProcess;
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434"; // デフォルトポート

        // 自分が起動したかどうか（終了時に殺すかどうかのフラグ）
        private bool _isManagedByMe = false;

        public OllamaManager()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        }

        /// <summary>
        /// Ollamaサーバーを開始（または確認）する
        /// </summary>
        public async Task StartAsync()
        {
            // 1. 既に動いているかチェック
            if (await IsOllamaRunning())
            {
                System.Diagnostics.Debug.WriteLine("Ollama: Already running. I will use existing instance.");
                _isManagedByMe = false;
                return;
            }
            string command = "ollama";

            // ユーザーごとのデフォルトインストールパスをチェック
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Debug.WriteLine(localAppData);
            string fullPath = System.IO.Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe");

            if (System.IO.File.Exists(fullPath))
            {
                command = fullPath; // 絶対パスが見つかればそれを使う
            }
            // 2. 動いていなければ起動する
            try
            {
                System.Diagnostics.Debug.WriteLine("Ollama: Starting server...");

                var psi = new ProcessStartInfo(command)
                {
                    Arguments = "serve", // サーバーモードで起動
                    UseShellExecute = false,
                    CreateNoWindow = true, // 完全に裏方として動かす
                    // ログを見たいならRedirectStandardOutputなどを設定するけど、今回は静かにさせる
                };

                _ollamaProcess = Process.Start(psi);
                _isManagedByMe = true;

                // 3. 起動完了を少し待つ（ポーリング）
                // サーバーが応答するまで最大10秒くらい待ってみる
                int retries = 0;
                while (retries < 20) // 0.5s * 20 = 10s
                {
                    await Task.Delay(500);
                    if (await IsOllamaRunning())
                    {
                        System.Diagnostics.Debug.WriteLine("Ollama: Started successfully.");
                        return;
                    }
                    retries++;
                }

                System.Diagnostics.Debug.WriteLine("Ollama: Start timed out, but process is running.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ollama: Start failed. {ex.Message}");
                // PATHに通ってない可能性などが考えられるわね
            }
        }

        /// <summary>
        /// サーバーが生きてるかチェック
        /// </summary>
        public async Task<bool> IsOllamaRunning()
        {
            try
            {
                // ルートにGETして200 OKが返れば生きているとみなす
                // Ollamaはルートにアクセスすると "Ollama is running" と返す
                var response = await _httpClient.GetAsync(OllamaUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 終了処理（自分が起動したなら殺す）
        /// </summary>
        public void Dispose()
        {
            if (_isManagedByMe && _ollamaProcess != null && !_ollamaProcess.HasExited)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Ollama: Stopping server...");
                    _ollamaProcess.Kill(); // 強制終了
                    _ollamaProcess.WaitForExit(1000); // 少し待つ
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ollama: Stop failed. {ex.Message}");
                }
                finally
                {
                    _ollamaProcess.Dispose();
                    _ollamaProcess = null;
                }
            }
            _httpClient.Dispose();
        }
    }
}