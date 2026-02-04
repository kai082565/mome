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

    public ObservableCollection<Lamp> Lamps { get; }
    public ObservableCollection<LampOrderDisplayModel> ExpiringOrders { get; }

    public event EventHandler<Guid>? OrderCreated;

    public async Task InitializeAsync()
    {
        var lamps = await _lampRepository.GetAllOrderedAsync();
        Lamps.Clear();
        foreach (var lamp in lamps)
        {
            Lamps.Add(lamp);
        }

        await LoadExpiringOrdersAsync();
    }

    public async Task SetCustomerAsync(CustomerDisplayModel? customer)
    {
        SelectedCustomer = customer;
        SelectedLamp = null;
        CanOrder = false;
        CannotOrderReason = null;

        StatusMessage = customer == null ? "請先選擇客戶" : $"已選擇客戶：{customer.Name}";
    }

    partial void OnSelectedLampChanged(Lamp? value)
    {
        _ = CheckCanOrderAsync();
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
        if (SelectedCustomer == null)
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
        StatusMessage = "建立點燈紀錄中...";

        try
        {
            var order = await _lampOrderService.CreateLampOrderAsync(SelectedCustomer.Id, SelectedLamp.Id, Price);
            _lastCreatedOrder = order;

            try
            {
                await _supabaseService.UpsertLampOrderAsync(order);
            }
            catch { }

            var result = MessageBox.Show(
                $"點燈成功！\n\n" +
                $"客戶：{SelectedCustomer.Name}\n" +
                $"燈種：{SelectedLamp.LampName}\n" +
                $"期間：{order.StartDate:yyyy/MM/dd} ~ {order.EndDate:yyyy/MM/dd}\n" +
                $"金額：${order.Price}\n\n" +
                $"是否列印單據？",
                "點燈成功",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                await PrintLastOrderAsync();
            }

            StatusMessage = "點燈成功！";
            OrderCreated?.Invoke(this, SelectedCustomer.Id);
            await CheckCanOrderAsync();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "無法點燈", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = ex.Message;
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
