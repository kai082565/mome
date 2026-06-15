using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class SetupConfigWindow : Window
{
    public SetupConfigWindow()
    {
        InitializeComponent();
        ShowPage(1);
    }

    private void ShowPage(int page)
    {
        Page1.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        Page2.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepLabel.Text = page == 1
            ? "步驟 1 / 2　－　宮廟資訊設定"
            : "步驟 2 / 2　－　雲端同步設定";
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        Page1Error.Visibility = Visibility.Collapsed;
        var name = TempleNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Page1Error.Text = "請輸入宮廟名稱";
            Page1Error.Visibility = Visibility.Visible;
            return;
        }
        ShowPage(2);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        Page2Error.Visibility = Visibility.Collapsed;
        TestStatusText.Visibility = Visibility.Collapsed;
        ShowPage(1);
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        Page2Error.Visibility = Visibility.Collapsed;
        TestStatusText.Visibility = Visibility.Collapsed;

        var url = SupabaseUrlBox.Text.Trim().TrimEnd('/');
        var key = SupabaseKeyBox.Text.Trim();

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            ShowPage2Error("請先填入 URL 和 Anon Key 再測試");
            return;
        }

        TestButton.IsEnabled = false;
        TestButton.Content = "測試中…";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("apikey", key);
            var resp = await http.GetAsync($"{url}/rest/v1/");

            if ((int)resp.StatusCode < 500)
            {
                TestStatusText.Text = "✓ 連線成功";
                TestStatusText.Foreground = new SolidColorBrush(Color.FromRgb(30, 120, 50));
                TestStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                ShowPage2Error($"連線失敗（HTTP {(int)resp.StatusCode}），請確認 URL 正確");
            }
        }
        catch
        {
            ShowPage2Error("無法連線，請確認 URL 是否正確及網路是否正常");
        }
        finally
        {
            TestButton.IsEnabled = true;
            TestButton.Content = "測試連線";
        }
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        Page2Error.Visibility = Visibility.Collapsed;

        var url = SupabaseUrlBox.Text.Trim().TrimEnd('/');
        var key = SupabaseKeyBox.Text.Trim();

        if (string.IsNullOrEmpty(url))   { ShowPage2Error("請填入 Supabase URL"); return; }
        if (string.IsNullOrEmpty(key))   { ShowPage2Error("請填入 Supabase Anon Key"); return; }

        var settings = new UserSettings
        {
            TempleName    = TempleNameBox.Text.Trim(),
            TempleAddress = TempleAddressBox.Text.Trim(),
            TemplePhone   = TemplePhoneBox.Text.Trim(),
            SupabaseUrl      = url,
            SupabaseAnonKey  = key
        };
        settings.Save();

        DialogResult = true;
        Close();
    }

    private void ShowPage2Error(string msg)
    {
        Page2Error.Text = msg;
        Page2Error.Visibility = Visibility.Visible;
    }
}
