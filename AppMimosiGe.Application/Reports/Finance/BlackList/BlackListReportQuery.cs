using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Finance.BlackList;

//r15BlackList: შავი სია
public sealed record BlackListReportQuery : IQuery<ReportTable>;
