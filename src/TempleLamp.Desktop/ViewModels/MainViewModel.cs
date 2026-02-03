using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempleLamp.Desktop.Models;
using TempleLamp.Desktop.Services;

namespace TempleLamp.Desktop.ViewModels;

/// <summary>
/// 主視窗 ViewModel - 協調整個點燈流程
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    // ===== 當前步驟 =====
    [ObservableProperty]
    private int _currentStep = 1;

    // ===== 燈位選擇 =====
    [ObservableProperty]
    private ObservableCollection<LampType> _lampTypes = new();

    [ObservableProperty]
    private LampType? _selectedLampType;

    [ObservableProperty]
    private ObservableCollection<LampSlot> _lampSlots = new();

    [ObservableProperty]
    private ObservableCollection<LampSlot> _selectedSlots = new();

    // ===== 客戶資料 =====
    [ObservableProperty]
    private string _searchPhone = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Customer> _searchResults = new();

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private string _newCustomerName = string.Empty;

    [ObservableProperty]
    private string _newCustomerPhone = string.Empty;

    [ObservableProperty]
    private string? _newCustomerAddress;

    [ObservableProperty]
    private bool _isCreatingNewCustomer;

    // ===== 訂單資料 =====
    [ObservableProperty]
    private string _lightingName = string.Empty;

    [ObservableProperty]
    private string? _blessingContent;

    [ObservableProperty]
    private string? _orderNotes;

    [ObservableProperty]
    private Order? _currentOrder;

    // ===== 付款資料 =====
    [ObservableProperty]
    private string _paymentMethod = "CASH";

    [ObservableProperty]
    private decimal _amountReceived;

    [ObservableProperty]
    private string? _paymentNotes;

    // ===== 收據 =====
    [ObservableProperty]
    private Receipt? _receipt;

    public MainViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// 計算總金額
    /// </summary>
    public decimal TotalAmount => SelectedSlots.Sum(s => s.Price);

    /// <summary>
    /// 計算找零
    /// </summary>
    public decimal ChangeAmount => AmountReceived - TotalAmount;

    /// <summary>
    /// 初始化載入
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        await LoadLampTypesAsync();
    }

    #region Step 1: 燈位選擇

    /// <summary>
    /// 載入燈種清單
    /// </summary>
    private async Task LoadLampTypesAsync()
    {
        IsBusy = true;
        ClearMessages();

        var result = await _apiClient.GetLampTypesAsync();

        if (result.Success && result.Data != null)
        {
            LampTypes = new ObservableCollection<LampType>(result.Data);
        }
        else
        {
            ShowError(result.Message ?? "載入燈種失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 載入燈位清單
    /// </summary>
    [RelayCommand]
    public async Task LoadLampSlotsAsync()
    {
        IsBusy = true;
        ClearMessages();

        var result = await _apiClient.GetLampSlotsAsync(
            lampTypeId: SelectedLampType?.LampTypeId,
            availableOnly: true,
            year: DateTime.Now.Year
        );

        if (result.Success && result.Data != null)
        {
            LampSlots = new ObservableCollection<LampSlot>(result.Data);
        }
        else
        {
            ShowError(result.Message ?? "載入燈位失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 選取燈位
    /// </summary>
    [RelayCommand]
    public void SelectSlot(LampSlot slot)
    {
        if (SelectedSlots.Any(s => s.SlotId == slot.SlotId))
        {
            var existing = SelectedSlots.First(s => s.SlotId == slot.SlotId);
            SelectedSlots.Remove(existing);
        }
        else
        {
            SelectedSlots.Add(slot);
        }

        OnPropertyChanged(nameof(TotalAmount));
    }

    /// <summary>
    /// 移除已選燈位
    /// </summary>
    [RelayCommand]
    public void RemoveSlot(LampSlot slot)
    {
        SelectedSlots.Remove(slot);
        OnPropertyChanged(nameof(TotalAmount));
    }

    /// <summary>
    /// 清空選取
    /// </summary>
    [RelayCommand]
    public void ClearSelectedSlots()
    {
        SelectedSlots.Clear();
        OnPropertyChanged(nameof(TotalAmount));
    }

    /// <summary>
    /// 進入客戶選擇步驟
    /// </summary>
    [RelayCommand]
    public void GoToCustomerStep()
    {
        if (SelectedSlots.Count == 0)
        {
            ShowError("請先選擇燈位");
            return;
        }

        ClearMessages();
        CurrentStep = 2;
    }

    #endregion

    #region Step 2: 客戶選擇

    /// <summary>
    /// 搜尋客戶
    /// </summary>
    [RelayCommand]
    public async Task SearchCustomersAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchPhone))
        {
            ShowError("請輸入電話號碼");
            return;
        }

        IsBusy = true;
        ClearMessages();

        var result = await _apiClient.SearchCustomersAsync(SearchPhone);

        if (result.Success && result.Data != null)
        {
            SearchResults = new ObservableCollection<Customer>(result.Data);

            if (SearchResults.Count == 0)
            {
                ShowError("找不到客戶，請建立新客戶");
                IsCreatingNewCustomer = true;
                NewCustomerPhone = SearchPhone;
            }
            else
            {
                IsCreatingNewCustomer = false;
            }
        }
        else
        {
            ShowError(result.Message ?? "搜尋客戶失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 切換到建立新客戶
    /// </summary>
    [RelayCommand]
    public void ShowCreateCustomer()
    {
        IsCreatingNewCustomer = true;
        NewCustomerPhone = SearchPhone;
        SelectedCustomer = null;
    }

    /// <summary>
    /// 建立新客戶
    /// </summary>
    [RelayCommand]
    public async Task CreateCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName))
        {
            ShowError("請輸入客戶姓名");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewCustomerPhone))
        {
            ShowError("請輸入電話號碼");
            return;
        }

        IsBusy = true;
        ClearMessages();

        var request = new CreateCustomerRequest
        {
            Name = NewCustomerName,
            Phone = NewCustomerPhone,
            Address = NewCustomerAddress
        };

        var result = await _apiClient.CreateCustomerAsync(request);

        if (result.Success && result.Data != null)
        {
            SelectedCustomer = result.Data;
            ShowSuccess($"客戶 {result.Data.Name} 建立成功");
            IsCreatingNewCustomer = false;

            // 預設點燈者名稱為客戶姓名
            LightingName = result.Data.Name;
        }
        else
        {
            ShowError(result.Message ?? "建立客戶失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 選擇客戶
    /// </summary>
    [RelayCommand]
    public void SelectCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        IsCreatingNewCustomer = false;

        // 預設點燈者名稱為客戶姓名
        LightingName = customer.Name;
    }

    /// <summary>
    /// 進入訂單確認步驟
    /// </summary>
    [RelayCommand]
    public void GoToOrderStep()
    {
        if (SelectedCustomer == null)
        {
            ShowError("請先選擇或建立客戶");
            return;
        }

        ClearMessages();
        CurrentStep = 3;
    }

    /// <summary>
    /// 返回燈位選擇步驟
    /// </summary>
    [RelayCommand]
    public void BackToSlotStep()
    {
        ClearMessages();
        CurrentStep = 1;
    }

    #endregion

    #region Step 3: 建立訂單

    /// <summary>
    /// 建立訂單
    /// </summary>
    [RelayCommand]
    public async Task CreateOrderAsync()
    {
        if (string.IsNullOrWhiteSpace(LightingName))
        {
            ShowError("請輸入點燈者姓名");
            return;
        }

        IsBusy = true;
        ClearMessages();

        // 先鎖定所有燈位
        foreach (var slot in SelectedSlots)
        {
            var lockResult = await _apiClient.LockLampSlotAsync(slot.SlotId, new LockLampSlotRequest { LockDurationSeconds = 600 });

            if (!lockResult.Success)
            {
                ShowError($"燈位 {slot.SlotNumber} 鎖定失敗: {lockResult.Message}");
                IsBusy = false;
                return;
            }
        }

        // 建立訂單
        var request = new CreateOrderRequest
        {
            CustomerId = SelectedCustomer!.CustomerId,
            LampSlotIds = SelectedSlots.Select(s => s.SlotId).ToList(),
            LightingName = LightingName,
            BlessingContent = BlessingContent,
            Notes = OrderNotes
        };

        var result = await _apiClient.CreateOrderAsync(request);

        if (result.Success && result.Data != null)
        {
            CurrentOrder = result.Data;
            AmountReceived = CurrentOrder.TotalAmount; // 預設實收金額
            ShowSuccess($"訂單 {result.Data.OrderNumber} 建立成功");
            CurrentStep = 4;
        }
        else
        {
            ShowError(result.Message ?? "建立訂單失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 返回客戶選擇步驟
    /// </summary>
    [RelayCommand]
    public void BackToCustomerStep()
    {
        ClearMessages();
        CurrentStep = 2;
    }

    #endregion

    #region Step 4: 付款確認

    /// <summary>
    /// 確認付款
    /// </summary>
    [RelayCommand]
    public async Task ConfirmPaymentAsync()
    {
        if (CurrentOrder == null)
        {
            ShowError("訂單資料遺失");
            return;
        }

        if (AmountReceived < CurrentOrder.TotalAmount)
        {
            ShowError($"實收金額不足，應付 {CurrentOrder.TotalAmount:N0} 元");
            return;
        }

        IsBusy = true;
        ClearMessages();

        var request = new ConfirmOrderRequest
        {
            PaymentMethod = PaymentMethod,
            AmountReceived = AmountReceived,
            PaymentNotes = PaymentNotes
        };

        var result = await _apiClient.ConfirmOrderAsync(CurrentOrder.OrderId, request);

        if (result.Success && result.Data != null)
        {
            CurrentOrder = result.Data;
            ShowSuccess("付款成功");

            // 取得收據
            await LoadReceiptAsync();
            CurrentStep = 5;
        }
        else
        {
            ShowError(result.Message ?? "付款失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 取消訂單
    /// </summary>
    [RelayCommand]
    public async Task CancelOrderAsync()
    {
        if (CurrentOrder == null) return;

        IsBusy = true;
        ClearMessages();

        var result = await _apiClient.CancelOrderAsync(CurrentOrder.OrderId, "客戶取消");

        if (result.Success)
        {
            ShowSuccess("訂單已取消");
            StartNewTransaction();
        }
        else
        {
            ShowError(result.Message ?? "取消訂單失敗");
        }

        IsBusy = false;
    }

    partial void OnAmountReceivedChanged(decimal value)
    {
        OnPropertyChanged(nameof(ChangeAmount));
    }

    #endregion

    #region Step 5: 收據

    /// <summary>
    /// 載入收據
    /// </summary>
    private async Task LoadReceiptAsync()
    {
        if (CurrentOrder == null) return;

        var result = await _apiClient.GetReceiptAsync(CurrentOrder.OrderId);

        if (result.Success && result.Data != null)
        {
            Receipt = result.Data;
        }
    }

    /// <summary>
    /// 列印收據
    /// </summary>
    [RelayCommand]
    public async Task PrintReceiptAsync()
    {
        if (CurrentOrder == null) return;

        IsBusy = true;
        ClearMessages();

        var request = new PrintReceiptRequest { Copies = 1 };
        var result = await _apiClient.PrintReceiptAsync(CurrentOrder.OrderId, request);

        if (result.Success)
        {
            ShowSuccess("列印完成");
        }
        else
        {
            ShowError(result.Message ?? "列印失敗");
        }

        IsBusy = false;
    }

    /// <summary>
    /// 開始新交易
    /// </summary>
    [RelayCommand]
    public void StartNewTransaction()
    {
        // 重設所有狀態
        CurrentStep = 1;
        SelectedSlots.Clear();
        SelectedCustomer = null;
        SearchPhone = string.Empty;
        SearchResults.Clear();
        NewCustomerName = string.Empty;
        NewCustomerPhone = string.Empty;
        NewCustomerAddress = null;
        IsCreatingNewCustomer = false;
        LightingName = string.Empty;
        BlessingContent = null;
        OrderNotes = null;
        CurrentOrder = null;
        PaymentMethod = "CASH";
        AmountReceived = 0;
        PaymentNotes = null;
        Receipt = null;

        ClearMessages();
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(ChangeAmount));

        // 重新載入燈位
        _ = LoadLampSlotsAsync();
    }

    #endregion
}
