using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Services;

namespace TempleLampSystem;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 設定全域例外處理器，防止未捕捉的例外導致閃退
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            // 設定 DI 容器
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddAppServices();
            Services = serviceCollection.BuildServiceProvider();

            // 初始化資料庫（先跑遷移加欄位，再跑初始化寫資料）
            using (var scope = Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DbMigrationService.ApplyMigrations(context);
                DbInitializer.Initialize(context);
            }

            // 檢查更新
            await CheckForUpdatesAsync();

            // 手動啟動 MainWindow
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"程式啟動失敗：\n{ex.Message}", "啟動錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            $"發生未預期的錯誤：\n{e.Exception.Message}\n\n程式將繼續執行。",
            "錯誤",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"發生嚴重錯誤：\n{ex.Message}",
                "嚴重錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        // 背景 Task 的未觀察例外，記錄但不中斷程式
    }

    private async Task CheckForUpdatesAsync()
    {
        var settings = AppSettings.Instance.Update;
        if (!settings.AutoCheck || string.IsNullOrEmpty(settings.CheckUrl))
            return;

        try
        {
            var updateService = Services.GetRequiredService<IUpdateService>();
            var updateInfo = await updateService.CheckForUpdateAsync();

            if (updateInfo != null)
            {
                var result = MessageBox.Show(
                    $"發現新版本 {updateInfo.Version}！\n\n" +
                    $"更新內容：\n{updateInfo.ReleaseNotes}\n\n" +
                    $"是否立即更新？",
                    "軟體更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    await updateService.DownloadAndInstallAsync(updateInfo);
                }
            }
        }
        catch
        {
            // 更新檢查失敗，靜默忽略
        }
    }
}
