namespace AppMimosiGe.Application.Reports.Checks.DayTimeMissedTransitions;

//r28DayTimesMissDate: დროების აცდენილი გადასვლები
public sealed record DayTimeMissedTransitionsReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
