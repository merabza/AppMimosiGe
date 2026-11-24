using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მოსწავლის ხაზი (GroupOrderStudents). სტრიქონი, რომელსაც ხაზი ქმნის, მის Id-ს ინახავს (LineId). ტარიფი
///     (4 კვირის საათები, 4 კვირის გადასახადი, საათის ფასი, კოეფიციენტი) გაყვანის ბრძანებაში (4) ცარიელია.
///     FourWeekHours, StartDate და EndDate მხოლოდ საწყის ბრძანებაშია: სხვა ბრძანების სტრიქონის საათებს ძრავა ფორმულით
///     ითვლის (D187), ხაზზე შენახული საათები კი ბეჭდვისთვისაა
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
    /// <summary>
    ///     საწყისი ბრძანების სტრიქონი: საათები ხაზიდან მოდის, როგორც ჯგუფს ჰქონდა (D196)
    /// </summary>
    public GroupStudentRow ToInitialRow(DateTime startDate, DateTime? endDate)
    {
        return CreateRow(startDate, endDate, FourWeekHours);
    }

    /// <summary>
    ///     სხვა ბრძანების სტრიქონი: 4 კვირის საათები ფორმულით ითვლება სტრიქონის ჯგუფის კვირის საათებიდან (D187)
    /// </summary>
    public GroupStudentRow ToRow(DateTime startDate, DateTime? endDate, float weeklyHours)
    {
        return CreateRow(startDate, endDate,
            HoursCoefficient is { } hoursCoefficient
                ? GroupOrderFormulas.FourWeekHours(hoursCoefficient, weeklyHours)
                : null);
    }

    //სტრიქონს ტარიფი სჭირდება: ხაზს ის ყველა ბრძანებაში აქვს, გაყვანის გარდა
    private GroupStudentRow CreateRow(DateTime startDate, DateTime? endDate, float? fourWeekHours)
    {
        if (fourWeekHours is not { } hours || FourWeekFee is not { } fourWeekFee || OneHourFee is not { } oneHourFee ||
            HoursCoefficient is not { } hoursCoefficient)
        {
            throw new InvalidOperationException($"Group order student line {Id} has no tariff");
        }

        return new GroupStudentRow(Id, StudentContractId, hours, fourWeekFee, oneHourFee, hoursCoefficient, Note,
            startDate, endDate);
    }
}
