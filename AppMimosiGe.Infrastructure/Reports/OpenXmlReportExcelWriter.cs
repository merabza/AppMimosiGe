using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AppMimosiGe.Application.Reports;
using AppMimosiGeShared.Contracts.V1.Responses;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AppMimosiGe.Infrastructure.Reports;

/// <summary>
///     რეპორტის xlsx ფაილი DocumentFormat.OpenXml-ით (D113): სათაური, პარამეტრები, სვეტების სათაურები (ზემოთ
///     დამაგრებული), სექციები სათაურით და ჯამით, რეპორტის ჯამები. რიცხვი, დრო და თარიღი Excel-ის რიცხვად ჩაიწერება
///     ფორმატით, ტექსტი ტექსტად; ქართული ტექსტისთვის შრიფტი Sylfaen-ია
/// </summary>
public sealed class OpenXmlReportExcelWriter : IReportExcelWriter
{
    private const string FontName = "Sylfaen";
    private const double FontSize = 11;
    private const double TitleFontSize = 14;
    private const int MaxSheetNameLength = 31;
    private const double MinColumnWidth = 6;
    private const double MaxColumnWidth = 60;

    //ჩაშენებული ფორმატი "0.00" და საკუთარი ფორმატები
    private const uint NumberFormatId = 2;
    private const uint TimeFormatId = 164;
    private const uint DateFormatId = 165;
    private const uint DateTimeFormatId = 166;

    //CellFormats-ის ინდექსი: ფორმატის სახე × 2 (+1 მუქი), ბოლოს სათაური
    private const uint TitleStyle = 10;

    private static readonly bool[] BoldVariants = [false, true];

    public byte[] Write(ReportResponse report)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.AddNewPart<WorkbookStylesPart>().Stylesheet = CreateStylesheet();

