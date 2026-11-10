using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     ბანკის ჩარიცხვების რეპორტები (TBC_INTEGRATION.md, D164)
/// </summary>
public static class BankTransactionReports
{
    //r40UnallocatedBankTransactions: ჩარიცხვები, რომელთა განაწილებული ჯამი ჩარიცხულზე ნაკლებია (სულ გაუნაწილებელიც),
    //„არ არის მოსწავლის გადახდის“ გარდა; ნაშთი = ჩარიცხული − განაწილებული. რიგი: თარიღი (ძველი პირველი), ID. ბოლოს
    //ჩარიცხვების რაოდენობა და თანხების ჯამები. ბანკი: რომელ ინტეგრირებულ ანგარიშზე შემოვიდა (D173)
    public static ReportTable Unallocated(IEnumerable<BankTransactionRowResponse> rows)
    {
        List<BankTransactionRowResponse> ordered =
        [
            .. rows.Where(r => !r.Ignored && r.AllocatedAmount < r.Amount).OrderBy(r => r.TransactionDate)
                .ThenBy(r => r.Id)
        ];
        List<ReportColumnResponse> columns =
        [
            new("transactionDate", "თარიღი", ReportColumnTypes.Date),
            new("bankName", "ბანკი", ReportColumnTypes.Text),
            new("partnerName", "გადამხდელი", ReportColumnTypes.Text),
            new("purpose", "დანიშნულება", ReportColumnTypes.Text),
            new("amount", "ჩარიცხული", ReportColumnTypes.Number),
            new("allocated", "განაწილებული", ReportColumnTypes.Number),
            new("remainder", "ნაშთი", ReportColumnTypes.Number)
        ];
        List<List<object?>> tableRows =
        [
            .. ordered.Select(r => new List<object?>
            {
                r.TransactionDate.Date, r.BankName, r.PartnerName, r.Purpose, r.Amount, r.AllocatedAmount,
                r.Amount - r.AllocatedAmount
            })
        ];
        List<object?> footer =
        [
            ReportFooters.TotalCaption, ordered.Count, null, null, ordered.Sum(r => r.Amount),
            ordered.Sum(r => r.AllocatedAmount), ordered.Sum(r => r.Amount - r.AllocatedAmount)
        ];
        return ReportTable.Flat(columns, tableRows, [footer]);
    }
}
