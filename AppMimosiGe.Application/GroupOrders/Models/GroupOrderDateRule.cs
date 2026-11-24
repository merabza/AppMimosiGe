using System;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების თარიღისა და უფლების წესი (D184, D196, D210, D211). ბრძანება იქმნება, იცვლება და იშლება, თუ მისი
///     თარიღი (შეცვლისას ძველიც და ახალიც) დღევანდელია ან მომავლისაა; წარსული თარიღი შეიძლება AppClaim
///     ChangePastGroupOrders-ით. დაპაუზებული ბრძანება ჯგუფს არ ცვლის, ამიტომ მისი ძველი თარიღი არ მოწმდება: ის ძველი
///     თარიღითაც იცვლება და იშლება, ახალი თარიღი კი ისე მოწმდება, როგორც სხვა ბრძანებისა. საწყის ბრძანებას (0) მხოლოდ
///     ვიზარდი 1 ქმნის და მხოლოდ ვიზარდი 2 შლის; მას უფლებით ცვლიან, თარიღის მიუხედავად
/// </summary>
public static class GroupOrderDateRule
{
    /// <summary>
    ///     before: ბრძანება ცვლილებამდე (null: ახალი), after: ცვლილების შემდეგ (null: წაშლა). today: დღევანდელი დღე.
    ///     canChangePastOrders: მომხმარებელს აქვს AppClaim ChangePastGroupOrders. აბრუნებს შეცდომას, ან null-ს, თუ
    ///     ცვლილება დასაშვებია
    /// </summary>
    public static Error? Check(GroupOrderData? before, GroupOrderData? after, DateTime today, bool canChangePastOrders)
    {
        if (before?.OrderType == EGroupOrderType.Initial || after?.OrderType == EGroupOrderType.Initial)
        {
            return CheckInitial(before, after, canChangePastOrders);
        }

        if (canChangePastOrders)
        {
            return null;
        }

        if (before is { IsPaused: false } && before.OrderDate.Date < today.Date)
        {
            return GroupOrderErrors.PastOrderNeedsRight;
        }

        if (after is null || after.OrderDate.Date >= today.Date)
        {
            return null;
        }

        //D184, D210: დაპაუზებული ბრძანება (ან ახლა გააქტიურებული) ძველი თარიღით იხსნება, ინახება კი მხოლოდ a ≥ დღეს
        return after.IsPaused || before is { IsPaused: true }
            ? GroupOrderErrors.PausedOrderDateIsInPast
            : GroupOrderErrors.PastOrderNeedsRight;
    }

    private static Error? CheckInitial(GroupOrderData? before, GroupOrderData? after, bool canChangePastOrders)
    {
        if (before?.OrderType != EGroupOrderType.Initial)
        {
            return GroupOrderErrors.InitialOrderIsCreatedByTransitionOnly;
        }

        if (after is null)
        {
            return GroupOrderErrors.InitialOrderCannotBeDeleted;
        }

        return canChangePastOrders ? null : GroupOrderErrors.InitialOrderNeedsRight;
    }
}
