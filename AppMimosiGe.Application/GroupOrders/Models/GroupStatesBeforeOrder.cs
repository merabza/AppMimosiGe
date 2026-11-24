using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფების მდგომარეობა ბრძანების ადგილამდე (თარიღი, Id), გატარების რიგით (D185): ითვლება ადრეული თარიღის ბრძანებები
///     და იმავე დღის უფრო ადრე შექმნილი (ნაკლები Id-ის) ბრძანებები. თავად ბრძანება არ ითვლება, თუნდაც ის სხვა თარიღზე
///     იყოს: შეცვლისას ის ახალ თარიღზე გადადის. ახალ ბრძანებას GroupOrderSaveSteps.NewOrderId აქვს, ამიტომ მისი დღის
///     ყველა ბრძანება ითვლება. უფრო გვიანდელი თარიღის ბრძანება ამ დღეს მოქმედ სტრიქონებს არ ცვლის, ამიტომ ამ დღისთვის
///     შედეგი ზუსტად ისაა, რასაც ბრძანება ხედავს
/// </summary>
public static class GroupStatesBeforeOrder
{
    /// <summary>
    ///     orders: ჯგუფებისა და მათთან გადაყვანებით დაკავშირებული ჯგუფების ყველა ბრძანება (GetLinkedOrders)
    /// </summary>
    public static GroupOrdersFoldResult Run(IEnumerable<GroupOrderData> orders, DateTime orderDate, int orderId)
    {
        DateTime day = orderDate.Date;
        return GroupOrdersFold.Run(orders.Where(o =>
            o.Id != orderId && (o.OrderDate.Date < day || o.OrderDate.Date == day && o.Id < orderId)));
    }
}
