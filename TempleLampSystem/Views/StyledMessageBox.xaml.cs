using System.Windows;
using System.Windows.Controls;

namespace TempleLampSystem.Views;

public partial class StyledMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    private StyledMessageBox(string message, string title, MessageBoxButton buttons)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        CreateButtons(buttons);
    }

    private void CreateButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("確定", MessageBoxResult.OK, isDefault: true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("是", MessageBoxResult.Yes, isDefault: true);
                AddButton("否", MessageBoxResult.No);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("是", MessageBoxResult.Yes, isDefault: true);
                AddButton("否", MessageBoxResult.No);
                AddButton("取消", MessageBoxResult.Cancel);
                break;
            case MessageBoxButton.OKCancel:
                AddButton("確定", MessageBoxResult.OK, isDefault: true);
                AddButton("取消", MessageBoxResult.Cancel);
                break;
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 20,
            Padding = new Thickness(32, 12, 32, 12),
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
        };

        if (result == MessageBoxResult.Yes || result == MessageBoxResult.OK)
        {
            button.Style = (Style)FindResource("SuccessButton");
        }

        button.Click += (_, _) =>
        {
            Result = result;
            DialogResult = result == MessageBoxResult.Yes || result == MessageBoxResult.OK;
            Close();
        };

        ButtonPanel.Children.Add(button);
    }

    public static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var owner = Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
            ?? Application.Current.MainWindow;

        var box = new StyledMessageBox(message, title, buttons);
        if (owner != null && owner.IsLoaded)
            box.Owner = owner;

        box.ShowDialog();
        return box.Result;
    }
}
