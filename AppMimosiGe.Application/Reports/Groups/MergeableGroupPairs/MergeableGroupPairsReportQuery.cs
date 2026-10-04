using System;

namespace AppMimosiGe.Application.Reports.Groups.MergeableGroupPairs;

//r09Optimization: ოპტიმიზაციის შესაძლებლობა
public sealed record MergeableGroupPairsReportQuery(DateTime Date) : GroupsReportQuery(Date);
