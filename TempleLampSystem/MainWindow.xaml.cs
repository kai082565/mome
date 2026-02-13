using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
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
        _lampOrderViewModel.CustomerRemoved += OnCustomerRemoved;

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
        try
        {
            await _lampOrderViewModel.InitializeAsync();
            _autoSyncService.Start();
            CurrentTimeText.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

            SyncStatusText.Text = e.Message ?? (e.IsOnline ? "已連線" : "離線模式");

            PendingSyncText.Text = e.PendingCount > 0
                ? $"待同步：{e.PendingCount} 筆"
                : "";
        });
    }

    private async void OnCustomerSelected(object? sender, CustomerDisplayModel? customer)
    {
        try
        {
            // 單選時的處理（保持向後相容）
            if (_customerSearchViewModel.SelectedCustomers.Count <= 1)
            {
                await _lampOrderViewModel.SetCustomerAsync(customer);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"選擇客戶時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCustomersSelectionChanged(object? sender, IList<CustomerDisplayModel> customers)
    {
        // 多選時的處理
        _lampOrderViewModel.SetSelectedCustomers(customers);
    }

    private async void OnOrderCreated(object? sender, Guid customerId)
    {
        try
        {
            await _customerSearchViewModel.RefreshCustomerOrdersAsync(customerId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新客戶資料時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCustomerRemoved(object? sender, CustomerDisplayModel customer)
    {
        CustomerSearchView.DeselectCustomer(customer.Id);
    }

    private async void ImportDataButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "匯入舊系統資料將會清除現有的所有客戶和點燈資料！\n\n確定要繼續嗎？",
            "匯入舊系統資料",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        // 選擇客戶 CSV
        var customerDialog = new OpenFileDialog
        {
            Title = "選擇客戶 CSV 檔案 (customers.csv)",
            Filter = "CSV 檔案 (*.csv)|*.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (customerDialog.ShowDialog() != true) return;

        // 選擇點燈 CSV
        var orderDialog = new OpenFileDialog
        {
            Title = "選擇點燈 CSV 檔案 (lamporders.csv)",
            Filter = "CSV 檔案 (*.csv)|*.csv",
            InitialDirectory = System.IO.Path.GetDirectoryName(customerDialog.FileName)
        };
        if (orderDialog.ShowDialog() != true) return;

        ImportDataButton.IsEnabled = false;
        ImportDataButton.Content = "匯入中...";

        try
        {
            using var scope = App.Services.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<DataImportService>();

            var progress = new Progress<DataImportService.ImportProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ImportDataButton.Content = p.CurrentStep;
                });
            });

            var result = await Task.Run(async () =>
                await importService.ImportAsync(customerDialog.FileName, orderDialog.FileName, progress));

            var msg = $"匯入完成！\n\n" +
                      $"客戶：{result.ImportedCustomers}/{result.TotalCustomers}\n" +
                      $"點燈：{result.ImportedOrders}/{result.TotalOrders}\n" +
                      $"略過：{result.SkippedOrders}";

            if (result.Errors.Count > 0)
            {
                msg += $"\n\n錯誤 ({result.Errors.Count} 筆)：\n" +
                       string.Join("\n", result.Errors.Take(10));
            }

            MessageBox.Show(msg, "匯入結果", MessageBoxButton.OK, MessageBoxImage.Information);

            // 重新載入資料
            await _lampOrderViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"匯入失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ImportDataButton.IsEnabled = true;
            ImportDataButton.Content = "匯入舊資料";
        }
    }
}
