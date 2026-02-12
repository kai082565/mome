using System.Printing;
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

    public Task<bool> PrintCertificateAsync(CertificateData data)
    {
        try
        {
            var settings = AppSettings.Instance.CertificateForm;
            var printDialog = new PrintDialog();

            // 設定 Landscape 方向
            printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

            // 建立 FixedDocument
            var fixedDoc = new FixedDocument();
            var pageContent = new PageContent();
            var page = new FixedPage();

            if (settings.PageWidthMm > 0 && settings.PageHeightMm > 0)
            {
                page.Width = MmToDip(settings.PageWidthMm);
                page.Height = MmToDip(settings.PageHeightMm);
            }

            // 依序加入各欄位文字
            AddField(page, settings.Name, data.Name);
            AddField(page, settings.Phone, data.Phone);
            AddField(page, settings.Address, data.Address);
            AddField(page, settings.BirthYear, data.BirthYear);
            AddField(page, settings.BirthMonth, data.BirthMonth);
            AddField(page, settings.BirthDay, data.BirthDay);
            AddField(page, settings.LunarStartDate, data.LunarStartDate);
            AddField(page, settings.LunarEndDate, data.LunarEndDate);
            AddField(page, settings.Amount, data.Amount);
            AddField(page, settings.LampType, data.LampType);

            ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
            fixedDoc.Pages.Add(pageContent);

            var paginator = fixedDoc.DocumentPaginator;
            printDialog.PrintDocument(paginator, $"感謝狀 - {data.Name}");

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"列印感謝狀失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            return Task.FromResult(false);
        }
    }

    private static double MmToDip(double mm)
    {
        return mm * 96.0 / 25.4;
    }

    private static void AddField(FixedPage page, CertificateFieldPosition pos, string? text)
    {
        if (string.IsNullOrEmpty(text) || (pos.X == 0 && pos.Y == 0)) return;

        var tb = new TextBlock
        {
            Text = text,
            FontSize = pos.FontSize,
            FontFamily = new FontFamily("Microsoft JhengHei")
        };

        if (pos.Rotation != 0)
            tb.LayoutTransform = new RotateTransform(pos.Rotation);

        FixedPage.SetLeft(tb, MmToDip(pos.X));
        FixedPage.SetTop(tb, MmToDip(pos.Y));
        page.Children.Add(tb);
    }

    public Task<bool> PrintCustomerLetterAsync(CustomerInfoLetter letter)
    {
        try
        {
            var printDialog = new PrintDialog();

            if (printDialog.ShowDialog() == true)
            {
                var document = CreateLetterFlowDocument(letter);
                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"客戶資料信件 - {letter.CustomerName}");
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

    public async Task<string> SaveCustomerLetterAsPdfAsync(CustomerInfoLetter letter, string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 檔案|*.pdf",
                FileName = $"客戶資料_{letter.CustomerName}_{letter.PrintDate:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
                return string.Empty;

            filePath = saveDialog.FileName;
        }

        var finalPath = filePath;
        await Task.Run(() =>
        {
            var document = CreateLetterPdfDocument(letter);
            document.GeneratePdf(finalPath);
        });

        return finalPath;
    }

    private FlowDocument CreateLetterFlowDocument(CustomerInfoLetter letter)
    {
        var document = new FlowDocument
        {
            PageWidth = 793.7, // A4 width in WPF units (210mm)
            PageHeight = 1122.5, // A4 height (297mm)
            PagePadding = new Thickness(60, 50, 60, 50),
            FontFamily = new FontFamily("Microsoft JhengHei"),
            FontSize = 14
        };

        // 宮廟標題
        var title = new Paragraph(new Run(letter.TempleName))
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        document.Blocks.Add(title);

        // 副標題
        var subtitle = new Paragraph(new Run("客戶資料通知函"))
        {
            FontSize = 18,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        document.Blocks.Add(subtitle);

        // 列印日期
        var dateText = new Paragraph(new Run($"列印日期：{letter.PrintDate:yyyy/MM/dd}"))
        {
            TextAlignment = TextAlignment.Right,
            FontSize = 12,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 15)
        };
        document.Blocks.Add(dateText);

        document.Blocks.Add(CreateSeparator());

        // 客戶資料
        document.Blocks.Add(CreateSectionTitle("【客戶資料】"));
        document.Blocks.Add(CreateInfoLine("姓    名", letter.CustomerName));
        if (!string.IsNullOrEmpty(letter.CustomerCode))
            document.Blocks.Add(CreateInfoLine("客戶編號", letter.CustomerCode));
        if (!string.IsNullOrEmpty(letter.CustomerPhone))
            document.Blocks.Add(CreateInfoLine("電    話", letter.CustomerPhone));
        if (!string.IsNullOrEmpty(letter.CustomerMobile))
            document.Blocks.Add(CreateInfoLine("手    機", letter.CustomerMobile));

        var fullAddress = string.Join("", new[]
        {
            letter.CustomerPostalCode,
            letter.CustomerVillage,
            letter.CustomerAddress
        }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(fullAddress))
            document.Blocks.Add(CreateInfoLine("地    址", fullAddress));

        document.Blocks.Add(CreateSeparator());

        // 點燈紀錄表格
        document.Blocks.Add(CreateSectionTitle("【點燈紀錄】"));

        if (letter.Orders.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run("尚無點燈紀錄"))
            {
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center
            });
        }
        else
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 5, 0, 10) };
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });   // 年度
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });  // 燈種
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });  // 起始日
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });  // 到期日
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });   // 金額
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });   // 狀態

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(CreateHeaderCell("年度"));
            headerRow.Cells.Add(CreateHeaderCell("燈種"));
            headerRow.Cells.Add(CreateHeaderCell("起始日"));
            headerRow.Cells.Add(CreateHeaderCell("到期日"));
            headerRow.Cells.Add(CreateHeaderCell("金額"));
            headerRow.Cells.Add(CreateHeaderCell("狀態"));
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            var bodyGroup = new TableRowGroup();
            foreach (var order in letter.Orders)
            {
                var row = new TableRow();
                row.Cells.Add(CreateCell($"{order.Year}年"));
                row.Cells.Add(CreateCell(order.LampName));
                row.Cells.Add(CreateCell(order.StartDate.ToString("yyyy/MM/dd")));
                row.Cells.Add(CreateCell(order.EndDate.ToString("yyyy/MM/dd")));
                row.Cells.Add(CreateCell($"${order.Price:N0}"));
                var statusCell = CreateCell(order.StatusText);
                if (order.IsExpired)
                    statusCell.Foreground = Brushes.Red;
                else if (order.IsActive)
                    statusCell.Foreground = Brushes.Green;
                row.Cells.Add(statusCell);
                bodyGroup.Rows.Add(row);
            }
            table.RowGroups.Add(bodyGroup);
            document.Blocks.Add(table);
        }

        // 備註
        if (!string.IsNullOrEmpty(letter.Note))
        {
            document.Blocks.Add(CreateSeparator());
            document.Blocks.Add(CreateSectionTitle("【備註】"));
            document.Blocks.Add(new Paragraph(new Run(letter.Note))
            {
                Margin = new Thickness(0, 2, 0, 5),
                TextAlignment = TextAlignment.Left
            });
        }

        document.Blocks.Add(CreateSeparator());

        // 宮廟聯繫資訊
        var footer = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 12,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 20, 0, 0)
        };
        footer.Inlines.Add(new Run(letter.TempleName + "\n"));
        if (!string.IsNullOrEmpty(letter.TempleAddress))
            footer.Inlines.Add(new Run(letter.TempleAddress + "\n"));
        if (!string.IsNullOrEmpty(letter.TemplePhone))
            footer.Inlines.Add(new Run("電話：" + letter.TemplePhone));
        document.Blocks.Add(footer);

        return document;
    }

    private static TableCell CreateHeaderCell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Margin = new Thickness(4, 2, 4, 2)
        });
        cell.BorderBrush = Brushes.Gray;
        cell.BorderThickness = new Thickness(0, 0, 0, 1);
        return cell;
    }

    private static TableCell CreateCell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text))
        {
            FontSize = 12,
            Margin = new Thickness(4, 2, 4, 2)
        });
        return cell;
    }

    private IDocument CreateLetterPdfDocument(CustomerInfoLetter letter)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content().Column(column =>
                {
                    // 宮廟標題
                    column.Item().AlignCenter().Text(letter.TempleName).FontSize(24).Bold();
                    column.Item().AlignCenter().Text("客戶資料通知函").FontSize(16);
                    column.Item().AlignRight().Text($"列印日期：{letter.PrintDate:yyyy/MM/dd}").FontSize(10).FontColor(QuestColors.Grey.Medium);
                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestColors.Grey.Medium);

                    // 客戶資料
                    column.Item().PaddingTop(10).Text("【客戶資料】").Bold();
                    column.Item().Text($"姓    名：{letter.CustomerName}");
                    if (!string.IsNullOrEmpty(letter.CustomerCode))
                        column.Item().Text($"客戶編號：{letter.CustomerCode}");
                    if (!string.IsNullOrEmpty(letter.CustomerPhone))
                        column.Item().Text($"電    話：{letter.CustomerPhone}");
                    if (!string.IsNullOrEmpty(letter.CustomerMobile))
                        column.Item().Text($"手    機：{letter.CustomerMobile}");

                    var fullAddress = string.Join("", new[]
                    {
                        letter.CustomerPostalCode,
                        letter.CustomerVillage,
                        letter.CustomerAddress
                    }.Where(s => !string.IsNullOrEmpty(s)));
                    if (!string.IsNullOrEmpty(fullAddress))
                        column.Item().Text($"地    址：{fullAddress}");

                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestColors.Grey.Lighten2);

                    // 點燈紀錄表格
                    column.Item().PaddingTop(10).Text("【點燈紀錄】").Bold();

                    if (letter.Orders.Count == 0)
                    {
                        column.Item().PaddingVertical(10).AlignCenter().Text("尚無點燈紀錄").FontColor(QuestColors.Grey.Medium);
                    }
                    else
                    {
                        column.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);   // 年度
                                columns.RelativeColumn(2);   // 燈種
                                columns.RelativeColumn(2);   // 起始日
                                columns.RelativeColumn(2);   // 到期日
                                columns.RelativeColumn(1.5f); // 金額
                                columns.RelativeColumn(1.2f); // 狀態
                            });

                            // 表頭
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("年度").Bold().FontSize(11);
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("燈種").Bold().FontSize(11);
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("起始日").Bold().FontSize(11);
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("到期日").Bold().FontSize(11);
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("金額").Bold().FontSize(11);
                                header.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Medium)
                                    .Padding(4).Text("狀態").Bold().FontSize(11);
                            });

                            // 資料列
                            foreach (var order in letter.Orders)
                            {
                                table.Cell().Padding(4).Text($"{order.Year}年").FontSize(11);
                                table.Cell().Padding(4).Text(order.LampName).FontSize(11);
                                table.Cell().Padding(4).Text(order.StartDate.ToString("yyyy/MM/dd")).FontSize(11);
                                table.Cell().Padding(4).Text(order.EndDate.ToString("yyyy/MM/dd")).FontSize(11);
                                table.Cell().Padding(4).Text($"${order.Price:N0}").FontSize(11);

                                var statusColor = order.IsExpired ? QuestColors.Red.Medium
                                    : order.IsActive ? QuestColors.Green.Medium
                                    : QuestColors.Grey.Medium;
                                table.Cell().Padding(4).Text(order.StatusText).FontSize(11).FontColor(statusColor);
                            }
                        });
                    }

                    // 備註
                    if (!string.IsNullOrEmpty(letter.Note))
                    {
                        column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestColors.Grey.Lighten2);
                        column.Item().PaddingTop(10).Text("【備註】").Bold();
                        column.Item().PaddingTop(5).Text(letter.Note);
                    }

                    column.Item().PaddingVertical(15).LineHorizontal(1).LineColor(QuestColors.Grey.Medium);

                    // 宮廟聯繫資訊
                    column.Item().PaddingTop(15).AlignCenter().Column(info =>
                    {
                        info.Item().AlignCenter().Text(letter.TempleName).FontSize(10).FontColor(QuestColors.Grey.Medium);
                        if (!string.IsNullOrEmpty(letter.TempleAddress))
                            info.Item().AlignCenter().Text(letter.TempleAddress).FontSize(10).FontColor(QuestColors.Grey.Medium);
                        if (!string.IsNullOrEmpty(letter.TemplePhone))
                            info.Item().AlignCenter().Text($"電話：{letter.TemplePhone}").FontSize(10).FontColor(QuestColors.Grey.Medium);
                    });
                });
            });
        });
    }

}
