using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტის Excel-ის (xlsx) ფაილი: სათაური, პარამეტრები, სვეტების სათაურები, სექციები და ჯამები, უჯრები სვეტის
///     ტიპით. რეალიზაცია AppMimosiGe.Infrastructure-შია (DocumentFormat.OpenXml)
/// </summary>
public interface IReportExcelWriter
{
    byte[] Write(ReportResponse report);
}
