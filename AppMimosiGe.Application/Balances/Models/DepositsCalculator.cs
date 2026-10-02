using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Balances.Models;

public static class DepositsCalculator
{
    /// <summary>
    ///     Access-ის vFrmDeposites და FrmDeposites-ის ფილტრები. სიაშია კონტრაქტი, რომლის ბალანსი "მაქსიმუმზე" ნაკლებია, ან
    ///     რომელსაც სასურველ დღეზე გადასახდელი თანხა აქვს. "ფილტრი": ბალანსი ≤ მაქსიმუმი; "დარეკვის ფილტრი": ამას გარდა
    ///     გაკვეთილი აქვს და CRM-ის "უნდა გადაიხადოს" (ცარიელი = დღეს) დღეს ან უფრო ადრეა. რიგი: ფილტრის გარეშე შემდეგი
    ///     გადახდის თარიღით და შემდეგი გაკვეთილით (Access-ის ფორმის OrderBy; "ფილტრის მოხსნა" Access-ში რიგსაც შლიდა, აქ ამ
    ///     რიგს აბრუნებს), ფილტრებით შემდეგი გაკვეთილით; ცარიელი თარიღი პირველია, როგორც Access-ში
    /// </summary>
    public static DepositsResponse Build(DepositsInput input, DepositsParameters parameters)
    {
        DateTime today = parameters.Today.Date;
        //"თარიღამდე" დღის ბოლომდე (Access-ის ნაგულისხმევი დღეს + 5 დღის 23:59:59-ია)
        DateTime dateToEnd = parameters.DateTo.Date.AddDays(1);
        //დაუსრულებელი სტრიქონის სავარაუდო დასრულება: ბოლო სამუშაო თვის მომდევნო თვე (Access: DateAdd("m", 1, MaxOfMonthDate))
        DateTime? openEndDate = input.LastOperationMonth?.AddMonths(1);
        ILookup<int, BalanceOperation> operations = input.Operations.ToLookup(o => o.StudentContractId);
        ILookup<int, DepositGroupStudentData> groupStudents = input.GroupStudents.ToLookup(g => g.StudentContractId);
        Dictionary<int, DateTime> crmMustPayDates = LastCrmMustPayDates(input.CrmMustPayDates);

        List<DepositRowResponse> rows = [];
        foreach (DepositContractData contract in input.Contracts)
        {
            List<BalanceOperation> contractOperations = [.. operations[contract.StudentContractId]];
            List<DepositGroupStudentData> contractGroupStudents = [.. groupStudents[contract.StudentContractId]];

            //vStudentDeposites: ოპერაციები "თარიღამდე"; ოპერაციის გარეშე ბალანსი არ არის (null)
            decimal? balance = SumOrNull(contractOperations.Where(o => o.OperationDate < dateToEnd));

            //vStudentMustPayOnDesiredDate: გადასახდელი = -(ოპერაციები მომდევნო გადახდის თარიღის შემდეგი დღის 00:00-მდე
            //ჩათვლით)
            DesiredPayDates? desiredPayDates = contract.DesiredMonthlyPaymentDay is { } desiredDay
                ? DesiredPayDates.Calculate(today, desiredDay)
                : null;
            decimal? desiredDayAmount = desiredPayDates is null
                ? null
                : -SumOrNull(contractOperations.Where(o => o.OperationDate <= desiredPayDates.AfterNextPayDate));

            if (!(balance < parameters.Maximum || desiredDayAmount is not null))
            {
                continue;
            }

            //vStudentMustPayToEndDate: ყველა ოპერაცია (მომავლისაც) და სტრიქონების დასრულების სავარაუდო თარიღებიდან
            //ბოლო; ორივე მხოლოდ მაშინ, როცა კონტრაქტს ჯგუფის სტრიქონი აქვს (Access-ში ოპერაციაც, მაგრამ ოპერაციის
            //გარეშე კონტრაქტი სიაში ისედაც არ ხვდება). სტრიქონის გარეშე Max ცარიელ სიმრავლეზე null-ია
            DepositRowResponse row = new(contract.StudentContractId, contract.AcademicYearId, contract.StudentName,
                contract.ContractNumber, balance, contract.StudentPhone, contract.PayerName, contract.PayerPhone,
                input.NextLessonDates.TryGetValue(contract.StudentContractId, out DateTime nextLessonDate)
                    ? nextLessonDate
                    : null,
                crmMustPayDates.TryGetValue(contract.StudentContractId, out DateTime crmMustPayDate)
                    ? crmMustPayDate
                    : null, FourWeekFee(contractGroupStudents, dateToEnd), contract.DesiredMonthlyPaymentDay,
                desiredPayDates?.NextPayDate, desiredPayDates?.AfterNextPayDate, desiredDayAmount, contract.NextPayDate,
                contractGroupStudents.Count > 0 ? -SumOrNull(contractOperations) : null,
                contractGroupStudents.Max(g => g.EndDate ?? g.GroupVoidDate ?? openEndDate));

            if (MatchesFilter(row, parameters, today))
            {
                rows.Add(row);
            }
        }

        List<DepositRowResponse> ordered =
        [
            .. (parameters.Filter == EDepositsFilter.None
                ? rows.OrderBy(r => r.StopDate).ThenBy(r => r.NextLessonDate)
                : rows.OrderBy(r => r.NextLessonDate)).ThenBy(r => r.StudentName, StringComparer.Ordinal)
            .ThenBy(r => r.StudentContractId)
        ];

        //Access-ის ფორმის footer: ნაჩვენები სტრიქონების ჯამები
        return new DepositsResponse(ordered.Sum(r => r.Balance ?? 0m), ordered.Sum(r => r.FourWeekFee ?? 0m), ordered);
    }

    private static bool MatchesFilter(DepositRowResponse row, DepositsParameters parameters, DateTime today)
    {
        return parameters.Filter switch
        {
            EDepositsFilter.Filter => row.Balance <= parameters.Maximum,
            EDepositsFilter.Call => row.NextLessonDate is not null && row.Balance <= parameters.Maximum &&
                                    (row.CrmMustPayDate ?? today) <= today,
            _ => true
        };
    }

    //vStudentsFourWeekFee: სტრიქონები, რომლებიც "თარიღამდე" არ დასრულებულა (EndDate ცარიელია ან მის შემდეგაა);
    //ასეთი სტრიქონის გარეშე null
    private static decimal? FourWeekFee(List<DepositGroupStudentData> groupStudents, DateTime dateToEnd)
    {
        List<DepositGroupStudentData> active =
            [.. groupStudents.Where(g => g.EndDate is null || g.EndDate >= dateToEnd)];
        return active.Count == 0 ? null : active.Sum(g => g.FourWeekFee);
    }

    //vStudentMustPayDate: კონტრაქტის ბოლო ზარი (CallDate-ით), რომელშიც "უნდა გადაიხადოს" შევსებულია. ერთი დროის ორი
    //ზარისას Access-ის join სტრიქონს აორმაგებდა; აქ ბოლო (უდიდესი ID-ის) ზარი ირჩევა
    private static Dictionary<int, DateTime> LastCrmMustPayDates(IEnumerable<CrmMustPayDateData> calls)
    {
        return calls.GroupBy(c => c.StudentContractId)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => (c.CallDate, c.CrmCallId))!.MustPayDate);
    }

    private static decimal? SumOrNull(IEnumerable<BalanceOperation> operations)
    {
        decimal? sum = null;
        foreach (BalanceOperation operation in operations)
        {
            sum = (sum ?? 0m) + operation.Amount;
        }

        return sum is null ? null : BalanceOperations.RoundMoney(sum.Value);
    }
}
