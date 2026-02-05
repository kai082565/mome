using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempleLampSystem.Models;
using TempleLampSystem.Services;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.ViewModels;

public partial class LampOrderViewModel : ViewModelBase
{
    private readonly ILampRepository _lampRepository;
    private readonly ILampOrderService _lampOrderService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPrintService _printService;
    private readonly ISupabaseService _supabaseService;

    public LampOrderViewModel(
        ILampRepository lampRepository,
        ILampOrderService lampOrderService,
        ICustomerRepository customerRepository,
        IPrintService printService,
        ISupabaseService supabaseService)
    {
        _lampRepository = lampRepository;
        _lampOrderService = lampOrderService;
        _customerRepository = customerRepository;
        _printService = printService;
        _supabaseService = supabaseService;

        Lamps = new ObservableCollection<Lamp>();
        ExpiringOrders = new ObservableCollection<LampOrderDisplayModel>();
    }

    [ObservableProperty]
    private CustomerDisplayModel? _selectedCustomer;

    [ObservableProperty]
    private Lamp? _selectedLamp;

    [ObservableProperty]
    private decimal _price = 600;

    [ObservableProperty]
    private bool _canOrder;

    [ObservableProperty]
    private string? _cannotOrderReason;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string? _syncStatus;

    private LampOrder? _lastCreatedOrder;
    private bool _isRemovingCustomer;

    public ObservableCollection<Lamp> Lamps { get; }
    public ObservableCollection<LampOrderDisplayModel> ExpiringOrders { get; }

    // 多選客戶支援
    public ObservableCollection<CustomerDisplayModel> SelectedCustomers { get; } = new();

    public event EventHandler<Guid>? OrderCreated;
    public event EventHandler<IEnumerable<Guid>>? OrdersCreated;
    public event EventHandler<CustomerDisplayModel>? CustomerRemoved;

