using System;

namespace AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;

//r05TeachersAgenda: მასწავლებლების ცხრილი
public sealed record TeachersAgendaReportQuery(DateTime Date) : ScheduleReportQuery(Date);
