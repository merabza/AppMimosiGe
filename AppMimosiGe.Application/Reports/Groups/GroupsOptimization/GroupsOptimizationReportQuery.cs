using System;

namespace AppMimosiGe.Application.Reports.Groups.GroupsOptimization;

//r24GroupsOptimization: ჯგუფების ოპტიმიზაციის შესაძლებლობა
public sealed record GroupsOptimizationReportQuery(DateTime Date) : GroupsReportQuery(Date);
