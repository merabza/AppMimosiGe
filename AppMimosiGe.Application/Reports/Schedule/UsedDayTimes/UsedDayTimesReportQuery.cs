using System;

namespace AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;

//r07UsedDayTimes: დატვირთვა დღეებისა და საათების მიხედვით
public sealed record UsedDayTimesReportQuery(DateTime Date) : ScheduleReportQuery(Date);
