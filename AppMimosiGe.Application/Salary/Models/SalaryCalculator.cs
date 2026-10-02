using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     ხელფასის გამოთვლა (Access-ის SalaryModule.CountSalary და მისი query-ები), EF-ის გარეშე:
///     1. ჩატარებული გაკვეთილების ხელფასი (ტიპი 1): ყოველ გაუქმებულ (სტატუსი 2) გაკვეთილზე მასწავლებელი შემცვლელია,
///     თუ ჰყავს, საათები მოსწავლეების საათების მაქსიმუმი. ჯგუფი × მასწავლებელი × სქემა × თვე: საათების ჯამი × სქემის
///     საათობრივი ხელფასი; მასწავლებელზე ჯამი უწყისში ხვდება, თუ მისი ხელფასის თარიღი დარიცხვის თარიღია;
///     2. სტრიქონები ყველა კონტრაქტზე: დანამატები (N, დამრგვალებული) და გამოქვითვები (G) სამი სქემით;
///     3. სტრიქონის დეტალები ჯგუფებით: ჯგუფის თანხა, საათები და ერთი საათის ღირებულება
/// </summary>
public static class SalaryCalculator
{
    public const int LessonSalaryPartTypeId = 1;
    public const int AdditionCountPlaceId = 1;
    public const int DeductionCountPlaceId = 2;
    private const int CancelledLessonStatusId = 2;
    private const int SalaryRsQuoteTypeId = 1;
    private const int IndividualEntrepreneurRsQuoteTypeId = 6;

