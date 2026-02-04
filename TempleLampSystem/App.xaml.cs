using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Services;

namespace TempleLampSystem;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 設定 DI 容器
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddAppServices();
        Services = serviceCollection.BuildServiceProvider();

        // 初始化資料庫
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DbInitializer.Initialize(context);
            DbMigrationService.ApplyMigrations(context);
        }

        // 檢查更新
        await CheckForUpdatesAsync();

        // 手動啟動 MainWindow
        var mainWindow = new MainWindow();
        mainWindow.Show();
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
