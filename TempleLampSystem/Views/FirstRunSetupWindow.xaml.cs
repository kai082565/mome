using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class FirstRunSetupWindow : Window
{
    private readonly IStaffService _staffService;

    public string AdminName { get; private set; } = string.Empty;
    public string AdminPassword { get; private set; } = string.Empty;

    public FirstRunSetupWindow()
    {
        InitializeComponent();
        _staffService = App.Services.GetRequiredService<IStaffService>();
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (string.IsNullOrEmpty(name))
        {
            ShowError("請輸入姓名");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("請輸入密碼");
            return;
        }

        if (password.Length < 4)
        {
            ShowError("密碼至少需要 4 個字元");
            return;
        }

        if (password != confirm)
        {
            ShowError("兩次輸入的密碼不一致");
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            var staff = await _staffService.CreateStaffAsync(name, password, StaffRole.Admin);

            // 立即上傳到 Supabase，確保其他電腦能立刻找到帳號（不必等 AutoSync 30 秒）
            var supabaseService = App.Services.GetRequiredService<ISupabaseService>();
            if (supabaseService.IsConfigured)
            {
                try
                {
                    await supabaseService.UpsertStaffAsync(staff);
                }
                catch
                {
                    // 上傳失敗：讓使用者主動確認後再繼續，AutoSync 會在背景重試
                    MessageBox.Show(
                        "帳號已建立於本機，但無法即時上傳至雲端。\n\n請確認網路連線，其他電腦需等待同步後才能登入。",
                        "雲端同步提醒",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            AdminName = name;
            AdminPassword = password;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"建立失敗：{ex.Message}");
            CreateButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
