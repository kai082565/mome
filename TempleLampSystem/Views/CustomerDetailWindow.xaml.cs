using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.Views;

public partial class CustomerDetailWindow : Window
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILampOrderRepository _lampOrderRepository;
    private readonly ISupabaseService _supabaseService;
    private readonly ISyncQueueService _syncQueueService;
    private readonly IPrintService _printService;
    private readonly Customer _customer;
    private List<FamilyMemberDisplayModel> _familyMembers = new();

    public bool CustomerWasDeleted { get; private set; }

    public CustomerDetailWindow(Customer customer)
    {
        InitializeComponent();
        _customer = customer;
        _customerRepository = App.Services.GetRequiredService<ICustomerRepository>();
        _lampOrderRepository = App.Services.GetRequiredService<ILampOrderRepository>();
        _supabaseService = App.Services.GetRequiredService<ISupabaseService>();
        _syncQueueService = App.Services.GetRequiredService<ISyncQueueService>();
        _printService = App.Services.GetRequiredService<IPrintService>();
        LoadCustomerData(customer);
        _ = LoadFamilyMembersAsync(customer.Id);
    }

    private void LoadCustomerData(Customer customer)
    {
        TitleText.Text = $"{customer.Name} 的資料";

        CustomerCodeText.Text = customer.CustomerCode ?? "-";
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

    private async void DeceasedButton_Click(object sender, RoutedEventArgs e)
    {
        var result = StyledMessageBox.Show(
            $"確定要刪除「{_customer.Name}」的所有資料嗎？\n\n此操作無法復原，將刪除該客戶及其所有點燈紀錄。",
            "已故 - 確認刪除",
            MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            // 1) 先刪除 Supabase 資料（必須先成功，否則下次同步會把資料拉回來）
            var supabaseFailed = false;
            if (_supabaseService.IsConfigured)
            {
                try
                {
                    // 刪除雲端 LampOrder
                    var cloudOrders = await _supabaseService.GetLampOrdersByCustomerAsync(_customer.Id);
                    foreach (var order in cloudOrders)
                    {
                        await _supabaseService.DeleteLampOrderAsync(order.Id);
                    }

                    // 刪除雲端 Customer
                    await _supabaseService.DeleteCustomerAsync(_customer.Id);
                }
                catch (Exception ex)
                {
                    supabaseFailed = true;
                    var retry = StyledMessageBox.Show(
                        $"雲端刪除失敗：{ex.Message}\n\n是否仍要刪除本機資料？\n（下次同步可能會從雲端恢復資料）",
                        "雲端刪除失敗",
                        MessageBoxButton.YesNo);

                    if (retry != MessageBoxResult.Yes) return;
                }
            }

            // 2) 刪除本機資料
            // 取得該客戶所有 LampOrder（從當前 DbContext 查詢）
            var localOrders = await _lampOrderRepository.GetByCustomerIdAsync(_customer.Id);

            // 逐筆刪除本機 LampOrder（因為 RESTRICT 必須先刪 orders）
            foreach (var order in localOrders)
            {
                await _lampOrderRepository.DeleteAsync(order);
            }

            // 從當前 DbContext 重新查詢 Customer，確保在追蹤範圍內
            var customerToDelete = await _customerRepository.GetByIdAsync(_customer.Id);
            if (customerToDelete != null)
            {
                await _customerRepository.DeleteAsync(customerToDelete);
            }

            // 3) 若 Supabase 刪除失敗，將刪除操作加入 SyncQueue 排隊重試
            if (supabaseFailed)
            {
                foreach (var order in localOrders)
                {
                    await _syncQueueService.EnqueueAsync(order, SyncOperation.Delete);
                }
                await _syncQueueService.EnqueueAsync(_customer, SyncOperation.Delete);
            }

            CustomerWasDeleted = true;
            StyledMessageBox.Show($"已刪除「{_customer.Name}」的所有資料。", "刪除完成");
            Close();
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"刪除失敗：{ex.Message}", "錯誤");
        }
    }

    private async void PrintInfoButton_Click(object sender, RoutedEventArgs e)
    {
        var result = StyledMessageBox.Show(
            "請選擇列印方式：\n\n「是」= 直接列印\n「否」= 儲存為 PDF",
            "列印客戶資料",
            MessageBoxButton.YesNoCancel);

        if (result == MessageBoxResult.Cancel) return;

        try
        {
            var letter = CustomerInfoLetter.FromCustomer(_customer);

            if (result == MessageBoxResult.Yes)
            {
                await _printService.PrintCustomerLetterAsync(letter);
            }
            else if (result == MessageBoxResult.No)
            {
                var path = await _printService.SaveCustomerLetterAsPdfAsync(letter);
                if (!string.IsNullOrEmpty(path))
                {
                    StyledMessageBox.Show($"已儲存至：\n{path}", "儲存完成");
                }
            }
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"列印失敗：{ex.Message}", "錯誤");
        }
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
