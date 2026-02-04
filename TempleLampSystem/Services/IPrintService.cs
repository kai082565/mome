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
}
