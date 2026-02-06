using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services.Repositories;

namespace TempleLampSystem.Views;

public partial class AddCustomerWindow : Window
{
    private static readonly string[] ZodiacAnimals = ["鼠", "牛", "虎", "兔", "龍", "蛇", "馬", "羊", "猴", "雞", "狗", "豬"];
    private static readonly string[] BirthHours = ["吉時", "子時", "丑時", "寅時", "卯時", "辰時", "巳時", "午時", "未時", "申時", "酉時", "戌時", "亥時"];

    private readonly ICustomerRepository _customerRepository;
    private List<Customer> _foundFamilyMembers = new();

    public Customer? NewCustomer { get; private set; }

    public AddCustomerWindow()
    {
        InitializeComponent();
        BirthHourComboBox.ItemsSource = BirthHours;
        _customerRepository = App.Services.GetRequiredService<ICustomerRepository>();
    }

    private void BirthYearTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(BirthYearTextBox.Text.Trim(), out var rocYear) && rocYear > 0)
        {
            var westernYear = rocYear + 1911;
            var index = ((westernYear - 4) % 12 + 12) % 12;
            ZodiacText.Text = ZodiacAnimals[index];
        }
        else
        {
            ZodiacText.Text = string.Empty;
        }
    }

    private async void PhoneTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await CheckFamilyMembersAsync();
    }

    private async Task CheckFamilyMembersAsync()
    {
        var phone = NullIfEmpty(PhoneTextBox.Text);

        if (phone == null)
        {
            FamilyHintBorder.Visibility = Visibility.Collapsed;
            _foundFamilyMembers.Clear();
            return;
        }

        try
        {
            _foundFamilyMembers = await _customerRepository.FindByPhoneOrMobileAsync(phone, phone);

            if (_foundFamilyMembers.Count > 0)
            {
                var names = string.Join("、", _foundFamilyMembers.Select(c => c.Name));
                FamilyHintText.Text = $"此號碼已有 {_foundFamilyMembers.Count} 位客戶使用：{names}\n新增後將自動關聯為同戶家人";
                FamilyHintBorder.Visibility = Visibility.Visible;
            }
            else
            {
                FamilyHintBorder.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            FamilyHintBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void BirthMonthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClampNumericTextBox(BirthMonthTextBox, 1, 12);
    }

    private void BirthDayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClampNumericTextBox(BirthDayTextBox, 1, 31);
    }

    private static void ClampNumericTextBox(TextBox textBox, int min, int max)
    {
        var text = textBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // 只保留數字
        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());
        if (digitsOnly != text)
        {
            textBox.Text = digitsOnly;
            textBox.CaretIndex = digitsOnly.Length;
            return;
        }

        if (int.TryParse(digitsOnly, out var value) && value > max)
        {
            textBox.Text = max.ToString();
            textBox.CaretIndex = textBox.Text.Length;
        }
    }

    private void JiYearCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = JiYearCheckBox.IsChecked == true;
        BirthYearTextBox.IsEnabled = !isChecked;
        if (isChecked)
        {
            BirthYearTextBox.Text = string.Empty;
            ZodiacText.Text = string.Empty;
        }
    }

    private void JiMonthCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = JiMonthCheckBox.IsChecked == true;
        BirthMonthTextBox.IsEnabled = !isChecked;
        if (isChecked)
            BirthMonthTextBox.Text = string.Empty;
    }

    private void JiDayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = JiDayCheckBox.IsChecked == true;
        BirthDayTextBox.IsEnabled = !isChecked;
        if (isChecked)
            BirthDayTextBox.Text = string.Empty;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("請輸入姓名！", "欄位驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        NewCustomer = new Customer
        {
            Name = name,
            Phone = NullIfEmpty(PhoneTextBox.Text),
            Mobile = NullIfEmpty(PhoneTextBox.Text),  // 電話和手機存同一個值
            Address = NullIfEmpty(AddressTextBox.Text),
            Village = NullIfEmpty(VillageTextBox.Text),
            PostalCode = NullIfEmpty(PostalCodeTextBox.Text),
            Note = NullIfEmpty(NoteTextBox.Text),
            BirthHour = BirthHourComboBox.SelectedItem as string
        };

        // 吉年/吉月/吉日 以 0 儲存，正常數值則存實際值
        if (JiYearCheckBox.IsChecked == true)
            NewCustomer.BirthYear = 0;
        else if (int.TryParse(BirthYearTextBox.Text.Trim(), out var birthYear))
            NewCustomer.BirthYear = birthYear;

        if (JiMonthCheckBox.IsChecked == true)
            NewCustomer.BirthMonth = 0;
        else if (int.TryParse(BirthMonthTextBox.Text.Trim(), out var birthMonth))
            NewCustomer.BirthMonth = birthMonth;

        if (JiDayCheckBox.IsChecked == true)
            NewCustomer.BirthDay = 0;
        else if (int.TryParse(BirthDayTextBox.Text.Trim(), out var birthDay))
            NewCustomer.BirthDay = birthDay;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string? NullIfEmpty(string text)
    {
        var trimmed = text.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
