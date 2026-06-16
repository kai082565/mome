using System.Windows;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class SetupConfigWindow : Window
{
    public SetupConfigWindow()
    {
        InitializeComponent();
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        var name = TempleNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ErrorText.Text = "請輸入宮廟名稱";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var settings = new UserSettings
        {
            TempleName    = name,
            TempleAddress = TempleAddressBox.Text.Trim(),
            TemplePhone   = TemplePhoneBox.Text.Trim()
        };
        settings.Save();

        DialogResult = true;
        Close();
    }
}
