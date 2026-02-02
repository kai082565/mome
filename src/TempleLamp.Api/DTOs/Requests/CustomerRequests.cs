using System.ComponentModel.DataAnnotations;

namespace TempleLamp.Api.DTOs.Requests;

/// <summary>
/// 客戶搜尋請求
/// </summary>
public class CustomerSearchRequest
{
    /// <summary>
    /// 電話號碼（模糊搜尋）
    /// </summary>
    [Required(ErrorMessage = "電話號碼為必填")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "電話號碼長度需介於 3-20 字元")]
    public string Phone { get; set; } = string.Empty;
}

/// <summary>
/// 建立客戶請求
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// 客戶姓名
    /// </summary>
    [Required(ErrorMessage = "客戶姓名為必填")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "姓名長度需介於 1-50 字元")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 電話號碼
    /// </summary>
    [Required(ErrorMessage = "電話號碼為必填")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "電話號碼長度需介於 6-20 字元")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 地址（選填）
    /// </summary>
    [StringLength(200)]
    public string? Address { get; set; }

    /// <summary>
    /// 備註（選填）
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
