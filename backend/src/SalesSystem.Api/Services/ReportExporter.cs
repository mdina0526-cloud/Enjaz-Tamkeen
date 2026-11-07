using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

/// <summary>Renders a <see cref="ReportResult"/> to Excel (RTL sheet) or PDF (RTL layout).</summary>
public class ReportExporter(BusinessClock clock, IOptions<AppOptions> options)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Navy = "#0B1F4B";
    private const string Royal = "#1E40AF";

    private static string Format(object? value, string kind) => value switch
    {
        null => "",
        decimal d when kind == "money" => d.ToString("N2", Inv),
        decimal d => d.ToString("0.##", Inv),
        IFormattable f => f.ToString(null, Inv),
        _ => value.ToString() ?? "",
    };

    public byte[] ToExcel(ReportResult report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("التقرير");
        ws.RightToLeft = true;

        var row = 1;
        ws.Cell(row, 1).Value = options.Value.CompanyName;
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(XLColor.FromHtml(Royal));
        row++;
        ws.Cell(row, 1).Value = report.Title;
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.FromHtml(Navy));
        row++;
        ws.Cell(row, 1).Value = $"الفترة: {report.Period}    |    تاريخ الإصدار: {clock.ToLocal(report.GeneratedAtUtc).ToString("yyyy-MM-dd HH:mm", Inv)}";
        row += 2;

        foreach (var item in report.Summary)
        {
            ws.Cell(row, 1).Value = item.Label;
            ws.Cell(row, 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EEF2FF"));
            ws.Cell(row, 2).Value = item.Value;
            row++;
        }
        row++;

        foreach (var section in report.Sections)
        {
            ws.Cell(row, 1).Value = section.Title;
            ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(XLColor.FromHtml(Navy));
            row++;
            for (var c = 0; c < section.Columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = section.Columns[c].Title;
                cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(Navy));
            }
            var headerRow = row;
            row++;
            foreach (var r in section.Rows)
            {
                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var cell = ws.Cell(row, c + 1);
                    var col = section.Columns[c];
                    switch (r[c])
                    {
                        case decimal d:
                            cell.Value = d;
                            cell.Style.NumberFormat.Format = col.Kind == "money" ? "#,##0.00" : "0.##";
                            break;
                        case int i:
                            cell.Value = i;
                            break;
                        default:
                            cell.Value = Format(r[c], col.Kind);
                            break;
                    }
                }
                row++;
            }
            if (section.Rows.Count > 0)
                ws.Range(headerRow, 1, row - 1, section.Columns.Count).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Hair);
            row += 2;
        }

        ws.Columns().AdjustToContents(1, row, 10, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ToPdf(ReportResult report)
    {
        var company = options.Value.CompanyName;
        var generated = ReportService.Ltr(clock.ToLocal(report.GeneratedAtUtc).ToString("yyyy-MM-dd HH:mm", Inv));

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t.FontSize(9).FontFamily("IBM Plex Sans Arabic"));

            page.Header().Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(company).FontSize(10).FontColor(Royal).SemiBold();
                        c.Item().Text(report.Title).FontSize(18).Bold().FontColor(Navy);
                    });
                    r.ConstantItem(220).AlignLeft().Column(c =>
                    {
                        c.Item().Text($"الفترة: {report.Period}").FontSize(9);
                        c.Item().Text($"تاريخ الإصدار: {generated}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
                col.Item().PaddingVertical(6).LineHorizontal(2).LineColor(Royal);
            });

            page.Content().Column(col =>
            {
                col.Spacing(10);
                col.Item().Table(t =>
                {
                    var perRow = 4;
                    t.ColumnsDefinition(c => { for (var i = 0; i < perRow; i++) c.RelativeColumn(); });
                    foreach (var s in report.Summary)
                        t.Cell().Padding(3).Background("#EEF2FF").Border(0.5f).BorderColor("#C7D2FE").Padding(6).Column(c =>
                        {
                            c.Item().Text(s.Label).FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text(s.Value).FontSize(12).Bold().FontColor(Navy);
                        });
                });

                foreach (var section in report.Sections)
                {
                    col.Item().PaddingTop(6).Text(section.Title).FontSize(12).Bold().FontColor(Navy);
                    if (section.Rows.Count == 0)
                    {
                        col.Item().Text("لا توجد بيانات").FontColor(Colors.Grey.Darken1);
                        continue;
                    }
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { foreach (var _ in section.Columns) c.RelativeColumn(); });
                        t.Header(h =>
                        {
                            foreach (var c in section.Columns)
                                h.Cell().Background(Navy).Padding(4).Text(c.Title).FontColor(Colors.White).SemiBold();
                        });
                        var i = 0;
                        foreach (var r in section.Rows)
                        {
                            string bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            for (var c = 0; c < section.Columns.Count; c++)
                                t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(Format(r[c], section.Columns[c].Kind));
                        }
                    });
                }
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("صفحة ");
                x.CurrentPageNumber();
                x.Span(" من ");
                x.TotalPages();
            });
        })).GeneratePdf();
    }
}
