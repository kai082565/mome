using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class AdminDashboardWindow
{
    private class PrintFieldEntry
    {
        public string Key      { get; set; } = string.Empty;
        public string Label    { get; set; } = string.Empty;
        public TextBox XBox    { get; set; } = null!;
        public TextBox YBox    { get; set; } = null!;
        public TextBox FontSizeBox { get; set; } = null!;
    }

    private readonly List<PrintFieldEntry> _printFields = new();
    private TextBox? _rocYearBox;
    private const double PreviewScale = 2.0; // 482px / 241mm = 2.0, 284px / 142mm = 2.0

    private static readonly Color[] MarkerColors =
    {
        Color.FromArgb(210, 200, 40,  40),
        Color.FromArgb(210, 30,  120, 200),
        Color.FromArgb(210, 30,  150, 60),
        Color.FromArgb(210, 180, 100, 0),
        Color.FromArgb(210, 120, 30,  180),
        Color.FromArgb(210, 0,   140, 140),
        Color.FromArgb(210, 180, 0,   100),
        Color.FromArgb(210, 80,  80,  0),
        Color.FromArgb(210, 0,   80,  160),
        Color.FromArgb(210, 140, 60,  20),
        Color.FromArgb(210, 20,  100, 100),
        Color.FromArgb(210, 100, 0,   80),
        Color.FromArgb(210, 60,  100, 20),
        Color.FromArgb(210, 0,   60,  120),
        Color.FromArgb(210, 160, 80,  40),
    };

    private void InitPrintSettingsTab()
    {
        var cfg = AppSettings.Instance.CertificateForm;
        var defs = new (string key, string label, CertificateFieldPosition pos)[]
        {
            ("Name",           "大德芳名",  cfg.Name),
            ("CustomerCode",   "客戶編號",  cfg.CustomerCode),
            ("Phone",          "電話",      cfg.Phone),
            ("Address",        "地址",      cfg.Address),
            ("BirthYear",      "生年",      cfg.BirthYear),
            ("BirthMonth",     "生月",      cfg.BirthMonth),
            ("BirthDay",       "生日",      cfg.BirthDay),
            ("BirthHour",      "生時",      cfg.BirthHour),
            ("LampType",       "燈種",      cfg.LampType),
            ("Amount",         "金額",      cfg.Amount),
            ("PrintDate",      "列印日期",  cfg.PrintDate),
            ("LunarStartDate", "農曆起",    cfg.LunarStartDate),
            ("LunarEndDate",   "農曆迄",    cfg.LunarEndDate),
            ("OrderNumber",    "單號",      cfg.OrderNumber),
            ("Temple",         "廟別",      cfg.Temple),
        };

        PrintFieldsPanel.Children.Clear();
        _printFields.Clear();

        // 民國年（只有數值，無座標）
        var rocRow = MakeLabelRow("民國年");
        _rocYearBox = new TextBox
        {
            Width = 55,
            FontSize = 13,
            Padding = new Thickness(4, 2, 4, 2),
            Text = cfg.RocYear.ToString()
        };
        var rocHint = new TextBlock
        {
            Text = "（列印用年份）",
            FontSize = 11,
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        rocRow.Children.Add(_rocYearBox);
        rocRow.Children.Add(rocHint);
        _rocYearBox.TextChanged += PrintSettingsInput_Changed;
        PrintFieldsPanel.Children.Add(rocRow);
        PrintFieldsPanel.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });

        // 各位置欄位
        for (int i = 0; i < defs.Length; i++)
        {
            var (key, label, pos) = defs[i];
            var row = MakeLabelRow(label);
            var xBox  = MakeNumBox(pos.X.ToString("F0"));
            var yBox  = MakeNumBox(pos.Y.ToString("F0"));
            var fsBox = MakeNumBox(pos.FontSize.ToString("F0"), 50);

            row.Children.Add(xBox);
            row.Children.Add(yBox);
            row.Children.Add(fsBox);
            PrintFieldsPanel.Children.Add(row);

            var entry = new PrintFieldEntry
            {
                Key = key, Label = label,
                XBox = xBox, YBox = yBox, FontSizeBox = fsBox
            };
            _printFields.Add(entry);

            xBox.TextChanged += PrintSettingsInput_Changed;
            yBox.TextChanged += PrintSettingsInput_Changed;
        }

        UpdatePreviewMarkers();
    }

    private static StackPanel MakeLabelRow(string label)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 3)
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = 90,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private static TextBox MakeNumBox(string value, double width = 62) => new()
    {
        Width = width,
        FontSize = 13,
        Padding = new Thickness(4, 2, 4, 2),
        Margin = new Thickness(0, 0, 8, 0),
        Text = value
    };

    private void PrintSettingsInput_Changed(object sender, TextChangedEventArgs e)
        => UpdatePreviewMarkers();

    private void UpdatePreviewMarkers()
    {
        // 保留 index 0 的背景圖，移除舊標記
        while (PrintPreviewCanvas.Children.Count > 1)
            PrintPreviewCanvas.Children.RemoveAt(1);

        for (int i = 0; i < _printFields.Count; i++)
        {
            var entry = _printFields[i];
            if (!double.TryParse(entry.XBox.Text, out var xMm)) continue;
            if (!double.TryParse(entry.YBox.Text, out var yMm)) continue;
            if (xMm == 0 && yMm == 0) continue;

            var color = MarkerColors[i % MarkerColors.Length];
            var marker = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 1, 3, 1),
                Child = new TextBlock
                {
                    Text = entry.Label,
                    FontSize = 9,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold
                }
            };

            Canvas.SetLeft(marker, xMm * PreviewScale);
            Canvas.SetTop(marker, yMm * PreviewScale);
            PrintPreviewCanvas.Children.Add(marker);
        }
    }

    private void SavePrintSettings_Click(object sender, RoutedEventArgs e)
    {
        var cfg = BuildCertificateFormFromInputs();
        if (cfg == null) return;

        AppSettings.SaveCertificateForm(cfg);

        PrintSaveStatus.Text = "已儲存 ✓";
        PrintSaveStatus.Foreground = new SolidColorBrush(Color.FromRgb(30, 150, 30));
        PrintSaveStatus.Visibility = Visibility.Visible;
        Task.Delay(3000).ContinueWith(_ =>
            Dispatcher.Invoke(() => PrintSaveStatus.Visibility = Visibility.Collapsed));
    }

    private CertificateFormSettings? BuildCertificateFormFromInputs()
    {
        var existing = AppSettings.Instance.CertificateForm;
        var result = new CertificateFormSettings
        {
            PageWidthMm  = existing.PageWidthMm,
            PageHeightMm = existing.PageHeightMm
        };

        if (!int.TryParse(_rocYearBox?.Text, out var ry))
        {
            ShowPrintError("民國年格式錯誤");
            return null;
        }
        result.RocYear = ry;

        foreach (var entry in _printFields)
        {
            if (!double.TryParse(entry.XBox.Text, out var x) ||
                !double.TryParse(entry.YBox.Text, out var y) ||
                !double.TryParse(entry.FontSizeBox.Text, out var fs))
            {
                ShowPrintError($"「{entry.Label}」數值格式錯誤");
                return null;
            }

            var pos = new CertificateFieldPosition { X = x, Y = y, FontSize = fs };
            switch (entry.Key)
            {
                case "Name":           result.Name           = pos; break;
                case "CustomerCode":   result.CustomerCode   = pos; break;
                case "Phone":          result.Phone          = pos; break;
                case "Address":        result.Address        = pos; break;
                case "BirthYear":      result.BirthYear      = pos; break;
                case "BirthMonth":     result.BirthMonth     = pos; break;
                case "BirthDay":       result.BirthDay       = pos; break;
                case "BirthHour":      result.BirthHour      = pos; break;
                case "LampType":       result.LampType       = pos; break;
                case "Amount":         result.Amount         = pos; break;
                case "PrintDate":      result.PrintDate      = pos; break;
                case "LunarStartDate": result.LunarStartDate = pos; break;
                case "LunarEndDate":   result.LunarEndDate   = pos; break;
                case "OrderNumber":    result.OrderNumber    = pos; break;
                case "Temple":         result.Temple         = pos; break;
            }
        }

        return result;
    }

    private void ShowPrintError(string msg)
    {
        PrintSaveStatus.Text = msg;
        PrintSaveStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 0, 0));
        PrintSaveStatus.Visibility = Visibility.Visible;
    }
}
