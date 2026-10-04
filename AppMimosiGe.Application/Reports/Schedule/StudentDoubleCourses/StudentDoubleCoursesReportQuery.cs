using System;

namespace AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;

//r20StudentDoubleCources: მოსწავლეები ერთ საგანზე რამდენჯერმე
public sealed record StudentDoubleCoursesReportQuery(DateTime Date) : ScheduleReportQuery(Date);
