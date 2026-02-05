using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;
using TempleLampSystem.Views;

namespace TempleLampSystem.ViewModels;

public partial class CustomerSearchViewModel : ViewModelBase
{
    private readonly ICustomerRepository _customerRepository;
    private bool _isRefreshing = false;

    public CustomerSearchViewModel(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
        Customers = new ObservableCollection<CustomerDisplayModel>();
    }

    [ObservableProperty]
    private string _searchPhone = string.Empty;

    [ObservableProperty]
    private CustomerDisplayModel? _selectedCustomer;

    public ObservableCollection<CustomerDisplayModel> Customers { get; }

    // 多選客戶列表
    public ObservableCollection<CustomerDisplayModel> SelectedCustomers { get; } = new();

    public event EventHandler<CustomerDisplayModel?>? CustomerSelected;
    public event EventHandler<IList<CustomerDisplayModel>>? CustomersSelectionChanged;

    [RelayCommand]
    private async Task SearchAsync()
    {

        IsBusy = true;
        StatusMessage = "搜尋中...";

        try
        {
            var customers = await _customerRepository.SearchByPhoneWithOrdersAsync(SearchPhone);

            Customers.Clear();
            foreach (var customer in customers)
            {
                var displayModel = new CustomerDisplayModel
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Phone = customer.Phone,
                    Mobile = customer.Mobile,
                    Address = customer.Address,
                    Orders = customer.LampOrders
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
                        .ToList()
                };
                Customers.Add(displayModel);
            }

            StatusMessage = $"找到 {Customers.Count} 位客戶";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜尋失敗：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        var window = new AddCustomerWindow
        {
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true && window.NewCustomer != null)
        {
            try
            {
                await _customerRepository.AddAsync(window.NewCustomer);
                StatusMessage = $"已新增客戶：{window.NewCustomer.Name}";
                await SearchAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"新增客戶失敗：{ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void SelectCustomer(CustomerDisplayModel? customer)
    {
        SelectedCustomer = customer;
        CustomerSelected?.Invoke(this, customer);
    }

    partial void OnSelectedCustomerChanged(CustomerDisplayModel? value)
    {
        // 刷新客戶資料時不觸發事件，避免清除右側的選擇狀態
        if (!_isRefreshing)
        {
            CustomerSelected?.Invoke(this, value);
        }
    }

    public void UpdateSelectedCustomers(System.Collections.IList selectedItems)
    {
        SelectedCustomers.Clear();
        foreach (var item in selectedItems)
        {
            if (item is CustomerDisplayModel customer)
            {
                SelectedCustomers.Add(customer);
            }
        }
        CustomersSelectionChanged?.Invoke(this, SelectedCustomers.ToList());
    }

    public async Task RefreshCustomerOrdersAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetWithOrdersAsync(customerId);
        if (customer == null) return;

        var existing = Customers.FirstOrDefault(c => c.Id == customerId);
        if (existing != null)
        {
            existing.Orders = customer.LampOrders
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

            // 刷新時暫時禁用事件，避免清除右側的客戶選擇
            _isRefreshing = true;
            try
            {
                var index = Customers.IndexOf(existing);
                Customers.RemoveAt(index);
                Customers.Insert(index, existing);

                if (SelectedCustomer?.Id == customerId)
                {
                    SelectedCustomer = existing;
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }
    }
}
