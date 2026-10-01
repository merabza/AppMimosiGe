using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     კონტრაქტის შემდეგი გადახდის თარიღი (Access-ის RecountStudentNextPayDate)
/// </summary>
public static class NextPayDateCalculator
{
    //VBA-ის Currency: ყოველი მიმატების შემდეგ 4 ათწილადამდე, ბანკირის დამრგვალებით
    private const int CurrencyDecimals = 4;

    /// <summary>
    ///     ოპერაციები (ერთი კონტრაქტის, BalanceOperations.Build-ის რიგით) თანმიმდევრულად ემატება ნაშთს. ნაშთი პირველად
    ///     რომ გახდება უარყოფითი, ეს თარიღი შემდეგი გადახდის თარიღია; თუ ნაშთი მერე ისევ ≥ 0 გახდა, თარიღი უქმდება და
    ///     შემდეგი უარყოფითი ნაშთი ახალ თარიღს იძლევა. უარყოფითი ნაშთისას ბოლო გადახდის თარიღის შემდეგ ოპერაციაზე ძებნა
    ///     წყდება. null: ვალი არ არის. ოპერაციებში მომავლის დარიცხვებიცაა (გენერირებული გაკვეთილები)
    /// </summary>
    public static DateTime? Calculate(IEnumerable<BalanceOperation> operations, DateTime lastPayDate)
    {
        decimal amount = 0m;
        DateTime nextPayDate = default;
        bool counted = false;
        foreach (BalanceOperation operation in operations)
        {
            amount = decimal.Round(amount + operation.Amount, CurrencyDecimals, MidpointRounding.ToEven);
            if (!counted && amount < 0m)
            {
                nextPayDate = operation.OperationDate;
                counted = true;
            }

            if (counted && amount >= 0m)
            {
                counted = false;
            }

            if (counted && operation.OperationDate > lastPayDate)
            {
                break;
            }
        }

        return counted ? nextPayDate : null;
    }

    /// <summary>
    ///     კონტრაქტის ბოლო გადახდის თარიღი, გადახდის გარეშე დღეს (Access: Nz(Max(PayDate), Date))
    /// </summary>
    public static DateTime LastPayDate(IEnumerable<BalanceOperation> operations, DateTime today)
    {
        return operations.Where(o => o.IsPayment).Select(o => (DateTime?)o.OperationDate).Max() ?? today.Date;
    }
}
