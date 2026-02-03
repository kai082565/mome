using System.Windows;
using System.Windows.Input;
using TempleLamp.Desktop.Models;
using TempleLamp.Desktop.ViewModels;

namespace TempleLamp.Desktop;

/// <summary>
/// MainWindow.xaml 的互動邏輯
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }

    private void SlotCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is LampSlot slot)
        {
            if (DataContext is MainViewModel viewModel && slot.IsAvailable)
            {
                viewModel.SelectSlotCommand.Execute(slot);
            }
        }
    }
}
