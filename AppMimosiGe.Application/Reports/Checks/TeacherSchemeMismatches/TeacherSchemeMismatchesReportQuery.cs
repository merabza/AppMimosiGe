namespace AppMimosiGe.Application.Reports.Checks.TeacherSchemeMismatches;

//r29TeacherMissSalary: მასწავლებლების აცდენილი ხელფასები
public sealed record TeacherSchemeMismatchesReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
