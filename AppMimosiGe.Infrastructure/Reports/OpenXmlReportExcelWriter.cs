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
///     ფორმატით, ტექსტი ტექსტად; ქართული ტექსტისთვის შრიფტი Sylfaen-ია. NewPage სექციის წინ ბეჭდვის გვერდის წყვეტაა
///     (D154). ბეჭდვისთვის (D155): A4, სიგანით ერთ გვერდზე (ფართო რეპორტი ლანდშაფტზე), სვეტების სათაურები ყოველ
///     გვერდზე; ტექსტი უჯრაში გადაიტანება, პარამეტრებისა და სექციის სათაურის სტრიქონი კი სვეტების მთელ სიგანეზეა
///     გაერთიანებული, ამიტომ ტექსტი სვეტებს გარეთ არ გადადის და ზედმეტ ფურცელს არ ქმნის
/// </summary>
public sealed class OpenXmlReportExcelWriter : IReportExcelWriter
{
    private const string FontName = "Sylfaen";
    private const double FontSize = 11;
    private const double TitleFontSize = 14;
    private const int MaxSheetNameLength = 31;
    private const double MinColumnWidth = 6;
    private const double MaxColumnWidth = 60;

    //Sylfaen-ის ქართული ასოს სიგანე Excel-ის სვეტის ერთეულებში (მუქისაც; Excel-ში გაზომილი 1.15–1.3), სხვა
    //სიმბოლოსი 1-ია
    private const double GeorgianLetterWidth = 1.3;

    //გაერთიანებული უჯრის ტექსტის ხაზის სიმაღლე (Sylfaen 11: Excel ეკრანის DPI-ის მიხედვით 14.25–15-ს იყენებს)
    private const double LineHeight = 15;

    //ხაზებად დაყოფისას უჯრის სიგანის ეს წილი ითვლება: მარაგი ეკრანისა და პრინტერის შრიფტის სიგანეების სხვაობისთვის
    private const double WrapWidthShare = 0.9;

    //ამაზე მეტი სვეტის რეპორტი (მაგ. r30, r36) ლანდშაფტზე იბეჭდება, როგორც ვებ-გვერდზე (ReportView-ის
    //wideReportColumns)
    private const int WideReportColumns = 12;

    //Excel-ის ქაღალდის ზომა A4
    private const uint A4PaperSize = 9;

    //Excel-ის ბოლო სვეტის ინდექსი (XFD): გვერდის წყვეტა მთელ სიგანეზეა
    private const uint MaxColumnIndex = 16383;

    //ჩაშენებული ფორმატი "0.00" და საკუთარი ფორმატები
    private const uint NumberFormatId = 2;
    private const uint TimeFormatId = 164;
    private const uint DateFormatId = 165;
    private const uint DateTimeFormatId = 166;

    //CellFormats-ის ინდექსი: 0 ნაგულისხმევი, შემდეგ ფორმატის სახე × 2 + 1 (+1 მუქი), სათაური, უჯრაში გადატანილი
    //ტექსტი (+1 მუქი) და სექციის სათაური
    private const uint TitleStyle = 11;
    private const uint WrappedTextStyle = 12;
    private const uint SectionHeaderStyle = 14;

    private static readonly bool[] BoldVariants = [false, true];

    public byte[] Write(ReportResponse report)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.AddNewPart<WorkbookStylesPart>().Stylesheet = CreateStylesheet();

            List<double> widths = ColumnWidths(report);
            var sheetData = new SheetData();
            (uint headerRowIndex, List<uint> newPageRows, List<uint> textRows) =
                WriteRows(report, sheetData, Math.Max(widths.Sum(), MinColumnWidth));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var worksheet = new Worksheet();
            //PageSetup-ის FitToWidth ამით მოქმედებს
            worksheet.AppendChild(new SheetProperties
            {
                PageSetupProperties = new PageSetupProperties { FitToPage = true }
            });
            worksheet.AppendChild(FrozenHeader(headerRowIndex));
            worksheet.AppendChild(Columns(widths));
            worksheet.AppendChild(sheetData);
            if (widths.Count > 1 && textRows.Count > 0)
            {
                worksheet.AppendChild(MergeCells(textRows, widths.Count));
            }

            worksheet.AppendChild(PageSetup(report));
            if (newPageRows.Count > 0)
            {
                worksheet.AppendChild(RowBreaks(newPageRows));
            }

            worksheetPart.Worksheet = worksheet;

