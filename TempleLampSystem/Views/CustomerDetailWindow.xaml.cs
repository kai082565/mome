using System.Windows;
using TempleLampSystem.Models;

namespace TempleLampSystem.Views;

public partial class CustomerDetailWindow : Window
{
    public CustomerDetailWindow(Customer customer)
    {
        InitializeComponent();
        LoadCustomerData(customer);
    }

    private void LoadCustomerData(Customer customer)
    {
        TitleText.Text = $"{customer.Name} 的資料";

        NameText.Text = customer.Name;
        PhoneText.Text = customer.Phone ?? "-";
        MobileText.Text = customer.Mobile ?? "-";
        PostalCodeText.Text = customer.PostalCode ?? "-";
        VillageText.Text = customer.Village ?? "-";
        AddressText.Text = customer.Address ?? "-";
        NoteText.Text = customer.Note ?? "-";

        // 出生日期（處理吉年/吉月/吉日）
        var yearStr = customer.BirthYear switch
        {
            null => "",
            0 => "吉年",
            _ => $"民國{customer.BirthYear}年"
        };
        var monthStr = customer.BirthMonth switch
        {
            null => "",
            0 => "吉月",
            _ => $"{customer.BirthMonth}月"
        };
        var dayStr = customer.BirthDay switch
        {
            null => "",
            0 => "吉日",
            _ => $"{customer.BirthDay}日"
        };
        var birthParts = new[] { yearStr, monthStr, dayStr }.Where(s => s.Length > 0);
        BirthDateText.Text = birthParts.Any() ? string.Join(" ", birthParts) : "-";

        // 生辰
        BirthHourText.Text = customer.BirthHour ?? "-";

        // 生肖
        if (customer.BirthYear is > 0)
        {
            ZodiacText.Text = $"({customer.Zodiac})";
        }

        // 點燈紀錄
        var orders = customer.LampOrders
            .OrderByDescending(o => o.Year)
            .ThenBy(o => o.Lamp.LampName)
            .Select(o => new LampOrderDisplayModel
            {
                Id = o.Id,
                LampName = o.Lamp.LampName,
                Year = o.Year,
                StartDate = o.StartDate,
                EndDate = o.EndDate,
                Price = o.Price
            })
            .ToList();

        if (orders.Count == 0)
        {
            NoOrdersText.Visibility = Visibility.Visible;
            OrderHeaderGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
            OrdersItemsControl.ItemsSource = orders;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
