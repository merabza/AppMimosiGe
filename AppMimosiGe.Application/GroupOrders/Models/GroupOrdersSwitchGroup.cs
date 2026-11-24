namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფი ვიზარდი 1-ის გეგმაში (D196). InitialOrder: მისი საწყისი ბრძანება, რომლის თარიღი ჯგუფის პირველი მოქმედი
///     დღეა; null, თუ ჯგუფი გადასვლის დღეს (D0) და მის შემდეგ არცერთ დღეს არ არის მოქმედი: ასეთი ჯგუფი საწყის ბრძანებას
///     ვერ მიიღებს და ბრძანებით ვეღარ შეიცვლება (D202)
/// </summary>
public sealed record GroupOrdersSwitchGroup(int GroupId, string GroupCode, GroupOrderData? InitialOrder);
