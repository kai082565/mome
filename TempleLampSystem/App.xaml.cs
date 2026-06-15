using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Services;
using TempleLampSystem.Views;

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
            // 1. 授權檢查（最優先，在任何初始化之前）
            if (!LicenseService.IsLicensed())
            {
                var licenseWindow = new LicenseActivationWindow();
                if (licenseWindow.ShowDialog() != true)
                {
                    Shutdown(0);
                    return;
                }
            }

            // 2. 首次設定精靈（宮廟資訊 + Supabase），在 DI 初始化之前執行
            if (NeedsInitialSetup())
            {
                var setupWindow = new SetupConfigWindow();
                if (setupWindow.ShowDialog() != true)
                {
                    Shutdown(0);
                    return;
                }
                AppSettings.Reload(); // 清除快取，讓 DI 初始化時讀取新設定
            }

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

            // 嘗試自動登入（從 session.json 讀取上次登入的帳號）
            var sessionService = Services.GetRequiredService<SessionService>();
            var savedStaffId = sessionService.TryLoadSavedStaffId();
            if (savedStaffId != null)
            {
                using var scope = Services.CreateScope();
                var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
                var staff = await staffService.GetByIdAsync(savedStaffId);
                if (staff != null && staff.IsActive)
                {
                    sessionService.Login(staff);
                }
            }

            // 若未登入，顯示登入視窗
            if (!sessionService.IsLoggedIn)
            {
                var loginWindow = new LoginWindow();
                loginWindow.ShowDialog();

                if (!loginWindow.LoginSucceeded)
                {
                    Shutdown(0);
                    return;
                }
            }

            // 檢查更新
            await CheckForUpdatesAsync();

            // 啟動每日定時備份服務
            var backupService = Services.GetRequiredService<IBackupService>();
            backupService.Start();

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

    private static bool NeedsInitialSetup()
    {
        // 如果用戶設定檔已存在，代表之前已完成設定
        if (UserSettings.FileExists()) return false;

        // 如果 appsettings.json 裡已有宮廟名稱（非預設值），代表是舊版升級，不需再設定
        var basePath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (System.IO.File.Exists(basePath))
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(basePath));
                if (doc.RootElement.TryGetProperty("Print", out var print) &&
                    print.TryGetProperty("TempleName", out var nameEl))
                {
                    var name = nameEl.GetString();
                    if (!string.IsNullOrEmpty(name) && name != "○○宮")
                        return false;
                }
            }
            catch { }
        }

        return true;
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
