using System;
using WinFormApp2.PluginBase;

namespace SamplePlugin
{
    public class DateInsertPlugin : IPlugin
    {
        public string Name => "日付挿入プラグイン";
        public string Version => "1.0";

        public void Initialize(IAppController app)
        {
            // メニューに機能を追加
            app.AddMenuItem("Edit", "現在に日付を挿入", (s, e) =>
            {
                string dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                app.InsertTextAtCursor($"**{dateStr}** ");
                app.ShowMessage("日付を入れました！");
            });
        }
    }
}