using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.WorkTime.Models;

/// <summary>
///     r36-ის მონაცემები პერიოდისთვის: გაკვეთილები, სამუშაო საათების ჩანაწერები და თანამშრომლების (კონტრაქტების)
///     სახელები და ნომრები
/// </summary>
public sealed record WorkTimeData(
    IReadOnlyList<WorkTimeLesson> Lessons,
    IReadOnlyList<WorkTimeRecord> Records,
    IReadOnlyDictionary<int, SchedulePerson> Employees);
