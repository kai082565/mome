using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempleLampSystem.Models;
using TempleLampSystem.Services;
using TempleLampSystem.Services.Repositories;
using TempleLampSystem.Views;

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
    private string? _selectedTemple;

    [ObservableProperty]
    private string? _selectedDeity;

    [ObservableProperty]
    private string? _quotaInfo;

    [ObservableProperty]
    private string? _orderNote;

    [ObservableProperty]
    private bool _showOrderNote;

    // 燈種預設金額對照表
    private static readonly Dictionary<string, decimal> DefaultPriceMap = new()
    {
        { "TAISUI",       300 },   // 太歲燈
        { "GUANGMING",    300 },   // 光明燈
        { "FACAI",        300 },   // 發財燈
        { "SHENGGUANG",   300 },   // 聖光
        { "HEJIA_PINGAN", 1500 },  // 闔家平安燈
    };

    private bool _isRemovingCustomer;

    public ObservableCollection<Lamp> Lamps { get; }
    public ObservableCollection<LampOrderDisplayModel> ExpiringOrders { get; }

    // 宮廟別選項（可自行輸入）
    public ObservableCollection<string> Temples { get; } = new()
    {
        "福德祠",
        "鳳屏宮",
        "天后宮"
    };

    // 神明別選項（可自行輸入）
    public ObservableCollection<string> Deities { get; } = new()
    {
        "土地公",
        "媽祖",
        "太歲星君",
        "觀世音菩薩"
    };

    // 多選客戶支援
    public ObservableCollection<CustomerDisplayModel> SelectedCustomers { get; } = new();

    public event EventHandler<Guid>? OrderCreated;
    public event EventHandler<IEnumerable<Guid>>? OrdersCreated;
    public event EventHandler<CustomerDisplayModel>? CustomerRemoved;

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "正在載入資料...";

            // 直接載入本地資料（背景同步服務會自動保持資料最新）
            var lamps = await _lampRepository.GetAllOrderedAsync();
            Lamps.Clear();
            foreach (var lamp in lamps)
            {
                Lamps.Add(lamp);
            }

            await LoadExpiringOrdersAsync();
            StatusMessage = "準備就緒";
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
        // 自動帶入宮廟別與神明別
        SelectedTemple = value?.Temple;
        SelectedDeity = value?.Deity;

        // 自動帶入預設金額
        if (value != null && DefaultPriceMap.TryGetValue(value.LampCode, out var defaultPrice))
            Price = defaultPrice;
        else
            Price = 600;

        // 闔家平安燈才顯示備註欄
        ShowOrderNote = value?.LampCode == "HEJIA_PINGAN";
        if (!ShowOrderNote)
            OrderNote = null;

        // 更新剩餘名額
        _ = UpdateQuotaInfoAsync(value);

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

    private async Task UpdateQuotaInfoAsync(Lamp? lamp)
    {
        if (lamp == null)
        {
            QuotaInfo = null;
            return;
        }

        try
        {
            var remaining = await _lampOrderService.GetRemainingQuotaAsync(lamp.Id);
            if (remaining == -1)
            {
                QuotaInfo = null; // 不限量，不顯示
            }
            else if (remaining == 0)
            {
                QuotaInfo = $"今年名額已額滿（限量 {lamp.MaxQuota} 名）";
            }
            else
            {
                QuotaInfo = $"剩餘名額：{remaining} / {lamp.MaxQuota}";
            }
        }
        catch
        {
            QuotaInfo = null;
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
            StyledMessageBox.Show("請先選擇客戶", "提示");
            return;
        }

        if (SelectedLamp == null)
        {
            StyledMessageBox.Show("請選擇燈種", "提示");
            return;
        }

        if (!CanOrder)
        {
            StyledMessageBox.Show(CannotOrderReason ?? "無法點燈", "無法點燈");
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
                    // 點燈前再次即時查詢雲端，防止兩台電腦同時點燈
                    if (_supabaseService.IsConfigured)
                    {
                        var alreadyOrdered = await _supabaseService.HasActiveOrderAsync(customer.Id, SelectedLamp.Id);
                        if (alreadyOrdered)
                        {
                            failedCustomers.Add($"{customer.Name}：該客戶已有未過期的此燈種點燈紀錄（其他電腦剛點過）");
                            continue;
                        }
                    }

                    var order = await _lampOrderService.CreateLampOrderAsync(customer.Id, SelectedLamp.Id, Price, OrderNote);

                    // 先上傳到雲端，確保成功後才算完成
                    try
                    {
                        if (_supabaseService.IsConfigured)
                        {
                            // 先確保客戶和燈種已上傳，再上傳訂單
                            var fullCustomer = await _customerRepository.GetByIdAsync(customer.Id);
                            if (fullCustomer != null)
                                await _supabaseService.UpsertCustomerAsync(fullCustomer);
                            await _supabaseService.UpsertLampAsync(SelectedLamp);
                            await _supabaseService.UpsertLampOrderAsync(order);
                        }
                        createdOrders.Add((customer, order));
                    }
                    catch (Exception syncEx)
                    {
                        // 雲端同步失敗，但本地已建立，仍算成功（離線模式）
                        createdOrders.Add((customer, order));
                        System.Diagnostics.Debug.WriteLine($"雲端同步失敗：{syncEx.Message}");
                        StatusMessage = $"點燈成功，但雲端同步失敗：{syncEx.Message}";
                    }
                }
                catch (Exception ex)
                {
                    failedCustomers.Add($"{customer.Name}：{ex.Message}");
                }
            }

            // 顯示結果
            var successNames = string.Join("、", createdOrders.Select(x => x.Customer.Name));
            var lampInfo = SelectedLamp.LampName;
            if (!string.IsNullOrEmpty(SelectedTemple))
                lampInfo += $"\n宮廟：{SelectedTemple}";
            if (!string.IsNullOrEmpty(SelectedDeity))
                lampInfo += $"\n神明：{SelectedDeity}";

            var message = $"點燈成功！\n\n" +
                $"客戶：{successNames}\n" +
                $"燈種：{lampInfo}\n" +
                $"金額：每人 ${Price:N0}\n" +
                $"共 {createdOrders.Count} 位客戶";

            if (failedCustomers.Count > 0)
            {
                message += $"\n\n以下客戶點燈失敗：\n{string.Join("\n", failedCustomers)}";
            }

            StyledMessageBox.Show(message, "點燈完成");

            // 列印感謝狀
            try
            {
                if (SelectedLamp.LampCode == "HEJIA_PINGAN")
                {
                    // 闔家平安燈：所有客戶合併成一張感謝狀
                    var allCustomerOrders = new List<(Customer, LampOrder)>();
                    foreach (var (customer, order) in createdOrders)
                    {
                        var fullCustomer = await _customerRepository.GetByIdAsync(customer.Id);
                        if (fullCustomer != null)
                            allCustomerOrders.Add((fullCustomer, order));
                    }
                    if (allCustomerOrders.Count > 0)
                    {
                        var certData = CertificateData.FromFamilyOrder(allCustomerOrders, SelectedLamp);
                        await _printService.PrintCertificateAsync(certData);
                    }
                }
                else
                {
                    // 一般燈種：每位客戶各印一張
                    foreach (var (customer, order) in createdOrders)
                    {
                        var fullCustomer = await _customerRepository.GetByIdAsync(customer.Id);
                        if (fullCustomer != null)
                        {
                            var certData = CertificateData.FromOrder(order, fullCustomer, SelectedLamp);
                            await _printService.PrintCertificateAsync(certData);
                        }
                    }
                }
            }
            catch (Exception printEx)
            {
                StyledMessageBox.Show($"列印感謝狀失敗：{printEx.Message}", "列印錯誤");
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
            StyledMessageBox.Show($"點燈失敗：{ex.Message}", "錯誤");
            StatusMessage = $"點燈失敗：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadExpiringOrdersAsync()
    {
        try
        {
            // 直接查詢本地資料（背景同步服務會自動保持資料最新）
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
