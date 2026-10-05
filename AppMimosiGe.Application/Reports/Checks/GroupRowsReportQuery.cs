using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Checks;

/// <summary>
///     ჯგუფების სტრიქონების შემოწმების რეპორტის query. AcademicYearId: ჯგუფების სასწავლო წელი, null: ყველა წელი (Access-ში
///     პარამეტრი არ იყო, რადგან ბაზა ერთი წელი იყო; ნაწილი 20)
/// </summary>
public abstract record GroupRowsReportQuery(int? AcademicYearId) : IQuery<ReportTable>;
