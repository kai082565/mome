using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.ViewModels;

public partial class CustomerSearchViewModel : ViewModelBase
{
    private readonly ICustomerRepository _customerRepository;

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

    public event EventHandler<CustomerDisplayModel?>? CustomerSelected;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchPhone))
        {
            StatusMessage = "請輸入電話號碼";
            return;
        }

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
    private void SelectCustomer(CustomerDisplayModel? customer)
    {
        SelectedCustomer = customer;
        CustomerSelected?.Invoke(this, customer);
    }

    partial void OnSelectedCustomerChanged(CustomerDisplayModel? value)
    {
        CustomerSelected?.Invoke(this, value);
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

            var index = Customers.IndexOf(existing);
            Customers.RemoveAt(index);
            Customers.Insert(index, existing);

            if (SelectedCustomer?.Id == customerId)
            {
                SelectedCustomer = existing;
            }
        }
    }
}
