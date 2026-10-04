using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     გაცდენები: მოსწავლისა და საგნის მიხედვით (r14) და ზედიზედ, ბოლო დასწრების შემდეგ (r17)
/// </summary>
public static class AbsenceReports
{
    private const int PhoneLength = 9;

    //r14Missings (vR14Missings): გაცდენების რაოდენობა მოსწავლის კონტრაქტით და საგნით; დათვლა (Present = false,
    //სტატუსი "გაუქმდა"-ს გარდა, პერიოდის მხოლოდ დაწყებული გაკვეთილები, D121) რეპოზიტორიაშია. რიგი: რაოდენობა
    //კლებით (Access-ის რეპორტის OrderBy), შემდეგ მოსწავლე, ნომერი და საგანი (ამ ველებით ერთნაირი სტრიქონები ერთნაირად
    //იბეჭდება)
    public static ReportTable Missings(IEnumerable<AbsenceCountRow> counts)
    {
        return ReportTable.Flat([
            Text("studentName", "გვარი და სახელი"), Text("contractNumber", "კონტრაქტი"), Text("courseName", "საგანი"),
            new ReportColumnResponse("missings", "გაცდენების რაოდენობა", ReportColumnTypes.WholeNumber)
        ], [
            .. counts.OrderByDescending(c => c.Count).ThenBy(c => c.Student.FullName, StringComparer.Ordinal)
                .ThenBy(c => c.Student.ContractNumber, StringComparer.Ordinal)
                .ThenBy(c => c.CourseName, StringComparer.Ordinal).Select(c =>
                    new List<object?> { c.Student.FullName, c.Student.ContractNumber, c.CourseName, c.Count })
        ]);
    }

    //r17MissingsInRow (vR17MissingsInRow, vStudentLastPresentDate): კონტრაქტის გაცდენები ბოლო დასწრების შემდეგ
    //(Access-ის MaxOfLessonDT < LessonDT); ვისაც დასწრება არ აქვს, ყველა გაცდენა ეთვლება (D122). სიაშია ერთზე მეტი.
    //რიგი: რაოდენობა კლებით (Access-ის ORDER BY), შემდეგ მოსწავლე. ბოლოს Access-ის რეპორტის ჩანაწერების რაოდენობა
    public static ReportTable MissingsInRow(MissingsInRowData data)
    {
        List<ReportColumnResponse> columns =
        [
            Text("student", "მოსწავლე"), Text("studentPhone", "ტელ. ნომერი"), Text("payer", "გადამხდელი"),
            Text("payerPhone", "ტელ. ნომერი"),
            new("missingsInRow", "ზედიზედ გაცდენა", ReportColumnTypes.WholeNumber)
        ];
        List<List<object?>> rows =
        [
            .. data.Absences.GroupBy(a => a.StudentContractId).Select(absences => (ContractId: absences.Key,
                    Count: absences.Count(a => IsAfterLastPresence(a, data.LastPresences))))
                .Where(c => c.Count > 1).Select(c => (c.ContractId, c.Count, Contact: data.Students[c.ContractId]))
                .OrderByDescending(c => c.Count).ThenBy(c => c.Contact.Student.NameWithNumber, StringComparer.Ordinal)
                .ThenBy(c => c.ContractId).Select(c => new List<object?>
                {
                    c.Contact.Student.NameWithNumber,
                    FormatPhone(c.Contact.StudentPhone),
                    c.Contact.PayerName,
                    FormatPhone(c.Contact.PayerPhone),
                    c.Count
                })
        ];
        return ReportTable.Flat(columns, rows, [ReportFooters.Count(columns, rows.Count)]);
    }

    //ტელეფონი Access-ის ფორმატით "000-00-00-00" (9 ციფრი, როგორც დეპოზიტების გვერდზე); სხვა მნიშვნელობა უცვლელად
    public static string? FormatPhone(string? phone)
    {
        return phone is { Length: PhoneLength } && phone.All(char.IsAsciiDigit)
            ? $"{phone[..3]}-{phone[3..5]}-{phone[5..7]}-{phone[7..]}"
            : phone;
    }

    private static bool IsAfterLastPresence(StudentAbsence absence, IReadOnlyDictionary<int, DateTime> lastPresences)
    {
        return !lastPresences.TryGetValue(absence.StudentContractId, out DateTime lastPresence) ||
               lastPresence < absence.LessonDt;
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }
}
