using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.Views;

public partial class CustomerDetailWindow : Window
{
    private readonly ICustomerRepository _customerRepository;
    private List<FamilyMemberDisplayModel> _familyMembers = new();

    public CustomerDetailWindow(Customer customer)
    {
        InitializeComponent();
        _customerRepository = App.Services.GetRequiredService<ICustomerRepository>();
        LoadCustomerData(customer);
        _ = LoadFamilyMembersAsync(customer.Id);
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

    private async Task LoadFamilyMembersAsync(Guid customerId)
    {
        try
        {
            var familyMembers = await _customerRepository.GetFamilyMembersAsync(customerId);

            if (familyMembers.Count > 0)
            {
                _familyMembers = familyMembers.Select(c => new FamilyMemberDisplayModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    OrderSummary = c.LampOrders.Count > 0
                        ? $"已點 {c.LampOrders.Count} 種燈"
                        : "尚無點燈紀錄"
                }).ToList();

                FamilyItemsControl.ItemsSource = _familyMembers;
                FamilyBorder.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // 載入家人失敗時不顯示
        }
    }

    private async void FamilyMember_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is FamilyMemberDisplayModel member)
        {
            try
            {
                var customer = await _customerRepository.GetWithOrdersAsync(member.Id);
                if (customer == null) return;

                var window = new CustomerDetailWindow(customer)
                {
                    Owner = this.Owner
                };
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入客戶資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

public class FamilyMemberDisplayModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OrderSummary { get; set; } = string.Empty;
}
