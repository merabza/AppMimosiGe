using System;

namespace AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;

//r19StudentOver: მოსწავლეების დროების გადაფარვა
public sealed record StudentOverlapsReportQuery(DateTime Date) : ScheduleReportQuery(Date);
