using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using WinFormApp2.PluginBase;

namespace WinFormsApp2.Services
{
    public class PluginManager
    {
        private readonly List<IPlugin> _plugins = new List<IPlugin>();
        private readonly IAppController _api;

        public PluginManager(IAppController api)
        {
            _api = api;
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
    }
}