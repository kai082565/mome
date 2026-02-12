using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class PrintPreviewWindow : Window
{
    private readonly IPrintService _printService;
    private readonly CustomerInfoLetter _letter;

    public PrintPreviewWindow(CustomerInfoLetter letter)
    {
        InitializeComponent();
        _letter = letter;
        _printService = App.Services.GetRequiredService<IPrintService>();
        LoadData();
    }

    private void LoadData()
    {
        TitleText.Text = $"{_letter.CustomerName} - 列印預覽";

        // 客戶資料
        CustomerCodeText.Text = _letter.CustomerCode ?? "-";
        NameText.Text = _letter.CustomerName;
        PhoneText.Text = _letter.CustomerPhone ?? "-";
        MobileText.Text = _letter.CustomerMobile ?? "-";

        var fullAddress = string.Join("", new[]
        {
            _letter.CustomerPostalCode,
            _letter.CustomerVillage,
            _letter.CustomerAddress
        }.Where(s => !string.IsNullOrEmpty(s)));
        AddressText.Text = !string.IsNullOrEmpty(fullAddress) ? fullAddress : "-";

        // 點燈紀錄
        if (_letter.Orders.Count == 0)
        {
            NoOrdersText.Visibility = Visibility.Visible;
            OrderHeaderGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
            OrdersItemsControl.ItemsSource = _letter.Orders;
        }
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _letter.Note = string.IsNullOrWhiteSpace(NoteTextBox.Text) ? null : NoteTextBox.Text.Trim();
            await _printService.PrintCustomerLetterAsync(_letter);
            Close();
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"列印失敗：{ex.Message}", "錯誤");
        }
    }

    private async void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _letter.Note = string.IsNullOrWhiteSpace(NoteTextBox.Text) ? null : NoteTextBox.Text.Trim();
            var path = await _printService.SaveCustomerLetterAsPdfAsync(_letter);
            if (!string.IsNullOrEmpty(path))
            {
                StyledMessageBox.Show($"已儲存至：\n{path}", "儲存完成");
                Close();
            }
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"列印失敗：{ex.Message}", "錯誤");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
