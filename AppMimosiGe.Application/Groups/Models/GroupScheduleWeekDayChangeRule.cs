using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     კვირის დღეების არასწორი ცვლილება (D181, რეპორტ r22-თან ერთად): განრიგის სტრიქონს, რომელიც დღემდე დაიწყო, კვირის
///     დღე ადგილზე არ ეცვლება და ის არ იშლება, რადგან გენერატორი მის წარსულ გაკვეთილებსაც ახალ დღეზე გადაიტანდა ან
///     წაშლიდა (დასწრებიან გაკვეთილს D177 იცავს, დანარჩენი უბრალოდ გადაიწერებოდა). სწორი ცვლილება: სტრიქონი
///     სრულდება და ახალი კვირის დღე ახალ სტრიქონად ემატება. დღეს ან მომავალში დაწყებული სტრიქონი თავისუფლად სწორდება
///     და იშლება. დაწყებული სტრიქონის სხვა ველებს ეს წესი არ ეხება
/// </summary>
public static class GroupScheduleWeekDayChangeRule
{
    /// <summary>
    ///     მოთხოვნა ცვლის თუ არა ჯგუფის შენახული სტრიქონის კვირის დღეს, რომელიც today-ის დღემდე დაიწყო. სტრიქონის
    ///     დაწყებად შენახული თარიღი ითვლება, ამიტომ დაწყების მომავალში გადატანა ცვლილებას არ უშვებს. ახალი სტრიქონი
    ///     (Id 0) ცვლილება არ არის
    /// </summary>
    public static bool ChangesStartedRowWeekDay(Group group, GroupRequest request, DateTime today)
    {
        Dictionary<int, GroupDayTimePlace> savedRows = group.GroupDayTimePlaces.ToDictionary(x => x.GdtpId);
        return request.DayTimePlaces.Any(row =>
            savedRows.TryGetValue(row.Id, out GroupDayTimePlace? saved) && HasStarted(saved, today) &&
            saved.WeekDayId != row.WeekDayId);
    }

    /// <summary>
    ///     ჯგუფის შენახული სტრიქონებიდან, რომლებიც მოთხოვნაში აღარ არის (GroupMapper.FindRemovedRows), რომელიმე
    ///     today-ის დღემდე დაიწყო თუ არა
    /// </summary>
    public static bool RemovesStartedRow(IEnumerable<GroupDayTimePlace> removedRows, DateTime today)
    {
        return removedRows.Any(row => HasStarted(row, today));
    }

    //სტრიქონის შენახული დაწყება დღევანდელ თარიღზე ადრეა
    private static bool HasStarted(GroupDayTimePlace row, DateTime today)
    {
        return row.StartDate.Date < today.Date;
    }
}
