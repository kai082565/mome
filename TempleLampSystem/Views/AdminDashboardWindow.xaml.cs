using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TempleLampSystem.Models;
using TempleLampSystem.Services;

namespace TempleLampSystem.Views;

public partial class AdminDashboardWindow : Window
{
    private readonly IStaffService _staffService;
    private readonly AppDbContext _dbContext;
    private List<DashboardOrderRow> _allRows = new();
    private List<StaffListItem> _staffItems = new();
    private StaffListItem? _selectedStaffItem;

    public AdminDashboardWindow()
    {
        InitializeComponent();
        _staffService = App.Services.GetRequiredService<IStaffService>();
        _dbContext = App.Services.GetRequiredService<AppDbContext>();

        // 預設今天
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await LoadOrdersAsync();
        await LoadStaffListAsync();
    }

    #region 點燈統計

    private async Task LoadOrdersAsync()
    {
        var start = StartDatePicker.SelectedDate ?? DateTime.Today;
        var end = (EndDatePicker.SelectedDate ?? DateTime.Today).AddDays(1).AddTicks(-1);

        _allRows = await _dbContext.LampOrders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new DashboardOrderRow
            {
                CreatedAt = o.CreatedAt,
                StaffName = o.StaffName ?? "（未記錄）",
                CustomerName = o.Customer.Name,
                LampName = o.Lamp.LampName,
                Temple = o.Lamp.Temple ?? "",
                Deity = o.Lamp.Deity ?? "",
                Year = o.Year,
                Price = o.Price,
                StartDate = o.StartDate,
                EndDate = o.EndDate
            })
            .ToListAsync();

        // 填入篩選 ComboBox
        var allItem = new[] { "（全部）" };

        var staffNames = allItem.Concat(_allRows.Select(r => r.StaffName).Distinct().OrderBy(x => x)).ToList();
        StaffFilterCombo.ItemsSource = staffNames;
        StaffFilterCombo.SelectedIndex = 0;

        var temples = allItem.Concat(_allRows.Select(r => r.Temple).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(x => x)).ToList();
        TempleFilterCombo.ItemsSource = temples;
        TempleFilterCombo.SelectedIndex = 0;

        var lampNames = allItem.Concat(_allRows.Select(r => r.LampName).Distinct().OrderBy(x => x)).ToList();
        LampFilterCombo.ItemsSource = lampNames;
        LampFilterCombo.SelectedIndex = 0;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filteredStaff = StaffFilterCombo.SelectedItem as string;
        var filteredTemple = TempleFilterCombo.SelectedItem as string;
        var filteredLamp = LampFilterCombo.SelectedItem as string;

        var rows = _allRows.AsEnumerable();

        if (!string.IsNullOrEmpty(filteredStaff) && filteredStaff != "（全部）")
            rows = rows.Where(r => r.StaffName == filteredStaff);
        if (!string.IsNullOrEmpty(filteredTemple) && filteredTemple != "（全部）")
            rows = rows.Where(r => r.Temple == filteredTemple);
        if (!string.IsNullOrEmpty(filteredLamp) && filteredLamp != "（全部）")
            rows = rows.Where(r => r.LampName == filteredLamp);

        var list = rows.ToList();
        OrdersDataGrid.ItemsSource = list;

        // 更新統計
        TotalOrdersText.Text = list.Count.ToString("N0");
        TotalAmountText.Text = $"${list.Sum(r => r.Price):N0}";

        var staffSummary = list
            .GroupBy(r => r.StaffName)
            .Select(g => new StaffSummaryItem { StaffName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        StaffSummaryControl.ItemsSource = staffSummary;
    }

    private async void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadOrdersAsync();
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;
        _ = LoadOrdersAsync();
    }

