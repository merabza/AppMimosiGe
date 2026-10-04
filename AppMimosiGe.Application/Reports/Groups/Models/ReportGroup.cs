namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     აქტიური ჯგუფი: საგანი, ზომა (GroupSizes: სახელი და ადგილების რაოდენობა) და მოსწავლეების სტატუსი
/// </summary>
public sealed record ReportGroup(
    int GroupId,
    string GroupCode,
    int CourseId,
    string CourseName,
    int GroupSizeId,
    string GroupSizeName,
    int Size,
    int StudentStatusId,
    string StudentStatusName);
