using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     მოსწავლის ანგარიში (ნაწილი 20): ბალანსი მოსწავლის ყველა კონტრაქტის (ყველა სასწავლო წლის) ოპერაციების ჯამია, ამიტომ
///     წინა წლის ნაშთი ახალ წელში გადატანის გარეშე ჩანს. studentByContract: კონტრაქტის ID → მოსწავლის (Humans) ID;
///     კონტრაქტი, რომელიც მასში არ არის, თავად არის ანგარიში (უარყოფითი გასაღებით, რომ მოსწავლის ID-ს არ დაემთხვეს)
/// </summary>
public static class StudentAccounts
{
    public static int AccountOf(IReadOnlyDictionary<int, int> studentByContract, int studentContractId)
    {
        return studentByContract.TryGetValue(studentContractId, out int studentHumanId)
            ? studentHumanId
            : -studentContractId;
    }

    //ელემენტები ანგარიშებით, თავდაპირველი რიგის შენარჩუნებით (ოპერაციების რიგი NextPayDate-ს სჭირდება)
    public static ILookup<int, T> ByAccount<T>(IEnumerable<T> items, IReadOnlyDictionary<int, int> studentByContract,
        Func<T, int> studentContractId)
    {
        return items.ToLookup(item => AccountOf(studentByContract, studentContractId(item)));
    }
}
