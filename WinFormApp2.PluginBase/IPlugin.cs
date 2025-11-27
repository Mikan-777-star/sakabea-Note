namespace WinFormApp2.PluginBase
{
    public interface IPlugin
    {
        // プラグインの名前
        string Name { get; }

        // バージョン
        string Version { get; }

        // 初期化処理（アプリ起動時に呼ばれる）
        // app: 本体を操作するためのコントローラー
        void Initialize(IAppController app);
    }
}