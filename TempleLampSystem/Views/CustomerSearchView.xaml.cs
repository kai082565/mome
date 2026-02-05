using System.Windows.Controls;
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

    public void DeselectCustomer(Guid customerId)
    {
        var itemToDeselect = CustomerListView.SelectedItems
            .Cast<Models.CustomerDisplayModel>()
            .FirstOrDefault(c => c.Id == customerId);

        if (itemToDeselect != null)
        {
            CustomerListView.SelectedItems.Remove(itemToDeselect);
        }
    }
}
