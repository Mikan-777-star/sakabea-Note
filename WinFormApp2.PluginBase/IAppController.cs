using System;
namespace WinFormApp2.PluginBase
{
    // プラグインに対して「何ができるか」を公開するAPIリスト
    public interface IAppController
    {
        // UI操作
        void AddMenuItem(string path, string text, EventHandler action, string shortcut = ""); // メニュー追加
        void ShowMessage(string message); // ステータスバー通知

        // エディタ操作
        string GetCurrentEditorText();
        void InsertTextAtCursor(string text);

        string GetSelectedEditorText();

        // 今後ここに必要な機能をどんどん足していくのよ
    }
}