            var sheetData = new SheetData();
            uint headerRowIndex = WriteRows(report, sheetData);

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var worksheet = new Worksheet();
            worksheet.AppendChild(FrozenHeader(headerRowIndex));
            worksheet.AppendChild(ColumnWidths(report));
            worksheet.AppendChild(sheetData);
            worksheetPart.Worksheet = worksheet;

            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = report.Key[..Math.Min(report.Key.Length, MaxSheetNameLength)]
            });
        }

        return stream.ToArray();
    }

    //აბრუნებს სვეტების სათაურების სტრიქონის ნომერს
    private static uint WriteRows(ReportResponse report, SheetData sheetData)
    {
        uint rowIndex = 1;
        sheetData.AppendChild(TextRow(rowIndex++, report.Title, TitleStyle));
        if (report.Parameters.Count > 0)
        {
            sheetData.AppendChild(TextRow(rowIndex++,
                string.Join("; ", report.Parameters.Select(p => $"{p.Caption}: {p.Value}")), Style(Kind.General)));
        }

        rowIndex++;
        uint headerRowIndex = rowIndex;
        sheetData.AppendChild(CellsRow(rowIndex++, [.. report.Columns.Select(c => (object?)c.Caption)], report.Columns,
            true));

        foreach (ReportSectionResponse section in report.Sections)
        {
            if (section.Header is not null)
            {
                sheetData.AppendChild(TextRow(rowIndex++, section.Header, Style(Kind.General, true)));
            }

            foreach (List<object?> row in section.Rows)
            {
                sheetData.AppendChild(CellsRow(rowIndex++, row, report.Columns, false));
            }

            if (section.Footer is not null)
            {
                sheetData.AppendChild(CellsRow(rowIndex++, section.Footer, report.Columns, true));
            }
        }

        foreach (List<object?> footerRow in report.FooterRows)
        {
            sheetData.AppendChild(CellsRow(rowIndex++, footerRow, report.Columns, true));
        }

        return headerRowIndex;
    }

    private static Row TextRow(uint rowIndex, string text, uint style)
    {
        var row = new Row { RowIndex = rowIndex };
        row.AppendChild(TextCell($"A{rowIndex}", text, style));
        return row;
    }

    private static Row CellsRow(uint rowIndex, List<object?> values, List<ReportColumnResponse> columns, bool bold)
    {
        var row = new Row { RowIndex = rowIndex };
        for (int i = 0; i < values.Count; i++)
        {
            Cell? cell = ValueCell($"{ColumnName(i)}{rowIndex}", values[i],
                i < columns.Count ? columns[i].Type : ReportColumnTypes.Text, bold);
            if (cell is not null)
            {
                row.AppendChild(cell);
            }
        }

        return row;
    }

    //null: ცარიელი უჯრა (Excel-ში არ იწერება)
    private static Cell? ValueCell(string reference, object? value, string columnType, bool bold)
    {
        return value switch
        {
            null => null,
            string text => TextCell(reference, text, Style(Kind.General, bold)),
            int or long or short or byte => NumberCell(reference,
                Convert.ToDecimal(value, CultureInfo.InvariantCulture), Style(Kind.General, bold)),
            decimal or double or float => NumberCell(reference, Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                Style(Kind.Number, bold)),
            TimeOnly time => NumberCell(reference, (decimal)time.ToTimeSpan().TotalDays, Style(Kind.Time, bold)),
            DateTime dateTime => NumberCell(reference, (decimal)dateTime.ToOADate(),
                Style(columnType == ReportColumnTypes.DateTime ? Kind.DateTime : Kind.Date, bold)),
            DateOnly date => NumberCell(reference, (decimal)date.ToDateTime(TimeOnly.MinValue).ToOADate(),
                Style(Kind.Date, bold)),
            _ => TextCell(reference, string.Create(CultureInfo.InvariantCulture, $"{value}"), Style(Kind.General, bold))
        };
    }

    private static Cell TextCell(string reference, string text, uint style)
    {
        var inlineString = new InlineString();
        inlineString.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var cell = new Cell { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style };
        cell.AppendChild(inlineString);
        return cell;
    }

    private static Cell NumberCell(string reference, decimal number, uint style)
    {
        var cell = new Cell { CellReference = reference, DataType = CellValues.Number, StyleIndex = style };
        cell.AppendChild(new CellValue(number.ToString(CultureInfo.InvariantCulture)));
        return cell;
    }

    //A, B, …, Z, AA, …
    private static string ColumnName(int index)
    {
        var name = new StringBuilder();
        for (int number = index + 1; number > 0; number = (number - 1) / 26)
        {
            name.Insert(0, (char)('A' + (number - 1) % 26));
        }

        return name.ToString();
    }

    private static uint Style(Kind kind, bool bold = false)
    {
        return (uint)kind * 2 + (bold ? 1u : 0u);
    }

    //სვეტების სათაურები გადახვევისას ჩანს
    private static SheetViews FrozenHeader(uint headerRowIndex)
    {
        var sheetView = new SheetView { WorkbookViewId = 0 };
        sheetView.AppendChild(new Pane
        {
            VerticalSplit = headerRowIndex,
            TopLeftCell = $"A{headerRowIndex + 1}",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen
        });
        var sheetViews = new SheetViews();
        sheetViews.AppendChild(sheetView);
        return sheetViews;
    }

    //სიგანე უჯრების ტექსტის სიგრძით (სათაურის და პარამეტრების სტრიქონის გარეშე)
    private static Columns ColumnWidths(ReportResponse report)
    {
        List<List<object?>> rows =
        [
            [.. report.Columns.Select(c => (object?)c.Caption)],
            .. report.Sections.SelectMany(s => s.Footer is null ? s.Rows : [.. s.Rows, s.Footer]),
            .. report.FooterRows
        ];
        int columnCount = rows.Max(r => r.Count);

        var columns = new Columns();
        for (int i = 0; i < columnCount; i++)
        {
            int index = i;
            int length = rows.Where(r => index < r.Count).Max(r => DisplayLength(r[index]));
            columns.AppendChild(new Column
            {
                Min = (uint)(i + 1),
                Max = (uint)(i + 1),
                Width = Math.Clamp(length + 2, MinColumnWidth, MaxColumnWidth),
                CustomWidth = true
            });
        }

        return columns;
    }

    private static int DisplayLength(object? value)
    {
        return value switch
        {
            null => 0,
            string text => text.Length,
            TimeOnly => 5,
            DateOnly => 10,
            DateTime => 16,
            decimal or double or float => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                .ToString("0.00", CultureInfo.InvariantCulture).Length,
            _ => string.Create(CultureInfo.InvariantCulture, $"{value}").Length
        };
    }

    private static Stylesheet CreateStylesheet()
    {
        uint[] formatIds = [0, NumberFormatId, TimeFormatId, DateFormatId, DateTimeFormatId];

        var numberingFormats = new NumberingFormats();
        numberingFormats.AppendChild(new NumberingFormat { NumberFormatId = TimeFormatId, FormatCode = "hh:mm" });
        numberingFormats.AppendChild(new NumberingFormat { NumberFormatId = DateFormatId, FormatCode = "dd.mm.yyyy" });
        numberingFormats.AppendChild(new NumberingFormat
        {
            NumberFormatId = DateTimeFormatId, FormatCode = "dd.mm.yyyy hh:mm"
        });

        var fonts = new Fonts();
        fonts.AppendChild(Font(FontSize, false));
        fonts.AppendChild(Font(FontSize, true));
        fonts.AppendChild(Font(TitleFontSize, true));

        var fills = new Fills();
        fills.AppendChild(Fill(PatternValues.None));
        fills.AppendChild(Fill(PatternValues.Gray125));

        var border = new Border();
        border.AppendChild(new LeftBorder());
        border.AppendChild(new RightBorder());
        border.AppendChild(new TopBorder());
        border.AppendChild(new BottomBorder());
        border.AppendChild(new DiagonalBorder());
        var borders = new Borders();
        borders.AppendChild(border);

        var cellFormats = new CellFormats();
        foreach (Kind kind in Enum.GetValues<Kind>())
        {
            foreach (bool bold in BoldVariants)
            {
                uint formatId = formatIds[(int)kind];
                cellFormats.AppendChild(new CellFormat
                {
                    NumberFormatId = formatId,
                    FontId = bold ? 1u : 0u,
                    FillId = 0,
                    BorderId = 0,
                    ApplyNumberFormat = formatId != 0,
                    ApplyFont = bold
                });
            }
        }

        cellFormats.AppendChild(new CellFormat
        {
            NumberFormatId = 0,
            FontId = 2,
            FillId = 0,
            BorderId = 0,
            ApplyFont = true
        });

        var stylesheet = new Stylesheet();
        stylesheet.AppendChild(numberingFormats);
        stylesheet.AppendChild(fonts);
        stylesheet.AppendChild(fills);
        stylesheet.AppendChild(borders);
        stylesheet.AppendChild(cellFormats);
        return stylesheet;
    }

    private static Font Font(double size, bool bold)
    {
        var font = new Font();
        if (bold)
        {
            font.AppendChild(new Bold());
        }

        font.AppendChild(new FontSize { Val = size });
        font.AppendChild(new FontName { Val = FontName });
        return font;
    }

    private static Fill Fill(PatternValues pattern)
    {
        var fill = new Fill();
        fill.AppendChild(new PatternFill { PatternType = pattern });
        return fill;
    }

    //CellFormats-ის რიგი: General, Number, Time, Date, DateTime (თითოეული ჩვეულებრივი და მუქი)
    private enum Kind
    {
        General = 0,
        Number = 1,
        Time = 2,
        Date = 3,
        DateTime = 4
    }
}
