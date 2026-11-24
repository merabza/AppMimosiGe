namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     მოსწავლის კონტრაქტის დეტალი (StudentContractDetails): საგნისა და ჯგუფის ზომის ტარიფი
/// </summary>
public sealed record StudentContractTariff(
    int CourseId,
    int GroupSizeId,
    float FourWeekHours,
    decimal FourWeekFee,
    decimal OneHourFee);
