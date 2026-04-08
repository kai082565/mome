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
    private List<DashboardOrderRow> _allRows = new();
    private List<ExpiringOrderRow> _expiringRows = new();
    private List<StaffListItem> _staffItems = new();
    private StaffListItem? _selectedStaffItem;
    private readonly SessionService _sessionService;

    public AdminDashboardWindow()
    {
        InitializeComponent();

        _sessionService = App.Services.GetRequiredService<SessionService>();

        // 預設今天
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await LoadOrdersAsync();
        await LoadExpiringOrdersAsync();
        await LoadStaffListAsync();
    }

    #region 點燈統計

    private async Task LoadOrdersAsync()
    {
        var start = StartDatePicker.SelectedDate ?? DateTime.Today;
        var end = (EndDatePicker.SelectedDate ?? DateTime.Today).AddDays(1).AddTicks(-1);

        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _allRows = await dbContext.LampOrders
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

        // 填入工作人員 ComboBox（從 Staff 表取得，只顯示啟用中的人員）
        var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var staffList = await staffService.GetAllAsync();
        var staffNames = new[] { "（全部）" }
            .Concat(staffList.Where(s => s.IsActive).Select(s => s.Name).OrderBy(x => x))
            .ToList();
        StaffFilterCombo.ItemsSource = staffNames;
        StaffFilterCombo.SelectedIndex = 0;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filteredStaff = StaffFilterCombo.SelectedItem as string;
        var rows = _allRows.AsEnumerable();

        if (!string.IsNullOrEmpty(filteredStaff) && filteredStaff != "（全部）")
            rows = rows.Where(r => r.StaffName == filteredStaff);

        var list = rows.ToList();
        OrdersDataGrid.ItemsSource = list;

        TotalOrdersText.Text = list.Count.ToString("N0");
        TotalAmountText.Text = $"${list.Sum(r => r.Price):N0}";

        var staffSummary = list
            .GroupBy(r => r.StaffName)
            .Select(g => new StaffSummaryItem { StaffName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        StaffSummaryControl.ItemsSource = staffSummary;
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

    private void DateFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        _ = LoadOrdersAsync();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    #endregion

    #region 即將到期訂單

    private async Task LoadExpiringOrdersAsync()
    {
        var today = DateTime.Today;
        var threshold = today.AddDays(30);

        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _expiringRows = await dbContext.LampOrders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Lamp)
            .Where(o => o.EndDate >= today && o.EndDate <= threshold)
            .OrderBy(o => o.EndDate)
            .Select(o => new ExpiringOrderRow
            {
                CustomerName = o.Customer.Name,
                Phone = o.Customer.Phone ?? "",
                LampName = o.Lamp.LampName,
                EndDate = o.EndDate,
                DaysLeft = (int)(o.EndDate - today).TotalDays
            })
            .ToListAsync();

        ExpiringOrdersDataGrid.ItemsSource = _expiringRows;
        ExpiringCountNumber.Text = _expiringRows.Count.ToString("N0");
        ExpiringCountText.Text = _expiringRows.Count > 0
            ? "筆訂單即將到期（每次開啟自動更新）"
            : "目前無即將到期的點燈訂單";
    }

    #endregion

    #region 帳號管理

    private async Task LoadStaffListAsync()
    {
        using var scope = App.Services.CreateScope();
        var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var staffList = await staffService.GetAllAsync();
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

        if (password.Length < 4)
        {
            AddStaffErrorText.Text = "密碼至少需要 4 個字元";
            AddStaffErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var role = isAdmin ? StaffRole.Admin : StaffRole.Staff;

            Staff newStaff;
            using (var scope = App.Services.CreateScope())
            {
                var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
                newStaff = await staffService.CreateStaffAsync(name, password, role);

                // 同步到雲端（同一 scope，避免 context 衝突）
                var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
                try { await supabaseService.UpsertStaffAsync(newStaff); } catch { /* 背景同步會重試 */ }
            }

            NewNameTextBox.Clear();
            NewPasswordBox.Password = string.Empty;
            NewRoleCombo.SelectedIndex = 0;
            await LoadStaffListAsync();
        }
        catch (Exception ex)
        {
            var msg = (ex.InnerException ?? ex).Message;
            AddStaffErrorText.Text = msg.Contains("UNIQUE") ? "此姓名已存在" : msg;
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

        if (newPwd.Length < 4)
        {
            StyledMessageBox.Show("密碼至少需要 4 個字元", "提示");
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
            await staffService.UpdatePasswordAsync(_selectedStaffItem.Id, newPwd);

            var staffContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staff = await staffContext.Staff.FindAsync(_selectedStaffItem.Id);
            if (staff != null)
            {
                var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
                try { await supabaseService.UpsertStaffAsync(staff); } catch { }
            }

            ResetPasswordBox.Password = string.Empty;
            StyledMessageBox.Show("密碼已重設", "完成");
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"重設失敗：{(ex.InnerException ?? ex).Message}", "錯誤");
        }
    }

    private async void DeleteStaffButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStaffItem == null) return;

        // 防止刪除自己的帳號
        if (_selectedStaffItem.Id == _sessionService.CurrentStaff?.Id)
        {
            StyledMessageBox.Show("無法刪除目前登入中的帳號。", "操作拒絕");
            return;
        }

        var confirm = MessageBox.Show(
            $"確定要刪除「{_selectedStaffItem.Name}」的帳號嗎？\n此操作無法復原。",
            "確認刪除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
            await staffService.DeleteAsync(_selectedStaffItem.Id);

            // 同步刪除到雲端
            var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
            await supabaseService.DeleteStaffAsync(_selectedStaffItem.Id);

            await LoadStaffListAsync();
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"刪除失敗：{(ex.InnerException ?? ex).Message}", "錯誤");
        }
    }

    private async void ToggleActiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStaffItem == null) return;

        try
        {
            var newActive = !_selectedStaffItem.IsActive;

            using var scope = App.Services.CreateScope();
            var staffService = scope.ServiceProvider.GetRequiredService<IStaffService>();
            await staffService.SetActiveAsync(_selectedStaffItem.Id, newActive);

            var staffContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staff = await staffContext.Staff.FindAsync(_selectedStaffItem.Id);
            if (staff != null)
            {
                var supabaseService = scope.ServiceProvider.GetRequiredService<ISupabaseService>();
                try { await supabaseService.UpsertStaffAsync(staff); } catch { }
            }

            await LoadStaffListAsync();
        }
        catch (Exception ex)
        {
            StyledMessageBox.Show($"操作失敗：{(ex.InnerException ?? ex).Message}", "錯誤");
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

public class ExpiringOrderRow
{
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LampName { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public int DaysLeft { get; set; }
}

public class StaffListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public bool IsInactive { get; set; }
}
