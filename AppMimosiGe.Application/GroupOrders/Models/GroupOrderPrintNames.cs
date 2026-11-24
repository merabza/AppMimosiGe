using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბეჭდვის სახელები Id-ით (IGroupOrdersRepository.GetPrintNames): მასწავლებლებისა და მოსწავლეების „გვარი სახელი"
///     კონტრაქტის Id-ით, ხელფასის სქემები, კვირის დღეები, დაწყების დროები („HH:mm") და ოთახები
/// </summary>
public sealed record GroupOrderPrintNames(
    IReadOnlyDictionary<int, string> Teachers,
    IReadOnlyDictionary<int, string> SalarySchemes,
    IReadOnlyDictionary<int, string> Students,
    IReadOnlyDictionary<int, string> WeekDays,
    IReadOnlyDictionary<int, string> LessonStartTimes,
    IReadOnlyDictionary<int, string> Rooms);
