using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მოთხოვნის ვალიდატორის (GroupRequestValidator) შეცდომა: თვისება, მაგალითად "Students[2].FourWeekFee" ან
///     "Teachers", და შეცდომა, როგორც ჯგუფის შენახვისას
/// </summary>
public sealed record GroupValidationFailure(string PropertyName, Error Error);
