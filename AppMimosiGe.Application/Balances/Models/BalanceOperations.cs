using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     Access-ის vFrmChargesAndPayments: დარიცხვები და გადახდები ერთ სიაში. დარიცხვა არსად ინახება, ყოველთვის ითვლება
/// </summary>
public static class BalanceOperations
{
    //ფულის სიზუსტე (Currency / money): ნაშთები და ჯამები 4 ათწილადამდე მრგვალდება
    private const int MoneyDecimals = 4;

    /// <summary>
    ///     დარიცხვის თანხა: IIf(FourWeekHours = 0, 0, -FourWeekFee / FourWeekHours) * HoursCount. Access-ის ფორმულის
    ///     + Nz(PrepaidAmount) D9-ით წაიშალა (ყველა მნიშვნელობა 0 იყო). Single საათები Access-ში Double-ად ზუსტად გადაიქცევა,
    ///     ამიტომ აქაც double-ის გავლით
    /// </summary>
    public static decimal ChargeAmount(decimal fourWeekFee, float fourWeekHours, float hoursCount)
    {
        decimal hours = (decimal)(double)fourWeekHours;
        return hours == 0m ? 0m : -fourWeekFee / hours * (decimal)(double)hoursCount;
    }

    /// <summary>
    ///     ოპერაციების დეტერმინისტული რიგი: თარიღი (და დრო), ერთ დროს ჯერ გადახდა, შემდეგ ID. Access-ში ერთი დროის
    ///     ოპერაციების რიგი განუსაზღვრელი იყო ("ORDER BY OperationDate")
    /// </summary>
    public static List<BalanceOperation> Build(IEnumerable<ChargeData> charges, IEnumerable<PaymentData> payments)
    {
        return
        [
            .. charges
                .Select(c => new BalanceOperation(false, c.LessonByStudentId, c.StudentContractId, c.LessonDt,
                    c.CourseName, ChargeAmount(c.FourWeekFee, c.FourWeekHours, c.HoursCount)))
                .Concat(payments.Select(p =>
                    new BalanceOperation(true, p.Id, p.StudentContractId, p.PayDate, p.Document, p.Amount)))
                .OrderBy(o => o.OperationDate).ThenByDescending(o => o.IsPayment).ThenBy(o => o.Id)
        ];
    }

    public static decimal RoundMoney(decimal amount)
    {
        return decimal.Round(amount, MoneyDecimals);
    }
}
