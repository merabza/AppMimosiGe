using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;
using AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;
using AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;
using AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;
using AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;
using AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;
using AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;
using AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;
using AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტების კატალოგი (Access-ის Reports, ReportCategories, ReportsByCategories; Q5, D111): კატეგორიები და
///     რეპორტები Access-ის რიგით. ახალი რეპორტი: query და handler (თითო რეპორტი ცალკე handler-ია), ჩანაწერი
///     Definitions-ში და AppClaim იმავე გასაღებით DataSeederRules-ში (D112)
/// </summary>
public static class ReportsCatalog
{
    //"ყველა": კატალოგის ყოველი რეპორტი, Definitions-ში ცალკე არ იწერება
    public const string AllCategoryKey = "all";
    public const string ScheduleCategoryKey = "schedule";
    public const string GroupsCategoryKey = "groups";
    public const string CommentsCategoryKey = "comments";
    public const string LessonsCategoryKey = "lessons";
    public const string FinanceCategoryKey = "finance";
    public const string ChecksCategoryKey = "checks";

    private const string ActiveGroupsNote = "მხოლოდ თარიღისთვის აქტიური ჯგუფები: მოქმედი მოსწავლით, მასწავლებლით და " +
                                            "განრიგით, გაუქმების გარეშე.";

    //Access-ის ReportCategories, ID-ების რიგით (Access-ის სიაში სახელით ლაგდებოდა)
    public static readonly IReadOnlyList<ReportCategory> Categories =
    [
        new(AllCategoryKey, "ყველა"),
        new(ScheduleCategoryKey, "გაკვეთილების ცხრილი"),
        new(GroupsCategoryKey, "ჯგუფები"),
        new(CommentsCategoryKey, "კომენტარები"),
        new(LessonsCategoryKey, "გაკვეთილები"),
        new(FinanceCategoryKey, "ფინანსები"),
        new(ChecksCategoryKey, "შემოწმება")
    ];

    //Access-ის რეპორტების სათაურში მხოლოდ თარიღისას "თარიღისთვის" ეწერა (FrmMain-ზე "თარიღამდე:")
    private static readonly ReportParameter[] ForDate =
        [new(ReportParameterNames.EndDate, ReportParameterCaptions.ForDate, true)];

    //r35TeacherLineOver არ გადმოდის (Q9, D114). ნაწილები 17 და 18 აქ თავიანთ რეპორტებს ამატებენ
    public static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        ReportDefinition.Create("r03RoomsAgenda", "ოთახების ცხრილი",
            $"ოთახების გაკვეთილები კვირის დღეების მიხედვით: მასწავლებელი, საგანი, ჯგუფი, დაწყების დრო. {ActiveGroupsNote}",
            [ScheduleCategoryKey], ForDate, p => new RoomsAgendaReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r04StudentsAgenda", "მოსწავლეების ცხრილი",
            $"მოსწავლეების გაკვეთილები კვირის დღეების მიხედვით: საგანი, ჯგუფი, მასწავლებელი, დაწყების დრო. {ActiveGroupsNote}",
            [ScheduleCategoryKey], ForDate, p => new StudentsAgendaReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r05TeachersAgenda", "მასწავლებლების ცხრილი",
            $"მასწავლებლების გაკვეთილები კვირის დღეების მიხედვით: საგანი, ჯგუფი, ოთახი, დაწყების დრო. {ActiveGroupsNote}",
            [ScheduleCategoryKey], ForDate, p => new TeachersAgendaReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r06RoomOver", "ოთახების გადაფარვა",
            $"ერთ ოთახში კვირის ერთსა და იმავე დღეს დროით გადამფარავი გაკვეთილები. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate, p => new RoomOverlapsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r07UsedDayTimes", "დატვირთვა დღეებისა და საათების მიხედვით",
            $"რამდენი გაკვეთილი იწყება კვირის თითო დღეს თითო დროს, დღეების ჯამებით. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate, p => new UsedDayTimesReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r18TeacherOver", "მასწავლებლების დროების გადაფარვა",
            $"ერთი მასწავლებლის კვირის ერთსა და იმავე დღეს დროით გადამფარავი გაკვეთილები. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate, p => new TeacherOverlapsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r19StudentOver", "მოსწავლეების დროების გადაფარვა",
            $"ერთი მოსწავლის კვირის ერთსა და იმავე დღეს დროით გადამფარავი გაკვეთილები. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate, p => new StudentOverlapsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r20StudentDoubleCources", "მოსწავლეები დარეგისტრირებული ერთ საგანზე რამდენჯერმე",
            $"მოსწავლეები, რომლებიც ერთ საგანზე რამდენიმე ჯგუფში (ან ერთ ჯგუფში ორჯერ) არიან. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate,
            p => new StudentDoubleCoursesReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r21TeacherDoubleGroups", "მასწავლებლები დარეგისტრირებული ერთ ჯგუფში რამდენჯერმე",
            $"მასწავლებლები, რომლებიც ერთ ჯგუფში ერთზე მეტჯერ არიან დარეგისტრირებული. {ActiveGroupsNote}",
            [ScheduleCategoryKey, ChecksCategoryKey], ForDate,
            p => new TeacherDoubleGroupsReportQuery(p.EndDate!.Value))
    ];

    //გასაღები რეგისტრის მიუხედავად (Access-ში r34-ს რეპორტში "R34" ეწერა, ცხრილში "r34")
    public static ReportDefinition? Find(string key)
    {
        return Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    //მომხმარებლის კატალოგი: რეპორტები, რომელთა AppClaim მას აქვს, და კატეგორიები, რომლებშიც ასეთი რეპორტი არის
    public static ReportCatalogResponse ForClaims(IReadOnlySet<string> claims)
    {
        List<ReportDefinition> visible = [.. Definitions.Where(d => claims.Contains(d.Key))];
        return new ReportCatalogResponse([
            .. Categories.Select(category => new ReportCategoryResponse(category.Key, category.Name, [
                .. visible.Where(d => category.Key == AllCategoryKey || d.CategoryKeys.Contains(category.Key))
                    .Select(d => d.Key)
            ])).Where(category => category.ReportKeys.Count > 0)
        ], [
            .. visible.Select(d => new ReportInfoResponse(d.Key, d.Title, d.Description, [
                .. d.Parameters.Select(p => new ReportParameterInfoResponse(p.Name, p.Caption, p.Required))
            ]))
        ]);
    }
}
