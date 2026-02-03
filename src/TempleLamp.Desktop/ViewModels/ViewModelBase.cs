using CommunityToolkit.Mvvm.ComponentModel;

namespace TempleLamp.Desktop.ViewModels;

/// <summary>
/// ViewModel 基底類別
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    /// <summary>
    /// 清除訊息
    /// </summary>
    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// 顯示錯誤訊息
    /// </summary>
    protected void ShowError(string message)
    {
        ErrorMessage = message;
        SuccessMessage = null;
    }

    /// <summary>
    /// 顯示成功訊息
    /// </summary>
    protected void ShowSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage = null;
    }
}
