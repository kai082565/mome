using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using QuestPDF.Fluent;
using QuestColors = QuestPDF.Helpers.Colors;
using QuestPDF.Infrastructure;
using static QuestPDF.Helpers.PageSizes;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class PrintService : IPrintService
{
    static PrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<bool> PrintReceiptAsync(Receipt receipt)
    {
        try
        {
            var printDialog = new PrintDialog();

            if (printDialog.ShowDialog() == true)
            {
                var document = CreateFlowDocument(receipt);
                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"點燈單據 - {receipt.CustomerName}");
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"列印失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            return Task.FromResult(false);
        }
    }

    public async Task<string> SaveReceiptAsPdfAsync(Receipt receipt, string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 檔案|*.pdf",
                FileName = $"點燈單據_{receipt.CustomerName}_{receipt.Year}年{receipt.LampName}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
                return string.Empty;

            filePath = saveDialog.FileName;
        }

        var finalPath = filePath;
        await Task.Run(() =>
        {
            var document = CreatePdfDocument(receipt);
            document.GeneratePdf(finalPath);
        });

        return finalPath;
    }

    public void PreviewReceipt(Receipt receipt)
    {
        var previewWindow = new Window
        {
            Title = "單據預覽",
            Width = 400,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var viewer = new FlowDocumentScrollViewer
        {
            Document = CreateFlowDocument(receipt),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        previewWindow.Content = viewer;
        previewWindow.ShowDialog();
    }

    private FlowDocument CreateFlowDocument(Receipt receipt)
    {
        var document = new FlowDocument
        {
            PageWidth = 300,
            PagePadding = new Thickness(20),
            FontFamily = new FontFamily("Microsoft JhengHei"),
            FontSize = 12
        };

        // 標題
        var title = new Paragraph(new Run(receipt.TempleName))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        document.Blocks.Add(title);

        // 副標題
        var subtitle = new Paragraph(new Run("點燈功德單"))
        {
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        };
        document.Blocks.Add(subtitle);

        document.Blocks.Add(CreateSeparator());

        document.Blocks.Add(CreateInfoLine("單據編號", receipt.ReceiptNo));
        document.Blocks.Add(CreateInfoLine("列印日期", receipt.PrintDate.ToString("yyyy/MM/dd HH:mm")));

        document.Blocks.Add(CreateSeparator());

        document.Blocks.Add(CreateSectionTitle("【信眾資料】"));
        document.Blocks.Add(CreateInfoLine("姓    名", receipt.CustomerName));
        if (!string.IsNullOrEmpty(receipt.CustomerPhone))
            document.Blocks.Add(CreateInfoLine("電    話", receipt.CustomerPhone));
        if (!string.IsNullOrEmpty(receipt.CustomerMobile))
            document.Blocks.Add(CreateInfoLine("手    機", receipt.CustomerMobile));
        if (!string.IsNullOrEmpty(receipt.CustomerAddress))
            document.Blocks.Add(CreateInfoLine("地    址", receipt.CustomerAddress));

        document.Blocks.Add(CreateSeparator());

        document.Blocks.Add(CreateSectionTitle("【點燈資料】"));
        document.Blocks.Add(CreateInfoLine("燈    種", receipt.LampName));
        document.Blocks.Add(CreateInfoLine("年    度", $"{receipt.Year} 年"));
        document.Blocks.Add(CreateInfoLine("起始日期", receipt.StartDate.ToString("yyyy/MM/dd")));
        document.Blocks.Add(CreateInfoLine("結束日期", receipt.EndDate.ToString("yyyy/MM/dd")));

        // 農曆期間
        var lunarPeriod = new Paragraph(new Run(receipt.LunarPeriod))
        {
            Margin = new Thickness(0, 5, 0, 2),
            TextAlignment = TextAlignment.Center,
            FontWeight = FontWeights.Bold
        };
        document.Blocks.Add(lunarPeriod);

        document.Blocks.Add(CreateInfoLine("功德金額", $"NT$ {receipt.Price:N0}"));

        document.Blocks.Add(CreateSeparator());

        var blessing = new Paragraph(new Run("祈願 闔家平安 身體健康 萬事如意"))
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 15, 0, 15),
            FontStyle = FontStyles.Italic
        };
        document.Blocks.Add(blessing);

        document.Blocks.Add(CreateSeparator());

        var templeInfo = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Foreground = Brushes.Gray
        };
        templeInfo.Inlines.Add(new Run(receipt.TempleName + "\n"));
        templeInfo.Inlines.Add(new Run(receipt.TempleAddress + "\n"));
        templeInfo.Inlines.Add(new Run("電話：" + receipt.TemplePhone));
        document.Blocks.Add(templeInfo);

        return document;
    }

    private Paragraph CreateSectionTitle(string title)
    {
        return new Paragraph(new Run(title))
        {
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 5)
        };
    }

    private Paragraph CreateInfoLine(string label, string value)
    {
        var para = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
        para.Inlines.Add(new Run(label + "："));
        para.Inlines.Add(new Run(value));
        return para;
    }

    private BlockUIContainer CreateSeparator()
    {
        return new BlockUIContainer(new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 5, 0, 5)
        });
    }

    private IDocument CreatePdfDocument(Receipt receipt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(A6);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Item().AlignCenter().Text(receipt.TempleName).FontSize(20).Bold();
                    column.Item().AlignCenter().Text("點燈功德單").FontSize(14);
                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestColors.Grey.Medium);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"單據編號：{receipt.ReceiptNo}").FontSize(9);
                        row.RelativeItem().AlignRight().Text($"{receipt.PrintDate:yyyy/MM/dd HH:mm}").FontSize(9);
                    });

                    column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(QuestColors.Grey.Lighten2);

                    column.Item().PaddingTop(10).Text("【信眾資料】").Bold();
                    column.Item().Text($"姓    名：{receipt.CustomerName}");
                    if (!string.IsNullOrEmpty(receipt.CustomerPhone))
                        column.Item().Text($"電    話：{receipt.CustomerPhone}");
                    if (!string.IsNullOrEmpty(receipt.CustomerMobile))
                        column.Item().Text($"手    機：{receipt.CustomerMobile}");
                    if (!string.IsNullOrEmpty(receipt.CustomerAddress))
                        column.Item().Text($"地    址：{receipt.CustomerAddress}");

                    column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(QuestColors.Grey.Lighten2);

                    column.Item().PaddingTop(10).Text("【點燈資料】").Bold();
                    column.Item().Text($"燈    種：{receipt.LampName}");
                    column.Item().Text($"年    度：{receipt.Year} 年");
                    column.Item().Text($"起始日期：{receipt.StartDate:yyyy/MM/dd}");
                    column.Item().Text($"結束日期：{receipt.EndDate:yyyy/MM/dd}");

                    // 農曆期間
                    column.Item().PaddingVertical(5).AlignCenter()
                        .Text(receipt.LunarPeriod).Bold();

                    column.Item().Text($"功德金額：NT$ {receipt.Price:N0}").Bold();

                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestColors.Grey.Medium);

                    column.Item().PaddingVertical(15).AlignCenter()
                        .Text("祈願 闔家平安 身體健康 萬事如意").Italic();

                    column.Item().LineHorizontal(1).LineColor(QuestColors.Grey.Lighten2);

                    column.Item().PaddingTop(10).AlignCenter().Column(info =>
                    {
                        info.Item().AlignCenter().Text(receipt.TempleName).FontSize(9).FontColor(QuestColors.Grey.Medium);
                        info.Item().AlignCenter().Text(receipt.TempleAddress).FontSize(9).FontColor(QuestColors.Grey.Medium);
                        info.Item().AlignCenter().Text($"電話：{receipt.TemplePhone}").FontSize(9).FontColor(QuestColors.Grey.Medium);
                    });
                });
            });
        });
    }
}
