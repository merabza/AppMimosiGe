using System;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 1-ის რეპორტის დარღვევა (D194): ჯგუფი, დარღვეული წესი (შეცდომა, როგორც ჯგუფის შენახვისას), დღე, როცა
///     წესი ირღვევა, და სტრიქონი: ვისი სტრიქონია, მაგალითად მოსწავლის სახელი. Date და Row ცარიელია, როცა დარღვევა მთელ
///     ჯგუფს ეხება
/// </summary>
public sealed record GroupOrdersSwitchViolation(int GroupId, string GroupCode, Error Rule, DateTime? Date, string? Row);
