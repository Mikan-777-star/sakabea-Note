using System;

namespace WinFormsApp2.Services
{
    public class AppCommand
    {
        public string Id { get; }
        public string Description { get; } // ユーザーに見せる文字
        public Action Execute { get; }     // 実行する処理

        public AppCommand(string id, string description, Action execute)
        {
            Id = id;
            Description = description;
            Execute = execute;
        }

        public override string ToString() => Description; // ListBox表示用
    }
}