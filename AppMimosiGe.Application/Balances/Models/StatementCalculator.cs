using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Balances.Models;

public static class StatementCalculator
{
    /// <summary>
    ///     ამონაწერი (Access-ის FrmChargesAndPayments). operations: ერთი კონტრაქტის ან ყველა კონტრაქტის ყველა ოპერაცია
    ///     BalanceOperations.Build-ის რიგით. სტრიქონები: ოპერაციები dateFrom-იდან dateTo-მდე (დღეები, ორივე ჩათვლით;
    ///     ცარიელი საზღვარი შეზღუდვის არქონაა). RunningTotal: ნაშთი ოპერაციის შემდეგ, ყველა ოპერაციის ჯამი თავიდან ამ
    ///     რიგით, ფილტრის "თარიღიდან"-ის მიუხედავად (ერთი დროის ოპერაციებზე დაგროვებით; Access-ში მათ ერთი ნაშთი ჰქონდათ).
    ///     საწყისი ნაშთი: ოპერაციები dateFrom-მდე (მის გარეშე 0). საბოლოო ნაშთი: dateTo ჩათვლით, მის გარეშე ყველა ოპერაცია
    ///     (Access-ის GetMaxEndDate ორი ბოლო თარიღიდან პატარას იღებდა და შეცდომით წყდებოდა)
    /// </summary>
    public static Statement Build(IReadOnlyList<BalanceOperation> operations, DateTime? dateFrom, DateTime? dateTo)
    {
        DateTime? fromStart = dateFrom?.Date;
        //"თარიღამდე" დღის ბოლომდე (Access-ის ნაგულისხმევი 23:59:59-ია)
        DateTime? toEnd = dateTo?.Date.AddDays(1);

        decimal total = 0m;
        decimal startBalance = 0m;
        decimal endBalance = 0m;
        List<StatementRow> rows = [];
        foreach (BalanceOperation operation in operations)
        {
            total += operation.Amount;
            bool beforeStart = operation.OperationDate < fromStart;
            bool afterEnd = operation.OperationDate >= toEnd;
            if (beforeStart)
            {
                startBalance += operation.Amount;
            }

            if (!afterEnd)
            {
                endBalance += operation.Amount;
            }

            if (!beforeStart && !afterEnd)
            {
                rows.Add(new StatementRow(operation, BalanceOperations.RoundMoney(total)));
            }
        }

        return new Statement(BalanceOperations.RoundMoney(startBalance), BalanceOperations.RoundMoney(endBalance),
            rows);
    }
}