    private void ThisMonthButton_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        EndDatePicker.SelectedDate = today;
        _ = LoadOrdersAsync();
    }

    private void DateFilter_Changed(object? sender, SelectionChangedEventArgs e) { }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    #endregion

    #region 帳號管理

    private async Task LoadStaffListAsync()
    {
        var staffList = await _staffService.GetAllAsync();
        _staffItems = staffList.Select(s => new StaffListItem
        {
            Id = s.Id,
            Name = s.Name,
            IsAdmin = s.Role == StaffRole.Admin,
            IsActive = s.IsActive,
            IsInactive = !s.IsActive
        }).ToList();

        StaffListBox.ItemsSource = _staffItems;
        SelectedStaffPanel.Visibility = Visibility.Collapsed;
    }

    private void StaffListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedStaffItem = StaffListBox.SelectedItem as StaffListItem;
        if (_selectedStaffItem == null)
        {
            SelectedStaffPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedStaffPanel.Visibility = Visibility.Visible;
        SelectedStaffTitle.Text = $"編輯：{_selectedStaffItem.Name}";
        ResetPasswordBox.Password = string.Empty;
        ToggleActiveButton.Content = _selectedStaffItem.IsActive ? "停用帳號" : "啟用帳號";
    }

    private async void AddStaffButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewNameTextBox.Text.Trim();
        var password = NewPasswordBox.Password;
        var isAdmin = NewRoleCombo.SelectedIndex == 1;

        AddStaffErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(name))
        {
            AddStaffErrorText.Text = "請輸入姓名";
            AddStaffErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            AddStaffErrorText.Text = "請輸入密碼";
            AddStaffErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var role = isAdmin ? StaffRole.Admin : StaffRole.Staff;
            await _staffService.CreateStaffAsync(name, password, role);

            // 同步到雲端
            using var scope = App.Services.CreateScope();
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
            var staffContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newStaff = await staffContext.Staff.FirstOrDefaultAsync(s => s.Name == name);
            if (newStaff != null)
                await supabaseService.UpsertStaffAsync(newStaff);

            NewNameTextBox.Clear();
            NewPasswordBox.Password = string.Empty;
            NewRoleCombo.SelectedIndex = 0;
            await LoadStaffListAsync();
        }
        catch (Exception ex)
        {
            AddStaffErrorText.Text = ex.Message.Contains("UNIQUE") ? "此姓名已存在" : ex.Message;
            AddStaffErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStaffItem == null) return;
        var newPwd = ResetPasswordBox.Password;
        if (string.IsNullOrEmpty(newPwd))
        {
            StyledMessageBox.Show("請輸入新密碼", "提示");
            return;
        }

        try
        {
            await _staffService.UpdatePasswordAsync(_selectedStaffItem.Id, newPwd);

            // 同步到雲端
            using var scope = App.Services.CreateScope();
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
            var staffContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staff = await staffContext.Staff.FindAsync(_selectedStaffItem.Id);
            if (staff != null)
                await supabaseService.UpsertStaffAsync(staff);

            ResetPasswordBox.Password = string.Empty;
            StyledMessageBox.Show("密碼已重設", "完成");
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"重設失敗：{ex.Message}", "錯誤");
        }
    }

    private async void ToggleActiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStaffItem == null) return;

        try
        {
            var newActive = !_selectedStaffItem.IsActive;
            await _staffService.SetActiveAsync(_selectedStaffItem.Id, newActive);

            // 同步到雲端
            using var scope = App.Services.CreateScope();
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
            var staffContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staff = await staffContext.Staff.FindAsync(_selectedStaffItem.Id);
            if (staff != null)
                await supabaseService.UpsertStaffAsync(staff);

            await LoadStaffListAsync();
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"操作失敗：{ex.Message}", "錯誤");
        }
    }

    #endregion
}

public class DashboardOrderRow
{
    public DateTime CreatedAt { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string LampName { get; set; } = string.Empty;
    public string Temple { get; set; } = string.Empty;
    public string Deity { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class StaffSummaryItem
{
    public string StaffName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StaffListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public bool IsInactive { get; set; }
}
