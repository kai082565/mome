using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;
using TempleLampSystem.ViewModels;

namespace TempleLampSystem;

public partial class MainWindow : Window
{
    private readonly CustomerSearchViewModel _customerSearchViewModel;
    private readonly LampOrderViewModel _lampOrderViewModel;
    private readonly IAutoSyncService _autoSyncService;
    private readonly DispatcherTimer _clockTimer;

    public MainWindow()
    {
        InitializeComponent();

        _customerSearchViewModel = App.Services.GetRequiredService<CustomerSearchViewModel>();
        _lampOrderViewModel = App.Services.GetRequiredService<LampOrderViewModel>();
        _autoSyncService = App.Services.GetRequiredService<IAutoSyncService>();

        CustomerSearchView.DataContext = _customerSearchViewModel;
        LampOrderView.DataContext = _lampOrderViewModel;

        _customerSearchViewModel.CustomerSelected += OnCustomerSelected;
        _customerSearchViewModel.CustomersSelectionChanged += OnCustomersSelectionChanged;
        _lampOrderViewModel.OrderCreated += OnOrderCreated;

        // 訂閱同步狀態
        _autoSyncService.SyncStatusChanged += OnSyncStatusChanged;

        // 時鐘更新
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => CurrentTimeText.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        _clockTimer.Start();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _lampOrderViewModel.InitializeAsync();
        _autoSyncService.Start();
        CurrentTimeText.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoSyncService.Stop();
        _clockTimer.Stop();
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            OnlineIndicatorBorder.Background = e.IsOnline
                ? new SolidColorBrush(Colors.LimeGreen)
                : new SolidColorBrush(Colors.Orange);

            SyncStatusText.Text = e.Message ?? (e.IsOnline ? "☁️ 已連線" : "📴 離線模式");

            PendingSyncText.Text = e.PendingCount > 0
                ? $"📤 待同步：{e.PendingCount} 筆"
                : "";
        });
    }

    private async void OnCustomerSelected(object? sender, CustomerDisplayModel? customer)
    {
        // 單選時的處理（保持向後相容）
        if (_customerSearchViewModel.SelectedCustomers.Count <= 1)
        {
            await _lampOrderViewModel.SetCustomerAsync(customer);
        }
    }

    private void OnCustomersSelectionChanged(object? sender, IList<CustomerDisplayModel> customers)
    {
        // 多選時的處理
        _lampOrderViewModel.SetSelectedCustomers(customers);
    }

    private async void OnOrderCreated(object? sender, Guid customerId)
    {
        await _customerSearchViewModel.RefreshCustomerOrdersAsync(customerId);
    }
}
