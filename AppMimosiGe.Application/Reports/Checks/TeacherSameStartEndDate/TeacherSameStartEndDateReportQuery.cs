namespace AppMimosiGe.Application.Reports.Checks.TeacherSameStartEndDate;

//r32TeachSameStartEndDate: მასწავლებლის ჯგუფში მუშაობის დაწყება და დამთავრება ერთსა და იმავე თარიღზე
public sealed record TeacherSameStartEndDateReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