            string sheetName = report.Key[..Math.Min(report.Key.Length, MaxSheetNameLength)];
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = sheetName
            });
            workbookPart.Workbook.AppendChild(PrintTitles(sheetName, headerRowIndex));
        }

        return stream.ToArray();
    }

    //აბრუნებს სვეტების სათაურების სტრიქონის ნომერს, NewPage სექციების პირველი სტრიქონების ნომრებს (მათ წინ
    //ბეჭდვისას გვერდი იწყება) და სვეტების სიგანეზე გასაერთიანებელი სტრიქონების (პარამეტრები, სექციის სათაური)
    //ნომრებს; width სვეტების ჯამური სიგანეა
    private static (uint HeaderRowIndex, List<uint> NewPageRows, List<uint> TextRows) WriteRows(ReportResponse report,
        SheetData sheetData, double width)
    {
        List<uint> newPageRows = [];
        List<uint> textRows = [];
        uint rowIndex = 1;
        sheetData.AppendChild(TextRow(rowIndex++, report.Title, TitleStyle));
        if (report.Parameters.Count > 0)
        {
            textRows.Add(rowIndex);
            sheetData.AppendChild(WrappedTextRow(rowIndex++,
                string.Join("; ", report.Parameters.Select(p => $"{p.Caption}: {p.Value}")), TextStyle(false), width));
        }

        rowIndex++;
        uint headerRowIndex = rowIndex;
        sheetData.AppendChild(CellsRow(rowIndex++, [.. report.Columns.Select(c => (object?)c.Caption)], report.Columns,
            true));

        foreach (ReportSectionResponse section in report.Sections)
        {
            if (section.NewPage)
            {
                newPageRows.Add(rowIndex);
            }

            if (section.Header is not null)
            {
                textRows.Add(rowIndex);
                sheetData.AppendChild(WrappedTextRow(rowIndex++, section.Header, SectionHeaderStyle, width));
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

        return (headerRowIndex, newPageRows, textRows);
    }

    //პარამეტრებისა და სექციის სათაურის სტრიქონები სვეტების მთელ სიგანეზე
    private static MergeCells MergeCells(List<uint> textRows, int columnCount)
    {
        var mergeCells = new MergeCells { Count = (uint)textRows.Count };
        foreach (uint rowIndex in textRows)
        {
            mergeCells.AppendChild(new MergeCell
            {
                Reference = $"A{rowIndex}:{ColumnName(columnCount - 1)}{rowIndex}"
            });
        }

        return mergeCells;
    }

    //A4, სიგანით ერთ გვერდზე, სიმაღლით რამდენიც საჭიროა (ასე NewPage-ის წყვეტები რჩება); ფართო რეპორტი ლანდშაფტზე
    private static PageSetup PageSetup(ReportResponse report)
    {
        return new PageSetup
        {
            PaperSize = A4PaperSize,
            Orientation = report.Columns.Count > WideReportColumns
                ? OrientationValues.Landscape
                : OrientationValues.Portrait,
            FitToWidth = 1,
            FitToHeight = 0
        };
    }

    //სვეტების სათაურების სტრიქონი ყოველ დაბეჭდილ გვერდზე მეორდება (Excel-ის Print_Titles)
    private static DefinedNames PrintTitles(string sheetName, uint headerRowIndex)
    {
        var definedNames = new DefinedNames();
        definedNames.AppendChild(new DefinedName($"'{sheetName}'!${headerRowIndex}:${headerRowIndex}")
        {
            Name = "_xlnm.Print_Titles", LocalSheetId = 0
        });
        return definedNames;
    }

    //ხელით დასმული გვერდის წყვეტები მითითებული სტრიქონების წინ: Excel-ის brk-ის Id წინა სტრიქონის ნომერია
    private static RowBreaks RowBreaks(List<uint> newPageRows)
    {
        var rowBreaks = new RowBreaks { Count = (uint)newPageRows.Count, ManualBreakCount = (uint)newPageRows.Count };
        foreach (uint rowIndex in newPageRows)
        {
            rowBreaks.AppendChild(new Break { Id = rowIndex - 1, Max = MaxColumnIndex, ManualPageBreak = true });
        }

        return rowBreaks;
    }

    private static Row TextRow(uint rowIndex, string text, uint style)
    {
        var row = new Row { RowIndex = rowIndex };
        row.AppendChild(TextCell($"A{rowIndex}", text, style));
        return row;
    }

    //სვეტების სიგანეზე გაერთიანებული (MergeCells) და გადატანილი ტექსტი; გაერთიანებული უჯრის სიმაღლეს Excel თვითონ
    //არ ასწორებს, ამიტომ რამდენიმე ხაზის სიმაღლე აქ ითვლება
    private static Row WrappedTextRow(uint rowIndex, string text, uint style, double width)
    {
        Row row = TextRow(rowIndex, text, style);
        int lines = WrappedLines(text, width * WrapWidthShare);
        if (lines > 1)
        {
            row.Height = lines * LineHeight;
            row.CustomHeight = true;
        }

        return row;
    }

    //ტექსტის ხაზები width სიგანის უჯრაში, Excel-ის გადატანასავით: სიტყვები ხაზზე სანამ ეტევა, ხაზზე გრძელი სიტყვა
    //კი ხაზებად იჭრება
    private static int WrappedLines(string text, double width)
    {
        int lines = 1;
        double lineWidth = 0;
        foreach (string word in text.Split(' '))
        {
            double wordWidth = TextWidth(word);
            if (lineWidth > 0 && lineWidth + 1 + wordWidth > width)
            {
                lines++;
                lineWidth = wordWidth;
            }
            else
            {
                lineWidth += lineWidth > 0 ? 1 + wordWidth : wordWidth;
            }

            while (lineWidth > width)
            {
                lines++;
                lineWidth -= width;
            }
        }

        return lines;
    }

    //ტექსტის სიგანე Excel-ის სვეტის ერთეულებში
    private static double TextWidth(string text)
    {
        return text.Sum(c => c is >= '\u10A0' and <= '\u10FF' ? GeorgianLetterWidth : 1);
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
            string text => TextCell(reference, text, TextStyle(bold)),
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
        return (uint)kind * 2 + 1 + (bold ? 1u : 0u);
    }

    private static uint TextStyle(bool bold)
    {
        return WrappedTextStyle + (bold ? 1u : 0u);
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

    //სიგანე უჯრების ტექსტის სიგანით (სათაურის, პარამეტრებისა და სექციის სათაურის სტრიქონის გარეშე); უფრო გრძელი
    //ტექსტი უჯრაში გადაიტანება
    private static List<double> ColumnWidths(ReportResponse report)
    {
        List<List<object?>> rows =
        [
            [.. report.Columns.Select(c => (object?)c.Caption)],
            .. report.Sections.SelectMany(s => s.Footer is null ? s.Rows : [.. s.Rows, s.Footer]),
            .. report.FooterRows
        ];
        int columnCount = rows.Max(r => r.Count);

        List<double> widths = [];
        for (int i = 0; i < columnCount; i++)
        {
            int index = i;
            double length = rows.Where(r => index < r.Count).Max(r => DisplayLength(r[index]));
            widths.Add(Math.Clamp(length + 2, MinColumnWidth, MaxColumnWidth));
        }

        return widths;
    }

    private static Columns Columns(List<double> widths)
    {
        var columns = new Columns();
        for (int i = 0; i < widths.Count; i++)
        {
            columns.AppendChild(new Column
            {
                Min = (uint)(i + 1),
                Max = (uint)(i + 1),
                Width = widths[i],
                CustomWidth = true
            });
        }

        return columns;
    }

    private static double DisplayLength(object? value)
    {
        return value switch
        {
            null => 0,
            string text => TextWidth(text),
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

        //0: ნაგულისხმევი; უჯრები მას არ იყენებენ, რადგან მის სწორებას (გადატანა, ზედა კიდე) Excel არ კითხულობს
        var cellFormats = new CellFormats();
        cellFormats.AppendChild(CellFormat(0, 0, null));
        //რამდენიმე ხაზიან სტრიქონში (გადატანილი ტექსტი) ყველა უჯრა ზედა კიდეზეა
        foreach (Kind kind in Enum.GetValues<Kind>())
        {
            foreach (bool bold in BoldVariants)
            {
                cellFormats.AppendChild(CellFormat(formatIds[(int)kind], bold ? 1u : 0u,
                    new Alignment { Vertical = VerticalAlignmentValues.Top }));
            }
        }

        cellFormats.AppendChild(CellFormat(0, 2, null));

        //ტექსტი უჯრაში გადაიტანება და სვეტს გარეთ არ გადადის (გაუერთიანებელი უჯრის სტრიქონის სიმაღლეს Excel ფაილის
        //გახსნისას თვითონ ასწორებს)
        foreach (bool bold in BoldVariants)
        {
            cellFormats.AppendChild(CellFormat(0, bold ? 1u : 0u,
                new Alignment { Vertical = VerticalAlignmentValues.Top, WrapText = true }));
        }

        //სექციის სათაური ქვედა კიდეზეა: სიმაღლის მარაგი მის ზემოთ რჩება და წინა სექციას გამოყოფს
        cellFormats.AppendChild(CellFormat(0, 1, new Alignment { WrapText = true }));

        var stylesheet = new Stylesheet();
        stylesheet.AppendChild(numberingFormats);
        stylesheet.AppendChild(fonts);
        stylesheet.AppendChild(fills);
        stylesheet.AppendChild(borders);
        stylesheet.AppendChild(cellFormats);
        return stylesheet;
    }

    private static CellFormat CellFormat(uint numberFormatId, uint fontId, Alignment? alignment)
    {
        var cellFormat = new CellFormat
        {
            NumberFormatId = numberFormatId,
            FontId = fontId,
            FillId = 0,
            BorderId = 0,
            ApplyNumberFormat = numberFormatId != 0,
            ApplyFont = fontId != 0
        };
        if (alignment is not null)
        {
            cellFormat.ApplyAlignment = true;
            cellFormat.AppendChild(alignment);
        }

        return cellFormat;
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
