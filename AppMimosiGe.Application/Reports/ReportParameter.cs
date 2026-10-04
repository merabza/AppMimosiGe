namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტის პარამეტრი: Name ReportParameterNames-იდანაა, Caption ფილტრისა და სათაურის წარწერაა
/// </summary>
public sealed record ReportParameter(string Name, string Caption, bool Required);
