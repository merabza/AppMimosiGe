using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Models;

/// <summary>
///     რეპორტის query handler-ის შედეგი: სვეტები, სექციები და რეპორტის ჯამის სტრიქონები. გასაღებს, სათაურს და
///     პარამეტრებს კატალოგი ამატებს (ReportsCatalog.Run)
/// </summary>
public sealed record ReportTable(
    List<ReportColumnResponse> Columns,
    List<ReportSectionResponse> Sections,
    List<List<object?>> FooterRows)
{
    //დაუჯგუფებელი რეპორტი: ერთი სექცია სათაურის და ჯამის გარეშე
    public static ReportTable Flat(List<ReportColumnResponse> columns, List<List<object?>> rows,
        List<List<object?>>? footerRows = null)
    {
        return new ReportTable(columns, [new ReportSectionResponse(null, rows, null)], footerRows ?? []);
    }
}
