using System;
using System.Runtime.InteropServices;

namespace WinFormsApp2.NoteApp.UI
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        // EnumWindows関連
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // --- スタイル変更用 (32bit/64bit両対応の黒魔術) ---

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
            return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        // ★追加: スレッド接続用API
        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        // フォーカス設定用 (SetForegroundWindowより強力)
        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        public const int SW_SHOW = 5;
        public const int SW_MAXIMIZE = 3;
        public const int SW_NORMAL = 1;
        // 定数
        public const int GWL_STYLE = -16;
        public const long WS_CAPTION = 0x00C00000L;
        public const long WS_THICKFRAME = 0x00040000L;
        public const long WS_POPUP = 0x80000000L; // トップレベルであることを示す
        public const long WS_CHILD = 0x40000000L; // これがないと埋め込めない！
        public const long WS_VISIBLE = 0x10000000L;
        public const int WS_CLIPCHILDREN = 0x02000000; // 子供がいる領域は描画しない

        // フォーカス奪取用
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public const int VK_LBUTTON = 0x01; // 左クリック

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        public static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        [DllImport("user32.dll")]
        public static extern int GetScrollPos(IntPtr hWnd, int nBar);

        // public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public const int SB_THUMBPOSITION = 4;
        public const int SB_VERT = 1;
        public const int WM_VSCROLL = 0x0115;

        [DllImport("user32.dll")]
        public static extern int GetScrollRange(IntPtr hWnd, int nBar,out int lpMinPos, out int lpMaxPos);
        public const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    }
}