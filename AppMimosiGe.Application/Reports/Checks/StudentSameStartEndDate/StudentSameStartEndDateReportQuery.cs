namespace AppMimosiGe.Application.Reports.Checks.StudentSameStartEndDate;

//r31StudSameStartEndDate: მოსწავლის სწავლის დაწყება და დამთავრება ერთსა და იმავე თარიღზე
public sealed record StudentSameStartEndDateReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
