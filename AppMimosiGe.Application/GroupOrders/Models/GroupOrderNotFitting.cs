using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანება, რომელიც ჯგუფის მდგომარეობას აღარ ესადაგება (D185): მისი Id და მიზეზი GroupOrderErrors-იდან. თუ ეს თვითონ
///     შესანახი ბრძანებაა, შეცდომა მიზეზია; გვიანდელი ბრძანებისთვის კი OrderNoLongerFits მისი ნომრით
/// </summary>
public sealed record GroupOrderNotFitting(int OrderId, Error Error);
