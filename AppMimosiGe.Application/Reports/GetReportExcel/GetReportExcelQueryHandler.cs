using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.RunReport;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.GetReportExcel;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetReportExcelQueryHandler(
    IQueryHandler<RunReportQuery, ReportResponse> runReport,
    IReportExcelWriter excelWriter) : IQueryHandler<GetReportExcelQuery, ReportFile>
{
    //იგივე რეპორტი, რაც ეკრანზე (იგივე შემოწმებებით), ფაილად
    public async Task<Result<ReportFile>> Handle(GetReportExcelQuery query, CancellationToken cancellationToken)
    {
        Result<ReportResponse> report =
            await runReport.Handle(new RunReportQuery(query.Key, query.Parameters), cancellationToken);
        if (report.IsFailure)
        {
            return report.Error;
        }

        //რეპორტი არსებობს: RunReportQueryHandler უცნობ გასაღებზე შეცდომას აბრუნებს
        ReportDefinition definition = ReportsCatalog.Find(report.Value.Key)!;
        return new ReportFile(excelWriter.Write(report.Value),
            ReportParameterRules.ExcelFileName(definition, ReportParameterRules.Normalize(query.Parameters)));
    }
}
