using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
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
        if (hasAny) return; // 本地已有帳號，正常顯示登入表單

        // 本地無帳號：必須先向雲端確認
        var supabaseService = App.Services.GetRequiredService<ISupabaseService>();
        if (!supabaseService.IsConfigured)
        {
            // 未設定 Supabase（純單機模式），直接進入首次設定
            await ShowFirstRunSetupAsync();
            return;
        }

        // 有設定 Supabase：嘗試從雲端拉取帳號
        SetLoginFormEnabled(false);
        StatusTextBlock.Text = "正在從雲端讀取帳號...";
        StatusTextBlock.Visibility = Visibility.Visible;

        try
        {
            // GetAllStaffAsync 若查詢失敗會拋出例外（由外層 catch 攔截顯示錯誤）
            // 這樣才能正確區分「雲端真的沒有帳號」vs「查詢失敗」兩種情況
            var cloudStaff = await supabaseService.GetAllStaffAsync();
            StatusTextBlock.Visibility = Visibility.Collapsed;

            if (cloudStaff.Count > 0)
            {
                // 雲端有帳號：同步到本地後正常顯示登入表單
                using var scope = App.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cloudStaffIds = cloudStaff.Select(s => s.Id).ToHashSet();
                var localStaffDict = await context.Staff
                    .Where(s => cloudStaffIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id);
                foreach (var staff in cloudStaff)
                {
                    if (!localStaffDict.ContainsKey(staff.Id))
                        context.Staff.Add(staff);
                }
                await context.SaveChangesAsync();
                SetLoginFormEnabled(true);
                return;
            }

            // 確認查詢成功且雲端真的沒有帳號 → 首次安裝
            SetLoginFormEnabled(true);
            await ShowFirstRunSetupAsync();
        }
        catch
        {
            StatusTextBlock.Visibility = Visibility.Collapsed;
            ShowCloudError("無法連線至雲端伺服器，請確認：\n1. 網路是否正常\n2. Supabase 專案是否已暫停（免費方案閒置會自動暫停）");
        }
    }

    private async Task ShowFirstRunSetupAsync()
    {
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

    private void SetLoginFormEnabled(bool enabled)
    {
        NameTextBox.IsEnabled = enabled;
        PasswordBox.IsEnabled = enabled;
        LoginButton.IsEnabled = enabled;
    }

    private void ShowCloudError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        RetryButton.Visibility = Visibility.Visible;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        await CheckFirstRunAsync();
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
