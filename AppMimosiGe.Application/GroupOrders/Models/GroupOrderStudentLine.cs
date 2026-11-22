using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მოსწავლის ხაზი (GroupOrderStudents). სტრიქონი, რომელსაც ხაზი ქმნის, მის Id-ს ინახავს (LineId). ტარიფი
///     (4 კვირის საათები, 4 კვირის გადასახადი, საათის ფასი, კოეფიციენტი) გაყვანის ბრძანებაში (4) ცარიელია. StartDate და
///     EndDate მხოლოდ საწყის ბრძანებაშია
/// </summary>
public sealed record GroupOrderStudentLine(
    int Id,
    int StudentContractId,
    float? FourWeekHours,
    decimal? FourWeekFee,
    decimal? OneHourFee,
    float? HoursCoefficient,
    string? Note = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null)
{
    //სტრიქონს ტარიფი სჭირდება: ხაზს ის ყველა ბრძანებაში აქვს, გაყვანის გარდა
    public GroupStudentRow ToRow(DateTime startDate, DateTime? endDate)
    {
        if (FourWeekHours is not { } fourWeekHours || FourWeekFee is not { } fourWeekFee ||
            OneHourFee is not { } oneHourFee || HoursCoefficient is not { } hoursCoefficient)
        {
            throw new InvalidOperationException($"Group order student line {Id} has no tariff");
        }

        return new GroupStudentRow(Id, StudentContractId, fourWeekHours, fourWeekFee, oneHourFee, hoursCoefficient,
            Note, startDate, endDate);
    }
}
