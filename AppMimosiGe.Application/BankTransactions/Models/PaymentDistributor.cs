using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <param name="StudentHumanId">მოსწავლე</param>
/// <param name="Balance">მოსწავლის ანგარიშის ბალანსი ჩარიცხვის თარიღისთვის (უარყოფითი = ვალი, დადებითი = დეპოზიტი)</param>
/// <param name="MustPayToEnd">წლის ბოლომდე გადასახდელი (დეპოზიტების სიის MustPayToEnd; ვალის ჩათვლით)</param>
public sealed record DistributionAccount(int StudentHumanId, decimal Balance, decimal MustPayToEnd);

public sealed record DistributionShare(int StudentHumanId, decimal Amount);

/// <summary>
///     ჩარიცხვის განაწილება რამდენიმე მოსწავლეზე (D163): თანხა &lt; ვალების ჯამი → ვალების პროპორციულად; თანხა = ვალების
///     ჯამი → ყველას ვალი იფარება; თანხა &gt; ვალების ჯამი → ჯერ ყველა ვალი, ზედმეტი კი წლის ბოლომდე დარჩენილი
///     გადასახდელის (MustPayToEnd − ვალი) პროპორციულად, ხოლო თუ ყველას 0 რჩება, თანაბრად. პროპორციული ნაწილი მთელ
///     ლარებად ნაწილდება (უდიდესი ნაშთის მეთოდით, ასე წილი უარყოფითი ვერ იქნება), დარჩენილი თეთრები კი უდიდეს წილს
///     ემატება, ამიტომ წილების ჯამი ზუსტად ჩარიცხვის თანხაა. შედეგი ზრდადი ID-ებითაა, ყველა მოსწავლე (0-იანი წილიც).
///     თანხა დადებითია და თეთრებამდეა (ბანკის ჩარიცხვა)
/// </summary>
public static class PaymentDistributor
{
    public static List<DistributionShare> Distribute(decimal amount, IEnumerable<DistributionAccount> accounts)
    {
        List<DistributionAccount> ordered = [.. accounts.OrderBy(a => a.StudentHumanId)];
        if (ordered.Count == 0)
        {
            return [];
        }

        //ბალანსი 4 ათწილადით ითვლება, გადახდას კი მხოლოდ თეთრები აქვს (2 ათწილადი)
        decimal[] debts = [.. ordered.Select(a => Math.Round(Math.Max(0m, -a.Balance), 2, MidpointRounding.AwayFromZero))];
        decimal totalDebt = debts.Sum();

        decimal[] shares;
        if (amount < totalDebt)
        {
            shares = Apportion(amount, debts);
        }
        else
        {
            decimal[] remaining = [.. ordered.Select((a, i) => Math.Max(0m, a.MustPayToEnd - debts[i]))];
            decimal[] weights = remaining.Sum() > 0m ? remaining : [.. ordered.Select(_ => 1m)];
            decimal[] excess = Apportion(amount - totalDebt, weights);
            shares = [.. debts.Select((debt, i) => debt + excess[i])];
        }

        //თეთრები (და ყველა მცირე სხვაობა) უდიდეს წილს, თანაბრობისას პირველს
        int largest = Array.IndexOf(shares, shares.Max());
        shares[largest] += amount - shares.Sum();

        return [.. ordered.Select((a, i) => new DistributionShare(a.StudentHumanId, shares[i]))];
    }

    //total-ის მთელი ლარები წონების პროპორციულად: ჯერ ქვედა მთელი, დარჩენილი ლარები უდიდეს წილად ნაშთებს
    private static decimal[] Apportion(decimal total, decimal[] weights)
    {
        decimal wholeLari = Math.Floor(total);
        decimal weightSum = weights.Sum();
        decimal[] exact = [.. weights.Select(w => weightSum == 0m ? 0m : wholeLari * w / weightSum)];
        decimal[] result = [.. exact.Select(Math.Floor)];
        int leftover = (int)(wholeLari - result.Sum());
        foreach (int i in Enumerable.Range(0, exact.Length).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i)
                     .Take(leftover))
        {
            result[i] += 1m;
        }

        return result;
    }
}
