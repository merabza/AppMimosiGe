using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     ფინანსების რეპორტები: შავი სია (r15) და მასწავლებლების გამომუშავებული ხელფასი ჯგუფების მიხედვით (r25)
/// </summary>
public static class FinanceReports
{
    private const int MoneyDecimals = 4;

    //r15BlackList (vR15BlackList): ადამიანები, რომელთა კონტრაქტზე (მოსწავლედ ან გადამხდელად) გადახდა „უიმედო ვალის"
    //ანგარიშით არის გატარებული, და ამ გადახდების ჯამი. გადახდა მოსწავლესაც ეთვლება და გადამხდელსაც (Access-ის join
    //"მოსწავლე Or გადამხდელი"), ადამიანს კი ერთხელ, თუ ის ორივეა. რიგი: გვარი, სახელი, პირადი ნომერი (Access-ის
    //GROUP BY; პირადი ნომერი უნიკალურია). ბოლოს ადამიანების რაოდენობა (Access-ის =Count(*))
    public static ReportTable BlackList(BlackListData data)
    {
        List<ReportColumnResponse> columns =
        [
            Text("lastName", "გვარი"), Text("firstName", "სახელი"), Text("personalId", "პირადი ნომერი"),
            Number("debt", "დავალიანება")
        ];
        List<List<object?>> rows =
        [
            .. data.Payments.SelectMany(p => new[] { p.StudentHumanId, p.PayerHumanId }.Distinct()
                    .Select(humanId => (HumanId: humanId, p.Amount))).GroupBy(p => p.HumanId)
                .Select(human => (Debtor: data.Humans[human.Key], Debt: human.Sum(p => p.Amount)))
                .OrderBy(h => h.Debtor.LastName, StringComparer.Ordinal)
                .ThenBy(h => h.Debtor.FirstName, StringComparer.Ordinal)
                .ThenBy(h => h.Debtor.PersonalId, StringComparer.Ordinal).Select(h => new List<object?>
                {
                    h.Debtor.LastName, h.Debtor.FirstName, h.Debtor.PersonalId, h.Debt
                })
        ];
        return ReportTable.Flat(columns, rows, [ReportFooters.Count(columns, rows.Count)]);
    }

    //r25TecherSalaryByGroups (vR25TecherSalaryByGroups): ხელფასის დეტალები ჯგუფებით (ნაწილი 15). სექცია = თვე და
    //მასწავლებელი (Access-ის თვისა და მასწავლებლის სათაურები); სექციის ჯამი: საათები, საშუალო ფასი (ღირებულება /
    //საათები; 0 საათზე ცარიელი, Access-ში #Div/0!) და ღირებულება (Access-ის მასწავლებლის სათაურის ჯამები).
    //მასწავლებელი Access-ის "გვარი სახელი" (სახელი იურიდიული, თუ აქვს) და კონტრაქტის ნომერი. რიგი: თვე, მასწავლებელი;
    //ჯგუფი
    public static ReportTable TeacherSalaryByGroups(IEnumerable<SalaryDetailRow> details,
        IReadOnlyDictionary<int, string> monthNames)
    {
        return new ReportTable(
        [
            Text("groupCode", "ჯგუფი"), Text("course", "საგანი"), Number("hours", "საათები"), Number("hourCost", "ფასი"),
            Number("amount", "ღირებულება")
        ], [
            .. details.GroupBy(d => (d.MonthDate, d.TeacherContractId)).Select(section => section.ToList())
                .OrderBy(section => section[0].MonthDate)
                .ThenBy(section => TeacherText(section[0]), StringComparer.Ordinal).Select(section =>
                {
                    decimal hours = section.Sum(d => (decimal)d.HoursCount);
                    decimal amount = section.Sum(d => d.Amount);
                    return new ReportSectionResponse(
                        $"{ReportMonths.Name(section[0].MonthDate, monthNames)} · მასწავლებელი: {TeacherText(section[0])}",
                        [
                            .. section.OrderBy(d => d.GroupCode, StringComparer.Ordinal).ThenBy(d => d.GroupId)
                                .ThenBy(d => d.SadId).Select(d => new List<object?>
                                {
                                    d.GroupCode, d.CourseName, d.HoursCount, d.HourCost, d.Amount
                                })
                        ],
                        [
                            ReportFooters.TotalCaption, null, hours,
                            hours == 0 ? null : Math.Round(amount / hours, MoneyDecimals), amount
                        ]);
                })
        ], []);
    }

    //"გვარი სახელი / ნომერი"; კონტრაქტის ნომერი უნიკალურია, ამიტომ მასწავლებლებს ცალსახად ალაგებს
    private static string TeacherText(SalaryDetailRow detail)
    {
        return $"{detail.TeacherLastName} {detail.TeacherName} / {detail.ContractNumber}";
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }

    private static ReportColumnResponse Number(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Number);
    }
}
