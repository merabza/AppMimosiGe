namespace AppMimosiGe.Application.Reports.Checks.TeacherMissedTransitions;

//r27TeacherMissDate: მასწავლებლების აცდენილი გადასვლები
public sealed record TeacherMissedTransitionsReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
