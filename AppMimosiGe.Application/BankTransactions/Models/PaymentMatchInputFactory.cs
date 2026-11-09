using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     PaymentMatcher-ის მონაცემები მიმდინარე წლის კონტრაქტებიდან. მოსწავლეს წელიწადში ერთი კონტრაქტი აქვს (D148); თუ
///     ძველ მონაცემებში მეტია, გადახდა ბოლოზე (უდიდესი ID) იწერება
/// </summary>
public static class PaymentMatchInputFactory
{
    public static PaymentMatchInput Create(IReadOnlyCollection<MatchContractData> currentContracts,
        IReadOnlyDictionary<string, int> studentByContractNumber, IReadOnlyList<LearnedPayer> learnedPayers)
    {
        List<MatchStudent> students =
        [
            .. currentContracts.GroupBy(c => c.StudentHumanId).Select(g => g.MaxBy(c => c.StudentContractId)!)
                .Select(c => new MatchStudent(c.StudentHumanId, c.StudentFirstName, c.StudentLastName))
                .OrderBy(s => s.StudentHumanId)
        ];
        List<MatchPayer> payers =
        [
            .. currentContracts.GroupBy(c => c.PayerHumanId).OrderBy(g => g.Key).Select(g =>
                new MatchPayer(g.Key, g.First().PayerPersonalId, g.First().PayerFirstName, g.First().PayerLastName,
                    [.. g.Select(c => c.StudentHumanId).Distinct().Order()]))
        ];
        return new PaymentMatchInput(students, studentByContractNumber, payers, learnedPayers);
    }

    //მოსწავლე → მიმდინარე წლის კონტრაქტი
    public static Dictionary<int, int> CurrentContractByStudent(IEnumerable<MatchContractData> currentContracts)
    {
        return currentContracts.GroupBy(c => c.StudentHumanId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.StudentContractId));
    }
}
