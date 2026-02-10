using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;
using TempleLampSystem.Services.Repositories;
using TempleLampSystem.ViewModels;

namespace TempleLampSystem.Views;

public partial class CustomerSearchView : UserControl
{
    public CustomerSearchView()
    {
        InitializeComponent();
    }

    private void CustomerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CustomerSearchViewModel viewModel)
        {
            viewModel.UpdateSelectedCustomers(CustomerListView.SelectedItems);
        }
    }

    private async void CustomerName_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CustomerDisplayModel displayModel)
        {
            e.Handled = true;

            try
            {
                // 先從雲端同步最新資料
                try
                {
                    var supabaseService = App.Services.GetRequiredService<ISupabaseService>();
                    if (supabaseService.IsConfigured)
                    {
                        await supabaseService.SyncFromCloudAsync();
                    }
                }
                catch
                {
                    // 同步失敗時使用本地資料
                }

                var repository = App.Services.GetRequiredService<ICustomerRepository>();
                var customer = await repository.GetWithOrdersAsync(displayModel.Id);
                if (customer == null) return;

                var window = new CustomerDetailWindow(customer)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();

                // 若客戶已被刪除，直接從列表移除（不要觸發 SearchAsync，避免 SyncFromCloud 又把雲端資料同步回來）
                if (window.CustomerWasDeleted && DataContext is CustomerSearchViewModel vm)
                {
                    var toRemove = vm.Customers.FirstOrDefault(c => c.Id == displayModel.Id);
                    if (toRemove != null)
                    {
                        vm.Customers.Remove(toRemove);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入客戶資料失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void DeselectCustomer(Guid customerId)
    {
        var itemToDeselect = CustomerListView.SelectedItems
            .Cast<CustomerDisplayModel>()
            .FirstOrDefault(c => c.Id == customerId);

        if (itemToDeselect != null)
        {
            CustomerListView.SelectedItems.Remove(itemToDeselect);
        }
    }
}