    //Access-ის Currency (money) 4 ათწილადს ინახავს; CCur ტოლობისას ლუწზე ამრგვალებს
    public static decimal Money(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.ToEven);
    }

    //vSalaryCountBase2Danamatebi: Int(Σ/(-2))*(-2), ანუ 2-ის ჯერადამდე ზემოთ (უარყოფითი ნულისკენ)
    public static decimal RoundUpToTwo(decimal sum)
    {
        return Math.Floor(sum / -2m) * -2m;
    }

    //vSalaryCountBase3NoPensionsDanamatebi: Int(Σ*(-25))/(-25), ანუ 0.04-ის ჯერადამდე ზემოთ
    public static decimal RoundUpToFourHundredths(decimal sum)
    {
        return Math.Floor(sum * -25m) / -25m;
    }

    //vR16TSBase4SalaryByLessonsForSalaryParts: თვის 1-ლი + 1 თვე + 4 დღე, NextMonth-ისას იმავე თვის 5 რიცხვი
    public static DateTime SalaryDate(DateTime lessonMonth, bool nextMonth)
    {
        return (nextMonth ? lessonMonth : lessonMonth.AddMonths(1)).AddDays(4);
    }

    //სტრიქონის თვე (saMonthDate): დარიცხვის თარიღის წინა თვის 1-ლი რიცხვი
    public static DateTime LineMonthDate(DateTime chargeDate)
    {
        return MonthStart(chargeDate).AddMonths(-1);
    }

    public static SalaryCalculationResult Calculate(SalaryCalculationInput input)
    {
        List<GroupMonthAmount> groupAmounts = GroupAmounts(input);
        Dictionary<int, SalaryContractData> contracts = input.Contracts.ToDictionary(x => x.Id);

        List<CalculatedSalaryPart> lessonParts =
        [
            .. groupAmounts
                .Where(x => contracts.TryGetValue(x.TeacherContractId, out SalaryContractData? contract) &&
                            SalaryDate(x.MonthDate, contract.NextMonth) == input.ChargeDate)
                .GroupBy(x => x.TeacherContractId).OrderBy(g => g.Key)
                .Select(g => new CalculatedSalaryPart(g.Key, Money(g.Sum(x => x.Amount))))
        ];

        List<SalaryPartData> parts =
        [
            .. input.ManualParts,
            .. lessonParts.Select(x => new SalaryPartData(x.TeacherContractId, LessonSalaryPartTypeId, x.Amount))
        ];

        DateTime monthDate = LineMonthDate(input.ChargeDate);
        List<CalculatedSalaryLine> lines = [];
        foreach (SalaryContractData contract in input.Contracts.OrderBy(x => x.Id))
        {
            CalculatedSalaryLine? line = CalculateLine(contract, parts, input.PartTypeCountPlaces, monthDate,
                groupAmounts);
            if (line is not null)
            {
                lines.Add(line);
            }
        }

        return new SalaryCalculationResult(lessonParts, lines);
    }

    private static CalculatedSalaryLine? CalculateLine(SalaryContractData contract, List<SalaryPartData> parts,
        IReadOnlyDictionary<int, int?> countPlaces, DateTime monthDate, List<GroupMonthAmount> groupAmounts)
    {
        List<SalaryPartData> contractParts = [.. parts.Where(x => x.TeacherContractId == contract.Id)];
        decimal additions = SumByCountPlace(contractParts, countPlaces, AdditionCountPlaceId);
        decimal g = SumByCountPlace(contractParts, countPlaces, DeductionCountPlaceId);
        //საპენსიოში ჩართული (ინდ. მეწარმის გარდა) 2 ლარამდე მრგვალდება, დანარჩენები 0.04-მდე
        decimal n = contract.PensionScheme && !contract.IndEnt
            ? RoundUpToTwo(additions)
            : RoundUpToFourHundredths(additions);

        //Access-ის პირობაა |N| ≥ 0.01 ან |G| ≥ 0.01. N 2-ის ან 0.04-ის ჯერადია, ამიტომ |N| ≥ 0.01 იგივეა, რაც N ≠ 0
        if (n == 0m && Math.Abs(g) < 0.01m)
        {
            return null;
        }

        //NextMonth-ის კონტრაქტის ხელფასი იმავე თვის გაკვეთილებისაა, ამიტომ დეტალებიც იმ თვისაა (Access-ში წინა
        //თვისა იყო, D107)
        DateTime detailsMonth = contract.NextMonth ? monthDate.AddMonths(1) : monthDate;
        List<CalculatedSalaryLineDetail> details =
        [
            .. groupAmounts.Where(x => x.TeacherContractId == contract.Id && x.MonthDate == detailsMonth)
                .GroupBy(x => x.GroupId).OrderBy(x => x.Key).Select(x => Detail(x.Key, x.ToList()))
        ];

        if (contract.IndEnt)
        {
            return new CalculatedSalaryLine(contract.Id, n, n, 0m, n, 0m, g, 0m, Money(n * 0.99m - g), monthDate,
                IndividualEntrepreneurRsQuoteTypeId, Money(n * 0.01m), details);
        }

        if (contract.PensionScheme)
        {
            return new CalculatedSalaryLine(contract.Id, n, Money(n * 1.25m), Money(n * 0.025m), Money(n * 1.225m),
                Money(n * 0.245m), g, Money(n * 0.05m), Money(n * 0.98m - g), monthDate, SalaryRsQuoteTypeId, 0m,
                details);
        }

        return new CalculatedSalaryLine(contract.Id, n, Money(n * 1.25m), 0m, Money(n * 1.25m), Money(n / 4m), g, 0m,
            n - g, monthDate, SalaryRsQuoteTypeId, 0m, details);
    }

    //Access-ში HourCost = თანხა / საათები; 0 საათზე Access-ის გამოთვლა შეცდომით წყდებოდა, აქ 0-ია
    private static CalculatedSalaryLineDetail Detail(int groupId, List<GroupMonthAmount> amounts)
    {
        decimal amount = amounts.Sum(x => x.Amount);
        double hours = amounts.Sum(x => x.Hours);
        decimal hoursCount = (decimal)hours;
        decimal hourCost = hoursCount == 0m ? 0m : Money(amount / hoursCount);
        return new CalculatedSalaryLineDetail(groupId, Money(amount), (float)hours, hourCost);
    }

    private static decimal SumByCountPlace(List<SalaryPartData> parts, IReadOnlyDictionary<int, int?> countPlaces,
        int countPlaceId)
    {
        return parts.Where(x =>
                x.SalaryPartTypeId is { } typeId && countPlaces.TryGetValue(typeId, out int? place) &&
                place == countPlaceId)
            .Sum(x => x.Amount);
    }

    //vR16TSBase2GroupsHours + vR16TSBase3GroupAmounts: მასწავლებელი × სქემა × თვე × ჯგუფი
    private static List<GroupMonthAmount> GroupAmounts(SalaryCalculationInput input)
    {
        //VR16TSBase1ChargeDates: გაკვეთილი თავისი მასწავლებლით (შემცვლელი, თუ ჰყავს), სქემით, თვით და ჯგუფით, საათები
        //მოსწავლეების საათების მაქსიმუმი; გაკვეთილის თვე სამუშაო თვეებში უნდა იყოს
        var lessons = input.LessonRows.Where(x => x.LessonStatusId != CancelledLessonStatusId)
            .GroupBy(x => new
            {
                x.LessonId,
                TeacherContractId = x.SubstituteTeacherContractId ?? x.TeacherContractId,
                x.SalarySchemeId,
                MonthDate = MonthStart(x.LessonDt),
                x.GroupId
            }).Select(g => new
            {
                g.Key.TeacherContractId,
                g.Key.SalarySchemeId,
                g.Key.MonthDate,
                g.Key.GroupId,
                MaxHours = g.Max(x => x.HoursCount)
            }).Where(x => input.OperationMonths.Contains(x.MonthDate));

        return
        [
            .. lessons.GroupBy(x => new { x.TeacherContractId, x.SalarySchemeId, x.MonthDate, x.GroupId }).Select(g =>
            {
                double hours = g.Sum(x => (double)x.MaxHours);
                decimal rate = input.HourRates.GetValueOrDefault(g.Key.SalarySchemeId);
                return new GroupMonthAmount(g.Key.TeacherContractId, g.Key.GroupId, g.Key.MonthDate,
                    (decimal)hours * rate, hours);
            })
        ];
    }

    private static DateTime MonthStart(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
    }

    private sealed record GroupMonthAmount(
        int TeacherContractId,
        int GroupId,
        DateTime MonthDate,
        decimal Amount,
        double Hours);
}
