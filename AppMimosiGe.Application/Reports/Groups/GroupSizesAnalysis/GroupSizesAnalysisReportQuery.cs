using System;

namespace AppMimosiGe.Application.Reports.Groups.GroupSizesAnalysis;

//r23GroupSizesAnalize: ჯგუფების ზომების ანალიზი
public sealed record GroupSizesAnalysisReportQuery(DateTime Date) : GroupsReportQuery(Date);
