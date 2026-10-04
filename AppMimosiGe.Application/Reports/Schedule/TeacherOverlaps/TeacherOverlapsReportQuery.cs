using System;

namespace AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;

//r18TeacherOver: მასწავლებლების დროების გადაფარვა
public sealed record TeacherOverlapsReportQuery(DateTime Date) : ScheduleReportQuery(Date);
