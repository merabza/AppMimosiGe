using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     ბანკისა და შემოსავლების სამსახურის CSV ფაილები, ბაიტ-ბაიტ Access-ის ფაილების მსგავსად (D106): UTF-8 BOM-ით
///     (ADODB.Stream), სტრიქონი CRLF-ით იწყება, ბოლოს ახალი ხაზი არ არის. რიცხვები VBA-ს Str() და იმპლიციტური
///     გარდაქმნისაა: Str() დადებით რიცხვს წინ ჰარს უმატებს, თანხა ათწილადი წერტილით და ზედმეტი ნულების გარეშე
///     (Access-ის კომპიუტერის en-US რეგიონით)
/// </summary>
public static class SalaryFilesGenerator
{
    public const string TransferFileHeader = "DOCNUM,ACCIBANTO,BENEFNAME,BENEFTAXCODE,AMOUNT,DESCR,ADDDESCR";

    //Access-ის GeoPhrases gpId=4 (ცხრილი D7-ით წაიშალა)
    public const string DeclarationFileHeader =
        "\"საიდენტიფიკაციო ნომერი (პირადი ნომერი)\",\"თანხის მიმღების სახელი/სამართლებრივი ფორმა\"," +
        "\"თანხის მიმღების გვარი/დასახელება\",\"მისამართი\",\"პირის რეზიდენტობა (ქვეყანა)\"," +
        "\"შემოსავლის მიმღებ პირთა კატეგორია\",\"განაცემის სახე\",\"განაცემი თანხა (ლარი)\",\"სხვა შეღავათები\"," +
        "\"გაცემის თარიღი\",\"წყაროსთან დასაკავებელი გადასახადის განაკვეთი\"," +
        "\"საერთაშორისო ხელშეკრულების საფუძველზე გათავისუფლებას ან შემცირებას დაქვემდებარებული გადასახადის თანხა\"," +
        "\"ორმაგი დაბეგვრის თავიდან აცილების შესახებ ხელშეკრულების საფუძველზე ჩათვლას დაქვემდებარებული, " +
        "უცხო ქვეყანაში გადახდილი გადასახადის თანხა\"";

    private const string NewLine = "\r\n";

    public static string TransferFileName(DateTime transferDate)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"salary_{transferDate.Year}_{transferDate.Month}_{transferDate.Day}.csv");
    }

    public static string DeclarationFileName(DateTime month)
    {
        return string.Create(CultureInfo.InvariantCulture, $"TaxDepDeclaration_{month.Year}_{month.Month}.csv");
    }

    //Access-ის ExportTransferFile. სტრიქონების რიგი Access-ის GROUP BY-ისაა (ანგარიში, სახელი, პირადი ნომერი,
    //თანხა, შინაარსი, თვე), მაგრამ ერთნაირი სტრიქონები აღარ ერთიანდება და შინაარსის გარეშე სტრიქონი აღარ იკარგება
    //(D107). ყველა ამ ველით ერთნაირი სტრიქონები ფაილში ერთნაირად იწერება, ამიტომ მათი რიგი არაფერს ცვლის
    public static byte[] TransferFile(IEnumerable<TransferFileRow> rows)
    {
        var lines = rows.Select(x => new
        {
            BankAccount = x.BankAccount ?? string.Empty,
            BenefName = (x.LegalName ?? x.FirstName) + " " + x.LastName,
            x.PersonalId,
            x.AmountNet,
            Descr = x.Description ?? x.QuoteTypeName ?? string.Empty,
            AddDescr = x.MonthName + " " + VbaStr(x.Year)
        }).OrderBy(x => x.BankAccount, StringComparer.Ordinal).ThenBy(x => x.BenefName, StringComparer.Ordinal)
            .ThenBy(x => x.PersonalId, StringComparer.Ordinal).ThenBy(x => x.AmountNet)
            .ThenBy(x => x.Descr, StringComparer.Ordinal).ThenBy(x => x.AddDescr, StringComparer.Ordinal);

        var text = new StringBuilder(TransferFileHeader);
        int docNum = 1;
        foreach (var line in lines)
        {
            text.Append(NewLine).Append(VbaStr(docNum)).Append(',').Append(line.BankAccount).Append(',')
                .Append(line.BenefName).Append(',').Append(CareCsv(line.PersonalId, true)).Append(',')
                .Append(VbaNumber(line.AmountNet)).Append(',').Append(line.Descr).Append(',').Append(line.AddDescr);
            docNum++;
        }

        return Utf8WithBom(text.ToString());
    }

    //Access-ის ExportTaxDepDeclarationFile. Access-ის query-ს რიგი არ ჰქონდა, აქ სტრიქონის იდენტიფიკატორით;
    //ცარიელი მისამართი და განაცემის სახე ცარიელ ველად იწერება (Access-ში შეცდომით წყდებოდა, D107)
    public static byte[] DeclarationFile(IEnumerable<DeclarationFileRow> rows)
    {
        var text = new StringBuilder(DeclarationFileHeader);
        foreach (DeclarationFileRow row in rows.OrderBy(x => x.SaId))
        {
            text.Append(NewLine).Append(CareCsv(FormatPersonalId(row.PersonalId), true)).Append(',')
                .Append(CareCsv(row.LegalName ?? row.FirstName)).Append(',').Append(CareCsv(row.LastName))
                .Append(',').Append(CareCsv(row.LegalAddress ?? string.Empty)).Append(',')
                .Append(CareCsv(row.CountryCode)).Append(",4,")
                .Append(CareCsv(row.RsQuoteTypeId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty))
                .Append(',').Append(CareCsv(VbaNumber(row.AmountGross))).Append(",0,")
                .Append(CareCsv(row.TransferDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)))
                .Append(",20,0,0");
        }

        return Utf8WithBom(text.ToString());
    }

    //Access-ის CareCSV: ბრჭყალებში, თუ ტექსტში მძიმეა ან ბრჭყალი იძულებითია (შიგნით ბრჭყალი არ ორმაგდება)
    public static string CareCsv(string value, bool forceQuotes = false)
    {
        return forceQuotes || value.Contains(',', StringComparison.Ordinal) ? "\"" + value + "\"" : value;
    }

    //VBA-ს Str(): დადებით რიცხვს (და ნულს) წინ ჰარი აქვს
    public static string VbaStr(int value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        return value >= 0 ? " " + text : text;
    }

    //VBA-ს Currency → String (en-US): ათწილადი წერტილით, ზედმეტი ნულების გარეშე, 4 ათწილადამდე
    public static string VbaNumber(decimal value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    //Format(PersonalID, "00000000000"): ციფრებისგან შემდგარი ნომერი 11 ციფრამდე წინა ნულებით ივსება, სხვა ტექსტი
    //უცვლელია
    public static string FormatPersonalId(string personalId)
    {
        return personalId.Length > 0 && personalId.All(char.IsAsciiDigit)
            ? personalId.TrimStart('0').PadLeft(11, '0')
            : personalId;
    }

    private static byte[] Utf8WithBom(string text)
    {
        var encoding = new UTF8Encoding(true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}
