using System;

namespace AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;

//r03RoomsAgenda: ოთახების ცხრილი
public sealed record RoomsAgendaReportQuery(DateTime Date) : ScheduleReportQuery(Date);
