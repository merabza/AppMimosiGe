using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Models;

/// <summary>
///     რეპორტის ჯამის სტრიქონები: პირველ სვეტში წარწერა, შემდეგ მნიშვნელობა, დანარჩენი უჯრები ცარიელია
/// </summary>
public static class ReportFooters
{
    public const string TotalCaption = "სულ:";

    //Access-ის რეპორტის =Count(*) (ჩანაწერების რაოდენობა)
    public static List<object?> Count(IReadOnlyCollection<ReportColumnResponse> columns, int count)
    {
        return [TotalCaption, count, .. Enumerable.Repeat<object?>(null, columns.Count - 2)];
    }
}
