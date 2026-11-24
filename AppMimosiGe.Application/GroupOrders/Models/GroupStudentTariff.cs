namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     მოსწავლის ტარიფი ბრძანების ხაზისთვის: 4 კვირის საათები, 4 კვირის გადასახადი, საათის ფასი და კოეფიციენტი
/// </summary>
public sealed record GroupStudentTariff(
    float FourWeekHours,
    decimal FourWeekFee,
    decimal OneHourFee,
    float HoursCoefficient);
