namespace AppMimosiGe.Application.Reports.Models;

/// <summary>
///     რეპორტის ფაილი (Excel): შიგთავსი და სახელი
/// </summary>
public sealed record ReportFile(byte[] Content, string FileName);
