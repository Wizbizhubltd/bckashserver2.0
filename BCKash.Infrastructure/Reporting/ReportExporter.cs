using System.Globalization;
using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;
using ClosedXML.Excel;
using CsvHelper;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Renders the generic <see cref="ReportResult"/> shape to PDF/CSV/XLS (FR-RPT-2) — one exporter
/// for all 29 reports rather than per-report export code, since they all share the same
/// columns+rows shape (see ReportModels.cs's doc comment).
/// </summary>
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // PDFsharp needs a font resolver on non-Windows platforms (no GDI+ to fall back on) —
        // this uses whatever the OS considers its default sans-serif font.
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new SystemFontResolver();
        }
    }

    public byte[] Export(ReportResult result, ReportSchedulerFileFormat format) => format switch
    {
        ReportSchedulerFileFormat.Pdf => ExportPdf(result),
        ReportSchedulerFileFormat.Csv => ExportCsv(result),
        ReportSchedulerFileFormat.Xls => ExportXlsx(result),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public string ContentType(ReportSchedulerFileFormat format) => format switch
    {
        ReportSchedulerFileFormat.Pdf => "application/pdf",
        ReportSchedulerFileFormat.Csv => "text/csv",
        ReportSchedulerFileFormat.Xls => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream",
    };

    public string FileExtension(ReportSchedulerFileFormat format) => format switch
    {
        ReportSchedulerFileFormat.Pdf => "pdf",
        ReportSchedulerFileFormat.Csv => "csv",
        ReportSchedulerFileFormat.Xls => "xlsx",
        _ => "bin",
    };

    private static byte[] ExportCsv(ReportResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var column in result.Columns)
            {
                csv.WriteField(column);
            }

            csv.NextRecord();

            foreach (var row in result.Rows)
            {
                foreach (var cell in row)
                {
                    csv.WriteField(cell);
                }

                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }

    private static byte[] ExportXlsx(ReportResult result)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(result.ReportName.ToString());

        for (var c = 0; c < result.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = result.Columns[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < result.Rows.Count; r++)
        {
            for (var c = 0; c < result.Rows[r].Count; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = result.Rows[r][c] ?? string.Empty;
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// A plain, unstyled tabular PDF — one page per ~40 rows, columns spaced evenly across the
    /// page width. No rich layout library (e.g. MigraDoc) was added; this is intentionally basic.
    /// </summary>
    private static byte[] ExportPdf(ReportResult result)
    {
        const int rowsPerPage = 40;
        const double margin = 30;
        const double rowHeight = 16;

        using var document = new PdfDocument();
        var font = new XFont("Arial", 9);
        var titleFont = new XFont("Arial", 14, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 9, XFontStyleEx.Bold);

        var totalPages = Math.Max(1, (int)Math.Ceiling(result.Rows.Count / (double)rowsPerPage));

        for (var page = 0; page < totalPages; page++)
        {
            var pdfPage = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(pdfPage);

            var y = margin;
            if (page == 0)
            {
                gfx.DrawString(result.ReportName.ToString(), titleFont, XBrushes.Black, new XPoint(margin, y));
                y += 24;
            }

            var columnWidth = (pdfPage.Width.Point - 2 * margin) / Math.Max(1, result.Columns.Count);

            for (var c = 0; c < result.Columns.Count; c++)
            {
                gfx.DrawString(result.Columns[c], headerFont, XBrushes.Black, new XPoint(margin + c * columnWidth, y));
            }

            y += rowHeight;

            var pageRows = result.Rows.Skip(page * rowsPerPage).Take(rowsPerPage);
            foreach (var row in pageRows)
            {
                for (var c = 0; c < row.Count && c < result.Columns.Count; c++)
                {
                    gfx.DrawString(row[c] ?? string.Empty, font, XBrushes.Black, new XPoint(margin + c * columnWidth, y));
                }

                y += rowHeight;
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
