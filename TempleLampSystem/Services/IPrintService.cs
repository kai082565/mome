using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface IPrintService
{
    /// <summary>
    /// 列印單據到印表機
    /// </summary>
    Task<bool> PrintReceiptAsync(Receipt receipt);

    /// <summary>
    /// 將單據儲存為 PDF
    /// </summary>
    Task<string> SaveReceiptAsPdfAsync(Receipt receipt, string? filePath = null);

    /// <summary>
    /// 預覽單據
    /// </summary>
    void PreviewReceipt(Receipt receipt);

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
