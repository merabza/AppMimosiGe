using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის არსებული სტრიქონები, რომლებიც შესანახ მოთხოვნაში აღარ არის და უნდა წაიშალოს
/// </summary>
public sealed record GroupRemovedRows(
    List<GroupByTeacher> Teachers,
    List<GroupByStudent> Students,
    List<GroupDayTimePlace> DayTimePlaces);
