using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface IPrintService
{
    /// <summary>
    /// 列印感謝狀（套印預印表單）
    /// </summary>
    Task<bool> PrintCertificateAsync(CertificateData data);

    /// <summary>
    /// 列印客戶資料信件
    /// </summary>
    Task<bool> PrintCustomerLetterAsync(CustomerInfoLetter letter);

    /// <summary>
    /// 將客戶資料信件儲存為 PDF
    /// </summary>
    Task<string> SaveCustomerLetterAsPdfAsync(CustomerInfoLetter letter, string? filePath = null);
}
