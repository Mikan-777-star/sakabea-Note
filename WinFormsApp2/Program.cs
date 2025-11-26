using WinFormsApp2.Presenters;
using WinFormsApp2.Services;

namespace WinFormsApp2
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // 1. サービスの生成
            var fileManager = new FileManager(Directory.GetCurrentDirectory()); // ここでパス設定
            var backupManager = new BackupManager();

            // 2. View (Form) の生成
            var form = new Form1();

            // 3. Presenter の生成 (ViewとServiceを注入)
            // これを作った瞬間にイベントの紐づけが行われるわ
            var presenter = new MainPresenter(form, fileManager, backupManager);

            // 4. アプリ起動
            Application.Run(form);

            presenter.Dispose();
        }
    }
}