    public async Task InitializeAsync()
    {
        try
        {
            var lamps = await _lampRepository.GetAllOrderedAsync();
            Lamps.Clear();
            foreach (var lamp in lamps)
            {
                Lamps.Add(lamp);
            }

            await LoadExpiringOrdersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失敗：{ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveSelectedCustomer(CustomerDisplayModel customer)
    {
        _isRemovingCustomer = true;
        try
        {
            SelectedCustomers.Remove(customer);

            if (SelectedCustomers.Count == 0)
            {
                SelectedCustomer = null;
                CanOrder = false;
                CannotOrderReason = null;
                StatusMessage = "請先選擇客戶";
            }
            else
            {
                SelectedCustomer = SelectedCustomers[0];
                StatusMessage = SelectedCustomers.Count == 1
                    ? $"已選擇客戶：{SelectedCustomers[0].Name}"
                    : $"已選擇 {SelectedCustomers.Count} 位客戶";

                // 重新檢查是否可以點燈
                _ = SafeCheckCanOrderForMultipleAsync();
            }

            // 通知 MainWindow 同步取消左側的選取
            CustomerRemoved?.Invoke(this, customer);
        }
        finally
        {
            _isRemovingCustomer = false;
        }
    }

    public async Task SetCustomerAsync(CustomerDisplayModel? customer)
    {
        // 如果是同一個客戶（點燈後刷新），保留燈種選擇
        bool isSameCustomer = customer != null && SelectedCustomer?.Id == customer.Id;

        SelectedCustomer = customer;

        if (isSameCustomer)
        {
            // 同一客戶，只重新檢查是否還能點同一燈種
            await CheckCanOrderAsync();
        }
        else
        {
            // 不同客戶才重置
            SelectedLamp = null;
            CanOrder = false;
            CannotOrderReason = null;
        }

        StatusMessage = customer == null ? "請先選擇客戶" : $"已選擇客戶：{customer.Name}";
    }

    public void SetSelectedCustomers(IList<CustomerDisplayModel> customers)
    {
        // 如果正在從右側移除客戶，不要重複處理
        if (_isRemovingCustomer) return;

        SelectedCustomers.Clear();
        foreach (var customer in customers)
        {
            SelectedCustomers.Add(customer);
        }

        // 更新狀態訊息
        if (SelectedCustomers.Count == 0)
        {
            StatusMessage = "請先選擇客戶";
        }
        else if (SelectedCustomers.Count == 1)
        {
            SelectedCustomer = SelectedCustomers[0];
            StatusMessage = $"已選擇客戶：{SelectedCustomers[0].Name}";
        }
        else
        {
            SelectedCustomer = SelectedCustomers[0]; // 設定第一個為主要選擇
            StatusMessage = $"已選擇 {SelectedCustomers.Count} 位客戶";
        }

        // 重新檢查是否可以點燈
        _ = SafeCheckCanOrderForMultipleAsync();
    }

    private async Task SafeCheckCanOrderForMultipleAsync()
    {
        try
        {
            await CheckCanOrderForMultipleAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"檢查點燈狀態失敗：{ex.Message}";
        }
    }

    private async Task SafeCheckCanOrderAsync()
    {
        try
        {
            await CheckCanOrderAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"檢查點燈狀態失敗：{ex.Message}";
        }
    }

    private async Task CheckCanOrderForMultipleAsync()
    {
        if (SelectedCustomers.Count == 0 || SelectedLamp == null)
        {
            CanOrder = false;
            CannotOrderReason = null;
            return;
        }

        IsBusy = true;
        try
        {
            // 檢查所有選中的客戶是否都可以點這個燈
            var cannotOrderCustomers = new List<string>();

            foreach (var customer in SelectedCustomers)
            {
                var canOrder = await _lampOrderService.CanOrderLampAsync(customer.Id, SelectedLamp.Id);
                if (!canOrder)
                {
                    var reason = await _lampOrderService.GetCannotOrderReasonAsync(customer.Id, SelectedLamp.Id);
                    cannotOrderCustomers.Add($"{customer.Name}：{reason}");
                }
            }

            if (cannotOrderCustomers.Count == 0)
            {
                CanOrder = true;
                CannotOrderReason = null;
            }
            else
            {
                CanOrder = false;
                CannotOrderReason = string.Join("\n", cannotOrderCustomers);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedLampChanged(Lamp? value)
    {
        // 如果有多選客戶，檢查多選；否則檢查單選
        if (SelectedCustomers.Count > 0)
        {
            _ = SafeCheckCanOrderForMultipleAsync();
        }
        else
        {
            _ = SafeCheckCanOrderAsync();
        }
    }

    private async Task CheckCanOrderAsync()
    {
        if (SelectedCustomer == null || SelectedLamp == null)
        {
            CanOrder = false;
            CannotOrderReason = null;
            return;
        }

        IsBusy = true;
        try
        {
            CanOrder = await _lampOrderService.CanOrderLampAsync(SelectedCustomer.Id, SelectedLamp.Id);

            CannotOrderReason = CanOrder
                ? null
                : await _lampOrderService.GetCannotOrderReasonAsync(SelectedCustomer.Id, SelectedLamp.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        // 判斷是單選還是多選
        var customersToOrder = SelectedCustomers.Count > 0
            ? SelectedCustomers.ToList()
            : (SelectedCustomer != null ? new List<CustomerDisplayModel> { SelectedCustomer } : new List<CustomerDisplayModel>());

        if (customersToOrder.Count == 0)
        {
            MessageBox.Show("請先選擇客戶", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedLamp == null)
        {
            MessageBox.Show("請選擇燈種", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!CanOrder)
        {
            MessageBox.Show(CannotOrderReason ?? "無法點燈", "無法點燈", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = $"正在為 {customersToOrder.Count} 位客戶建立點燈紀錄...";

        try
        {
            var createdOrders = new List<(CustomerDisplayModel Customer, LampOrder Order)>();
            var failedCustomers = new List<string>();

            foreach (var customer in customersToOrder)
            {
                try
                {
                    var order = await _lampOrderService.CreateLampOrderAsync(customer.Id, SelectedLamp.Id, Price);
                    createdOrders.Add((customer, order));

                    try
                    {
                        await _supabaseService.UpsertLampOrderAsync(order);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    failedCustomers.Add($"{customer.Name}：{ex.Message}");
                }
            }

            // 記錄最後一筆訂單（用於列印）
            if (createdOrders.Count > 0)
            {
                _lastCreatedOrder = createdOrders.Last().Order;
            }

            // 顯示結果
            var successNames = string.Join("、", createdOrders.Select(x => x.Customer.Name));
            var message = $"點燈成功！\n\n" +
                $"客戶：{successNames}\n" +
                $"燈種：{SelectedLamp.LampName}\n" +
                $"金額：每人 ${Price:N0}\n" +
                $"共 {createdOrders.Count} 位客戶";

            if (failedCustomers.Count > 0)
            {
                message += $"\n\n⚠️ 以下客戶點燈失敗：\n{string.Join("\n", failedCustomers)}";
            }

            message += "\n\n是否列印單據？";

            var result = MessageBox.Show(message, "點燈完成", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                await PrintLastOrderAsync();
            }

            StatusMessage = $"已完成 {createdOrders.Count} 位客戶點燈";

            // 通知更新
            foreach (var (customer, _) in createdOrders)
            {
                OrderCreated?.Invoke(this, customer.Id);
            }

            // 重新檢查點燈狀態
            if (SelectedCustomers.Count > 0)
            {
                await CheckCanOrderForMultipleAsync();
            }
            else
            {
                await CheckCanOrderAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"點燈失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = $"點燈失敗：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PrintLastOrderAsync()
    {
        if (_lastCreatedOrder == null || SelectedCustomer == null || SelectedLamp == null)
        {
            MessageBox.Show("沒有可列印的單據", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var customer = await _customerRepository.GetByIdAsync(SelectedCustomer.Id);
        if (customer == null) return;

        var receipt = Receipt.FromLampOrder(_lastCreatedOrder, customer, SelectedLamp);

        var choice = MessageBox.Show(
            "請選擇輸出方式：\n\n「是」= 直接列印\n「否」= 儲存為 PDF",
            "列印單據",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choice == MessageBoxResult.Yes)
        {
            await _printService.PrintReceiptAsync(receipt);
        }
        else if (choice == MessageBoxResult.No)
        {
            var path = await _printService.SaveReceiptAsPdfAsync(receipt);
            if (!string.IsNullOrEmpty(path))
            {
                MessageBox.Show($"PDF 已儲存至：\n{path}", "儲存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    [RelayCommand]
    private void PreviewReceipt()
    {
        if (_lastCreatedOrder == null || SelectedCustomer == null || SelectedLamp == null)
        {
            MessageBox.Show("沒有可預覽的單據", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var customer = new Customer
        {
            Id = SelectedCustomer.Id,
            Name = SelectedCustomer.Name,
            Phone = SelectedCustomer.Phone,
            Mobile = SelectedCustomer.Mobile,
            Address = SelectedCustomer.Address
        };

        var receipt = Receipt.FromLampOrder(_lastCreatedOrder, customer, SelectedLamp);
        _printService.PreviewReceipt(receipt);
    }

    [RelayCommand]
    private async Task SyncToCloudAsync()
    {
        IsSyncing = true;
        SyncStatus = "正在上傳至雲端...";

        try
        {
            var result = await _supabaseService.SyncToCloudAsync();

            if (result.Success)
            {
                SyncStatus = $"上傳完成：{result.CustomersUploaded} 位客戶，{result.OrdersUploaded} 筆點燈紀錄";
                MessageBox.Show(
                    $"同步完成！\n\n上傳 {result.CustomersUploaded} 位客戶\n上傳 {result.OrdersUploaded} 筆點燈紀錄",
                    "同步成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                SyncStatus = $"同步失敗：{result.ErrorMessage}";
                MessageBox.Show($"同步失敗：{result.ErrorMessage}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            SyncStatus = $"同步失敗：{ex.Message}";
            MessageBox.Show($"同步失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task SyncFromCloudAsync()
    {
        IsSyncing = true;
        SyncStatus = "正在從雲端下載...";

        try
        {
            var result = await _supabaseService.SyncFromCloudAsync();

            if (result.Success)
            {
                SyncStatus = $"下載完成：{result.CustomersDownloaded} 位客戶，{result.OrdersDownloaded} 筆點燈紀錄";
                MessageBox.Show(
                    $"同步完成！\n\n下載 {result.CustomersDownloaded} 位客戶\n下載 {result.OrdersDownloaded} 筆點燈紀錄",
                    "同步成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                SyncStatus = $"同步失敗：{result.ErrorMessage}";
                MessageBox.Show($"同步失敗：{result.ErrorMessage}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            SyncStatus = $"同步失敗：{ex.Message}";
            MessageBox.Show($"同步失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public async Task LoadExpiringOrdersAsync()
    {
        try
        {
            var orders = await _lampOrderService.GetExpiringOrdersAsync(30);

            ExpiringOrders.Clear();
            foreach (var order in orders)
            {
                ExpiringOrders.Add(new LampOrderDisplayModel
                {
                    Id = order.Id,
                    CustomerName = order.Customer.Name,
                    LampName = order.Lamp.LampName,
                    Year = order.Year,
                    StartDate = order.StartDate,
                    EndDate = order.EndDate,
                    Price = order.Price
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"載入到期提醒失敗：{ex.Message}";
        }
    }
}
