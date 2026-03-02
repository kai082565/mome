using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class LoginWindow : Window
{
    private readonly IStaffService _staffService;
    private readonly SessionService _sessionService;

    public bool LoginSucceeded { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        _staffService = App.Services.GetRequiredService<IStaffService>();
        _sessionService = App.Services.GetRequiredService<SessionService>();

        Loaded += async (_, _) => await CheckFirstRunAsync();
    }

    private async Task CheckFirstRunAsync()
    {
        var hasAny = await _staffService.HasAnyStaffAsync();

        if (!hasAny)
        {
            // 本地沒有帳號時，先嘗試從雲端拉取（多台電腦情況：其他電腦已設定過）
            var supabaseService = App.Services.GetRequiredService<ISupabaseService>();
            if (supabaseService.IsConfigured)
            {
                StatusTextBlock.Text = "正在從雲端讀取帳號...";
                StatusTextBlock.Visibility = Visibility.Visible;
                try
                {
                    var cloudStaff = await supabaseService.GetAllStaffAsync();
                    if (cloudStaff.Count > 0)
                    {
                        var context = App.Services.GetRequiredService<AppDbContext>();
                        foreach (var staff in cloudStaff)
                            context.Staff.Add(staff);
                        await context.SaveChangesAsync();
                        hasAny = true;
                    }
                }
                catch { }
                StatusTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        if (!hasAny)
        {
            // 真的是第一次安裝（雲端也沒有帳號），引導建立管理員
            var setupWindow = new FirstRunSetupWindow { Owner = this };
            if (setupWindow.ShowDialog() == true)
            {
                var admin = await _staffService.AuthenticateAsync(setupWindow.AdminName, setupWindow.AdminPassword);
                if (admin != null)
                {
                    _sessionService.Login(admin);
                    LoginSucceeded = true;
                    Close();
                }
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await TryLoginAsync();
    }

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await TryLoginAsync();
    }

    private async Task TryLoginAsync()
    {
        var name = NameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(name))
        {
            ShowError("請輸入姓名");
            return;
        }

        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var staff = await _staffService.AuthenticateAsync(name, password);
            if (staff == null)
            {
                ShowError("姓名或密碼錯誤，或帳號已停用");
                return;
            }

            _sessionService.Login(staff);
            LoginSucceeded = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"登入失敗：{ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
