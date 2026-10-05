namespace AppMimosiGe.Application.Reports.Checks.DayTimeSameStartEndDate;

//r33DTPSameStartEndDate: დღეების განაწილება, დაწყებული და დამთავრებული ერთსა და იმავე თარიღზე
public sealed record DayTimeSameStartEndDateReportQuery(int? AcademicYearId = null) : GroupRowsReportQuery(AcademicYearId);
