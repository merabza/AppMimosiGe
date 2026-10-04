using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Groups.GroupList;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GroupListReportQueryHandler(IReportsRepository repository)
    : GroupsReportQueryHandler<GroupListReportQuery>(repository)
{
    protected override ReportTable Build(GroupListReportQuery query, GroupsSnapshot snapshot)
    {
        return GroupListReport.Groups(snapshot, query.TeacherContractId, query.CourseId, query.StudentContractId);
    }
}
