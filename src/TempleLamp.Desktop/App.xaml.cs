using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TempleLamp.Desktop.Services;
using TempleLamp.Desktop.ViewModels;

namespace TempleLamp.Desktop;

/// <summary>
/// App.xaml 的互動邏輯
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 工作站 ID（模擬櫃檯編號）
    /// </summary>
    public static string WorkstationId { get; } = $"WS-{Environment.MachineName}";

    /// <summary>
    /// API 基底網址
    /// </summary>
    public static string ApiBaseUrl { get; } = "http://localhost:5000/";

    /// <summary>
    /// 服務提供者
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 設定 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 建立主視窗
        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 註冊 HttpClient（帶 X-Workstation-Id Header）
        services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrl);
            client.DefaultRequestHeaders.Add("X-Workstation-Id", WorkstationId);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 註冊 ViewModel
        services.AddTransient<MainViewModel>();
    }
}
