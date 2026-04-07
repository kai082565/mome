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
                Price = o.Price,
                StaffName = o.StaffName
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
            // 1) 先刪除本機資料（本地刪除成功後，雲端刪除失敗可透過 SyncQueue 重試）
            var localOrders = await _lampOrderRepository.GetByCustomerIdAsync(_customer.Id);

            foreach (var order in localOrders)
            {
                await _lampOrderRepository.DeleteAsync(order);
            }

            var customerToDelete = await _customerRepository.GetByIdAsync(_customer.Id);
            if (customerToDelete != null)
            {
                await _customerRepository.DeleteAsync(customerToDelete);
            }

            // 2) 嘗試刪除雲端資料；失敗時加入 SyncQueue 排隊重試
            if (_supabaseService.IsConfigured)
            {
                try
                {
                    var cloudOrders = await _supabaseService.GetLampOrdersByCustomerAsync(_customer.Id);
                    foreach (var order in cloudOrders)
                    {
                        await _supabaseService.DeleteLampOrderAsync(order.Id);
                    }
                    await _supabaseService.DeleteCustomerAsync(_customer.Id);
                }
                catch
                {
                    // 雲端刪除失敗，加入 SyncQueue 背景重試
                    foreach (var order in localOrders)
                    {
                        await _syncQueueService.EnqueueAsync(order, SyncOperation.Delete);
                    }
                    await _syncQueueService.EnqueueAsync(_customer, SyncOperation.Delete);
                }
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

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        // 取得最新的 Customer 物件（含 LampOrders，以便 Edit 完後重新顯示）
        var customer = await _customerRepository.GetWithOrdersAsync(_customer.Id);
        if (customer == null) return;

        var editWindow = new AddCustomerWindow(customer)
        {
            Owner = this
        };

        if (editWindow.ShowDialog() != true || editWindow.EditedCustomer == null)
            return;

        try
        {
            await _customerRepository.UpdateAsync(editWindow.EditedCustomer);

            // 同步到雲端
            if (_supabaseService.IsConfigured)
            {
                try { await _supabaseService.UpsertCustomerAsync(editWindow.EditedCustomer); }
                catch { await _syncQueueService.EnqueueAsync(editWindow.EditedCustomer, SyncOperation.Update); }
            }

            // 重新載入畫面
            var refreshed = await _customerRepository.GetWithOrdersAsync(_customer.Id);
            if (refreshed != null)
                LoadCustomerData(refreshed);

            StyledMessageBox.Show("客戶資料已更新。", "儲存成功");
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"儲存失敗：{ex.Message}", "錯誤");
        }
    }

    private void PrintInfoButton_Click(object sender, RoutedEventArgs e)
    {
        var letter = CustomerInfoLetter.FromCustomer(_customer);
        var previewWindow = new PrintPreviewWindow(letter)
        {
            Owner = this
        };
        previewWindow.ShowDialog();
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
