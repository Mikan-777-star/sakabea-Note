using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace WinFormsApp2.NoteApp.UI
{
    public class EmbeddedTerminalPanel : UserControl
    {
        private Process? _process;
        private IntPtr _terminalHandle = IntPtr.Zero;
        private System.Windows.Forms.Timer _terminalKeeper = null!;

        public EmbeddedTerminalPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(20, 20, 20);

            // パネル自体をフォーカス可能に
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;

            // 監視・修復用タイマー
            _terminalKeeper = new System.Windows.Forms.Timer();
            _terminalKeeper.Interval = 100; // 0.1秒間隔でパトロール
            _terminalKeeper.Tick += TerminalKeeper_Tick;

            this.Disposed += (s, e) => StopTerminal();
        }

        // 背景描画をスキップ（チラつきと隠れ防止）
        protected override void OnPaintBackground(PaintEventArgs e) { }

        public void StartTerminal(string? workingDirectory = null)
        {
            if (_process != null && !_process.HasExited) return;

            try
            {
                var psi = new ProcessStartInfo("conhost.exe")
                {
                    Arguments = "powershell.exe", // cmd経由じゃなくて直接conhostにpowershellを渡すのが一番安定
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = workingDirectory ?? string.Empty
                };

                _process = Process.Start(psi);

                if (_process != null)
                {
                    Task.Run(async () =>
                    {
                        int retries = 0;
                        while (retries < 50)
                        {
                            if (_process.HasExited) return;
                            _process.Refresh();

                            // 自前の検索メソッド（前回実装したもの）
                            _terminalHandle = FindConsoleWindow(_process);

                            if (_terminalHandle != IntPtr.Zero)
                            {
                                // 初回セットアップ
                                this.Invoke(new Action(InitialHijack));
                                return;
                            }
                            await Task.Delay(100);
                            retries++;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start Error: {ex.Message}");
            }
        }

        // 初回の乗っ取り処理（スタイル変更など重い処理はここだけ）
        private void InitialHijack()
        {
            if (_terminalHandle == IntPtr.Zero) return;

            try
            {
                // スタイル変更: 親分(POPUP)をやめて子供(CHILD)になる
                long style = NativeMethods.GetWindowLongPtr(_terminalHandle, NativeMethods.GWL_STYLE).ToInt64();
                style &= ~NativeMethods.WS_CAPTION;
                style &= ~NativeMethods.WS_THICKFRAME;
                style &= ~NativeMethods.WS_POPUP; // やっぱり消したほうが埋め込みは安定する
                style |= NativeMethods.WS_CHILD;
                style |= NativeMethods.WS_VISIBLE;
                NativeMethods.SetWindowLongPtr(_terminalHandle, NativeMethods.GWL_STYLE, new IntPtr(style));

                // 親セット
                var status = NativeMethods.SetParent(_terminalHandle, this.Handle);
                if(status == IntPtr.Zero)
                    Debug.WriteLine("SetParent failed.");
                else
                    Debug.WriteLine("Reparenting terminal window.");

                // 有効化
                // ---------------------------------------------------------
                // 4. リサイズ & 表示
                // ---------------------------------------------------------

                NativeMethods.MoveWindow(_terminalHandle, 0, 0, this.Width, this.Height, true);
                NativeMethods.ShowWindow(_terminalHandle, NativeMethods.SW_SHOW);

                // ---------------------------------------------------------
                // 5. フォーカスを強制的に叩き込む
                // ---------------------------------------------------------
                this.BeginInvoke(new Action(() =>
                {
                    NativeMethods.SetFocus(_terminalHandle);
                }));

                // 監視開始！あとはタイマーが面倒を見る
                _terminalKeeper.Start();
            }
            catch { }
        }

        // ★常時監視・修復メソッド
        private void TerminalKeeper_Tick(object? sender, EventArgs e)
        {
            // ターミナルがいなければ何もしない
            if (_terminalHandle == IntPtr.Zero) return;

            // プロセスが死んでたら停止
            if (_process == null || _process.HasExited)
            {
                StopTerminal();
                return;
            }

            // 1. 親子関係の修復 (Handleが変わった場合などに対応)
            IntPtr currentParent = NativeMethods.GetParent(_terminalHandle);
            if (currentParent != this.Handle)
            {
                Debug.WriteLine("Reparenting terminal window.");
                NativeMethods.SetParent(_terminalHandle, this.Handle);
            }
            else
            {
                //デバッグようだったもの
            }

                // 2. サイズと位置の強制同期 (MoveWindowは軽量なので毎回呼んでも大丈夫)
                //    これで「リサイズ時にズレる」「0x0になる」を防ぐ
                NativeMethods.MoveWindow(_terminalHandle, 0, 0, this.Width, this.Height, true);

            // 3. クリック監視 & フォーカス奪取
            //    アプリがアクティブで、かつマウスがパネルの上にあればチェック
            if (Form.ActiveForm != null)
            {
                Point cursor = Cursor.Position;
                Rectangle bounds = this.RectangleToScreen(this.ClientRectangle);

                if (bounds.Contains(cursor))
                {
                    // 左クリック(0x01)が押されているか？
                    short state = NativeMethods.GetAsyncKeyState(0x01);
                    if ((state & 0x8000) != 0)
                    {
                        // 強制的にフォーカスを当てる
                        NativeMethods.SetFocus(_terminalHandle);
                    }
                }
            }
        }

        // プロセスIDから探すやつ（前回と同じ）
        /*
        private IntPtr FindWindowByProcessId(int pid)
        {
            IntPtr foundHandle = IntPtr.Zero;

            NativeMethods.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);

                if (windowPid == pid)
                {
                    // 1. クラス名を取得して確認
                    // コンソールウィンドウのクラス名は伝統的に "ConsoleWindowClass" (Windows 11でもconhostならこれ)
                    // ※Windows Terminal (wt.exe) の場合は "CASCADIA_HOSTING_WINDOW_CLASS" だけど、今回はconhostを使ってるから前者よ。

                    var sb = new System.Text.StringBuilder(256);
                    NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                    string className = sb.ToString();

                    // デバッグ用ログ
                    Debug.WriteLine($"PID Match: hWnd={hWnd}, Class={className}");

                    if (className == "ConsoleWindowClass")
                    {
                        Debug.WriteLine($"Found terminal window: hWnd={hWnd}");
                        foundHandle = hWnd;
                        return false; // 確保！捜索終了
                    }
                }
                return true; // 次へ
            }, IntPtr.Zero);

            return foundHandle;
        }*/

        private IntPtr FindConsoleWindow(Process parentProcess)
        {
            IntPtr foundHandle = IntPtr.Zero;

            // 1. 捜索対象のPIDリストを作る（親 + 子供たち）
            var targetPids = new HashSet<int>();

            try
            {
                targetPids.Add(parentProcess.Id); // 親

                // WMIを使って子プロセスを探す
                // "Select * From Win32_Process Where ParentProcessId = {parentProcess.Id}"
                using (var searcher = new ManagementObjectSearcher(
                    $"Select ProcessId From Win32_Process Where ParentProcessId = {parentProcess.Id}"))
                using (var collection = searcher.Get())
                {
                    foreach (var item in collection)
                    {
                        int childId = Convert.ToInt32(item["ProcessId"]);
                        targetPids.Add(childId);
                        Debug.WriteLine($"Terminal: Found Child Process PID={childId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI Error: {ex.Message}");
                // WMIが失敗しても、とりあえず親PIDだけで続行する
            }

            // 2. 全ウィンドウを走査して、リスト内のPIDを持つ ConsoleWindowClass を探す
            NativeMethods.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);

                // PIDリストに含まれているか？
                if (targetPids.Contains((int)windowPid))
                {
                    // クラス名チェック
                    var sb = new System.Text.StringBuilder(256);
                    NativeMethods.GetClassName(hWnd, sb, sb.Capacity);

                    if (sb.ToString() == "ConsoleWindowClass")
                    {
                        foundHandle = hWnd;
                        Debug.WriteLine($"Terminal: HIT! Found Console Window. PID={(int)windowPid}, Handle={hWnd}");
                        return false; // 確保！終了
                    }
                }
                return true; // 次へ
            }, IntPtr.Zero);

            return foundHandle;
        }

        private void StopTerminal()
        {
            _terminalKeeper.Stop();
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
                _terminalHandle = IntPtr.Zero;
            }
        }

        public void ApplyTheme(bool isDark) { /* 背景色変更など */ }

        public void RestartTerminal(string workingDirectory)
        {
            // 1. 既存のターミナルを停止
            StopTerminal();

            // 2. 少し待つ（プロセスのクリーンアップ待ち）
            // UIスレッドを止めないようにTaskで遅延させるのがスマートよ
            Task.Delay(200).ContinueWith(t =>
            {
                this.Invoke(new Action(() =>
                {
                    // 3. 新しい場所で開始
                    StartTerminal(workingDirectory);
                }));
            });
        }
    }
}