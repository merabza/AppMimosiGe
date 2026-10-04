using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Checks;

/// <summary>
///     ჯგუფების სტრიქონების შემოწმების რეპორტის query (პარამეტრების გარეშე, Access-ის მსგავსად)
/// </summary>
public abstract record GroupRowsReportQuery : IQuery<ReportTable>;
