using System;

namespace AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;

//r06RoomOver: ოთახების გადაფარვა
public sealed record RoomOverlapsReportQuery(DateTime Date) : ScheduleReportQuery(Date);
