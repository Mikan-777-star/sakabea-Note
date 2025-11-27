using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using WinFormApp2.PluginBase;

namespace WinFormsApp2.Services
{
    public class PluginManager : IDisposable
    {
        private readonly List<IPlugin> _plugins = new List<IPlugin>();
        private readonly IAppController _api;

        public PluginManager(IAppController api)
        {
            _api = api; 
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        public void LoadPlugins()
        {
            // 実行ファイル直下の "plugins" フォルダを探す
            Debug.WriteLine("search plugins");
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

            foreach (var file in Directory.GetFiles(pluginsDir, "*.dll"))
            {
                try
                {
                    // DLLをロード
                    Debug.WriteLine($"{file}");
                    var assembly = Assembly.LoadFrom(file);

                    // IPluginを実装しているクラスを探す
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface)
                        {
                            // インスタンス化
                            var plugin = (IPlugin)Activator.CreateInstance(type)!;

                            // 初期化 (ここでAPIを渡す！)
                            plugin.Initialize(_api);

                            _plugins.Add(plugin);
                            System.Diagnostics.Debug.WriteLine($"Loaded Plugin: {plugin.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading plugin {file}: {ex.Message}");
                }
            }
        }
        private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            // 1. 探しているアセンブリの名前
            var requestedName = new AssemblyName(args.Name);

            // ★追加: 既にメモリに読み込まれているアセンブリの中に、同じ名前のやつがいないか探す
            // (本体が既に PluginBase を使っているから、メモリには絶対にあるはずなの！)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (AssemblyName.ReferenceMatchesDefinition(asm.GetName(), requestedName))
                {
                    return asm; // 「あ、それならここにあるわよ」と渡す
                }
            }

            // 2. なければファイルを探しに行く (既存のロジック)
            // リソースファイル(.resources)のリクエストは無視する（これ重要！）
            if (requestedName.Name.EndsWith(".resources")) return null;

            var assemblyName = requestedName.Name + ".dll";
            var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName);

            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return null;
        }

        // Disposeがあるなら、そこでイベント解除するのもマナーよ

        // ★追加: 終了処理
        public void Dispose()
        {

            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            foreach (var plugin in _plugins)
            {
                try
                {
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    // プラグインの不始末で本体を落とさない
                    System.Diagnostics.Debug.WriteLine($"Error disposing plugin {plugin.Name}: {ex.Message}");
                }
            }
            _plugins.Clear();
        }
    }
}