using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Finance.TeacherSalaryByGroups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherSalaryByGroupsReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<TeacherSalaryByGroupsReportQuery, ReportTable>
{
    //ხელფასის სტრიქონის თვე "თარიღიდან"-ის თვიდან "თარიღამდე"-ს თვის ჩათვლით (Access-ის DateSerial(…, 1))
    public async Task<Result<ReportTable>> Handle(TeacherSalaryByGroupsReportQuery query,
        CancellationToken cancellationToken)
    {
        List<SalaryDetailRow> details = await repository.GetSalaryDetails(ReportMonths.Start(query.StartDate),
            ReportMonths.Start(query.EndDate), query.TeacherContractId, cancellationToken);
        IReadOnlyDictionary<int, string> monthNames = await repository.GetMonthNames(cancellationToken);
        return FinanceReports.TeacherSalaryByGroups(details, monthNames);
    }
}
