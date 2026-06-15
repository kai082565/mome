using System.Windows;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class LicenseActivationWindow : Window
{
    private readonly string _machineId;

    public LicenseActivationWindow()
    {
        InitializeComponent();
        _machineId = LicenseService.GetMachineId();
        MachineIdText.Text = _machineId;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_machineId);
        CopyButton.Content = "已複製 ✓";
        Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() => CopyButton.Content = "複製"));
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            ShowError("請輸入授權金鑰");
            return;
        }

        if (!LicenseService.Validate(_machineId, key))
        {
            ShowError("授權金鑰無效，請確認金鑰是否正確或聯絡鎰翔科技");
            return;
        }

        LicenseService.SaveLicense(_machineId, key);
        DialogResult = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
