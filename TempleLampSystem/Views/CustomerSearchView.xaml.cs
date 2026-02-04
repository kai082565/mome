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
}
