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

        if (password != confirm)
        {
            ShowError("兩次輸入的密碼不一致");
            return;
        }

        try
        {
            await _staffService.CreateStaffAsync(name, password, StaffRole.Admin);
            AdminName = name;
            AdminPassword = password;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"建立失敗：{ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
