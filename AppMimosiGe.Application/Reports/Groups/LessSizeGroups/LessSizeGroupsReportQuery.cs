using System;

namespace AppMimosiGe.Application.Reports.Groups.LessSizeGroups;

//r08LessSizeGroups: შეუვსებელი ჯგუფები
public sealed record LessSizeGroupsReportQuery(DateTime Date) : GroupsReportQuery(Date);
