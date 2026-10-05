namespace AppMimosiGe.Application.Reports.Checks.StudentMissedTransitions;

//r26StudMissDate: მოსწავლეების აცდენილი გადასვლები
public sealed record StudentMissedTransitionsReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
