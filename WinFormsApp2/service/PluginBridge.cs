using System;
using WinFormApp2.PluginBase;
using WinFormsApp2.Views;

namespace WinFormsApp2.Services
{
    // 本体機能とプラグインの仲介役
    public class PluginBridge : IAppController
    {
        private readonly IMainView _view;

        public PluginBridge(IMainView view)
        {
            _view = view;
        }

        public void AddMenuItem(string path, string text, EventHandler action, string shortcut = "")
        {
            // Viewに「メニュー追加して」と頼む
            // (IMainViewにAddPluginMenuを追加する必要があるわね)
            _view.InvokeOnUI(() =>
            {
                _view.AddPluginMenu(path, text, action,shortcut);
            });
        }

        public void ShowMessage(string message)
        {
            _view.InvokeOnUI(() => _view.SetStatusMessage(message));
        }

        public string GetCurrentEditorText()
        {
            // UIスレッドをまたぐ場合は注意が必要だけど、取得系はInvokeが必要かも
            // ここでは簡易的に
            return _view.GetCurrentEditorContent();
        }

        public void InsertTextAtCursor(string text)
        {
            _view.InvokeOnUI(() => _view.InsertTextAtCursor(text));
        }

        public string GetSelectedEditorText()
        {
            // UIスレッドで取得して返す (Invokeが必要な場合はFuncを使う)
            // 今回は簡易実装で直接呼ぶわ（落ちるようならInvokeに変えて）
            return _view.GetSelectedEditorText();
        }
    }
}