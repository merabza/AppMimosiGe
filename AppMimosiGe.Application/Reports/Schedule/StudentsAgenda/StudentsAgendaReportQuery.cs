using System;

namespace AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;

//r04StudentsAgenda: მოსწავლეების ცხრილი
public sealed record StudentsAgendaReportQuery(DateTime Date) : ScheduleReportQuery(Date);
