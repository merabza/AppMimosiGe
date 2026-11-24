using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 2-ის ფაილი (D195): სახელი GroupOrders_yyyyMMddHHmmss.json (ჩაწერის ადგილობრივი დრო) და შიგთავსი, JSON
///     UTF-8-ში, BOM-ის გარეშე, ქართული ტექსტი escape-ის გარეშე
/// </summary>
public sealed partial record GroupOrdersExportFile(string FileName, byte[] Content)
{
    public const string ContentType = "application/json";

    private const int RegexTimeoutMilliseconds = 1000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static GroupOrdersExportFile Create(GroupOrdersExport export)
    {
        return new GroupOrdersExportFile(NameFor(export.ExportedAt),
            JsonSerializer.SerializeToUtf8Bytes(export, SerializerOptions));
    }

    public static string NameFor(DateTime exportedAt)
    {
        return string.Create(CultureInfo.InvariantCulture, $"GroupOrders_{exportedAt:yyyyMMddHHmmss}.json");
    }

    /// <summary>
    ///     სახელი ვიზარდი 2-ის ფაილისაა: გადმოწერა მხოლოდ ასეთ სახელს იღებს, რომ ExportFolder-ის გარეთ სხვა ფაილი ვერ
    ///     წაიკითხოს
    /// </summary>
    public static bool IsFileName(string fileName)
    {
        return FileNameRegex().IsMatch(fileName);
    }

    [GeneratedRegex(@"^GroupOrders_\d{14}\.json$", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex FileNameRegex();
}
