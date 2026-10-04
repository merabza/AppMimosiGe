using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.StudentSameStartEndDate;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentSameStartEndDateReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<StudentSameStartEndDateReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return SameStartEndDateReports.StudentSameStartEndDate(snapshot);
    }
}
