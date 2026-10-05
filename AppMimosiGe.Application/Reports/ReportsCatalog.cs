using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Checks.DayTimeMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.DayTimeSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.StudentFeeMismatches;
using AppMimosiGe.Application.Reports.Checks.StudentMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.StudentSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.TeacherMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.TeacherSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.TeacherSchemeMismatches;
using AppMimosiGe.Application.Reports.Comments.DailyComments;
using AppMimosiGe.Application.Reports.Finance.BlackList;
using AppMimosiGe.Application.Reports.Finance.TeacherSalaryByGroups;
using AppMimosiGe.Application.Reports.Groups.GroupList;
using AppMimosiGe.Application.Reports.Groups.GroupSizesAnalysis;
using AppMimosiGe.Application.Reports.Groups.GroupsOptimization;
using AppMimosiGe.Application.Reports.Groups.LessSizeGroups;
using AppMimosiGe.Application.Reports.Groups.MergeableGroupPairs;
using AppMimosiGe.Application.Reports.Lessons.LessonsWithErrors;
using AppMimosiGe.Application.Reports.Lessons.LessonsWithWrongVoidStatus;
using AppMimosiGe.Application.Reports.Lessons.Missings;
using AppMimosiGe.Application.Reports.Lessons.MissingsInRow;
using AppMimosiGe.Application.Reports.Lessons.TeacherMissAndSubstitutes;
using AppMimosiGe.Application.Reports.Lessons.WrongStatusLessons;
using AppMimosiGe.Application.Reports.Lessons.WrongWeekDayChanges;
using AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;
using AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;
using AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;
using AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;
using AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;
using AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;
using AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;
using AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;
using AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;
using AppMimosiGe.Application.Reports.WorkTime.TimeSheet;
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

    private const string AllGroupRowsNote =
        "არჩეული სასწავლო წლის (ცარიელი — ყველა წლის) ყველა ჯგუფის ყველა სტრიქონი, თარიღების მიუხედავად.";

    //Access-ის რეპორტების სათაურში მხოლოდ თარიღისას "თარიღისთვის" ეწერა (FrmMain-ზე "თარიღამდე:")
    private static readonly ReportParameter[] ForDate =
        [new(ReportParameterNames.EndDate, ReportParameterCaptions.ForDate, true)];

    //"თარიღამდე" ჩათვლით (Access-ის txtEndDate-ის ნაგულისხმევი დღის 23:59:59-ია)
    private static readonly ReportParameter[] Period =
    [
        new(ReportParameterNames.StartDate, ReportParameterCaptions.StartDate, true),
        new(ReportParameterNames.EndDate, ReportParameterCaptions.EndDate, true)
    ];

    private static readonly ReportParameter[] NoParameters = [];

    //პარამეტრების გარეშე შემოწმებების სასწავლო წელი (ნაწილი 20): არასავალდებულო, ცარიელი = ყველა წელი (D125)
    private static readonly ReportParameter[] AcademicYear =
        [new(ReportParameterNames.AcademicYearId, ReportParameterCaptions.AcademicYear, false)];

    //Access-ის ფილტრები მასწავლებელი, საგანი და მოსწავლე: არასავალდებულო (ცარიელი = ყველა)
    private static readonly ReportParameter Teacher =
        new(ReportParameterNames.TeacherId, ReportParameterCaptions.Teacher, false);

    private static readonly ReportParameter Course =
        new(ReportParameterNames.CourseId, ReportParameterCaptions.Course, false);

    private static readonly ReportParameter Student =
        new(ReportParameterNames.StudentId, ReportParameterCaptions.Student, false);

    //Access-ის ReportName-ის (გასაღების) ID-ების რიგით. r02SummaryComments (D8) და r35TeacherLineOver (Q9, D114) არ
    //გადმოდის
    public static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        ReportDefinition.Create("r01Comments", "დღის კომენტარები 1 თვისთვის",
            "თვის გაკვეთილები ჯგუფებით: მოსწავლეები (გაკვეთილში დანომრილი) ჟურნალში შეტანილი კომენტარებით, აღდგენის " +
            "თარიღი და შემცვლელი მასწავლებელი; შეუვსებელი ადგილი ცარიელია ხელით შესავსებად. „თვე\" — არჩეული " +
            "თარიღის თვე; მასწავლებლით მხოლოდ მისი გაკვეთილები.", [CommentsCategoryKey],
            [new ReportParameter(ReportParameterNames.EndDate, ReportParameterCaptions.Month, true), Teacher],
            p => new DailyCommentsReportQuery(p.EndDate!.Value, p.TeacherId)),
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
        ReportDefinition.Create("r08LessSizeGroups", "შეუვსებელი ჯგუფები",
            $"ჯგუფები, რომლებშიც მოსწავლეები ადგილებზე (ჯგუფის ზომაზე) ნაკლებია, ზომების მიხედვით. {ActiveGroupsNote}",
            [GroupsCategoryKey, ChecksCategoryKey], ForDate, p => new LessSizeGroupsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r09Optimization", "ოპტიმიზაციის შესაძლებლობა",
            "შეუვსებელი ჯგუფების წყვილები ერთი საგნით, მოსწავლეების სტატუსით და ზომით, რომელთა მოსწავლეები ერთ " +
            $"ჯგუფში ეტევა. {ActiveGroupsNote}", [GroupsCategoryKey, ChecksCategoryKey], ForDate,
            p => new MergeableGroupPairsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r10Groups", "ჯგუფები",
            "ჯგუფები მასწავლებლებით (სქემა, დაწყება) და მოსწავლეებით (4 კვირის საათები, გადასახადი, კოეფიციენტი, " +
            "დაწყება). მასწავლებელი, საგანი და მოსწავლე არასავალდებულო ფილტრებია: მასწავლებლით მხოლოდ ის ჯგუფები, " +
            $"სადაც ის ასწავლის, მოსწავლით — სადაც ის სწავლობს. {ActiveGroupsNote}", [GroupsCategoryKey],
            [.. ForDate, Teacher, Course, Student],
            p => new GroupListReportQuery(p.EndDate!.Value, p.TeacherId, p.CourseId, p.StudentId)),
        ReportDefinition.Create("r11WrongStatuseLessons", "გასაუქმებელი გაკვეთილები",
            "გაკვეთილები სტატუსით „არ გაუქმებულა\", რომლებსაც არცერთი მოსწავლე არ დასწრებია: ან გაუქმებულად უნდა " +
            "მოინიშნოს, ან დასწრება შეივსოს. მხოლოდ უკვე დაწყებული გაკვეთილები.",
            [LessonsCategoryKey, ChecksCategoryKey], Period,
            p => new WrongStatusLessonsReportQuery(p.StartDate!.Value, p.EndDate!.Value)),
        ReportDefinition.Create("r12LessonsWithWrongVoidStatus", "არასწორად გაუქმებული გაკვეთილები",
            "გაუქმებული გაკვეთილები (სტატუსი „არ გაუქმებულა\"-ს გარდა), რომლებსაც დამსწრე მოსწავლე ან აღდგენის " +
            "თარიღი აქვს.", [LessonsCategoryKey, ChecksCategoryKey], Period,
            p => new LessonsWithWrongVoidStatusReportQuery(p.StartDate!.Value, p.EndDate!.Value)),
        ReportDefinition.Create("r13LessonsWithErrors", "შეცდომიანი გაკვეთილები",
            "გაკვეთილები, რომლებზეც გენერატორმა ბოლო გაშვებისას შეცდომა ჩაწერა: ზედმეტი გაკვეთილი ან მოსწავლე " +
            "შეტანილი მონაცემით, რომელიც ამიტომ არ წაიშალა. ჯგუფის შეცდომები გენერატორის ლოგშია.",
            [LessonsCategoryKey, ChecksCategoryKey], AcademicYear,
            p => new LessonsWithErrorsReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r14Missings", "გაცდენები",
            "გაცდენების რაოდენობა მოსწავლისა და საგნის მიხედვით, ყველაზე მეტიდან: უკვე დაწყებული გაკვეთილები " +
            "„გაუქმდა\" სტატუსის გარდა.", [LessonsCategoryKey, ChecksCategoryKey], Period,
            p => new MissingsReportQuery(p.StartDate!.Value, p.EndDate!.Value)),
        ReportDefinition.Create("r15BlackList", "შავი სია",
            "ადამიანები (მოსწავლე და გადამხდელი), რომელთა კონტრაქტზე გადახდა „უიმედო ვალის\" ანგარიშით არის " +
            "გატარებული, და ამ გადახდების ჯამი.", [FinanceCategoryKey], NoParameters, _ => new BlackListReportQuery()),
        ReportDefinition.Create("r17MissingsInRow", "ზედიზედ გაცდენები",
            "მოსწავლეები, რომლებმაც ბოლო დასწრების შემდეგ (ან, თუ ჯერ არ დასწრებიან, თავიდან) ორი ან მეტი " +
            "გაკვეთილი გააცდინეს: თარიღისთვის აქტიურ ჯგუფებში, უკვე დაწყებული გაკვეთილებით.",
            [LessonsCategoryKey, ChecksCategoryKey], ForDate, p => new MissingsInRowReportQuery(p.EndDate!.Value)),
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
            p => new TeacherDoubleGroupsReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r22", "კვირის დღეების არასწორი ცვლილებები",
            "გაკვეთილები, რომელთა თეორიულად მინიმალური ან მაქსიმალური თარიღის დრო 00:00:00-ია, ანუ თეორიული " +
            "თარიღები ჯგუფის განრიგით არ არის დათვლილი (კვირის დღეების არასწორი ცვლილების ნიშანი).",
            [LessonsCategoryKey, ChecksCategoryKey], AcademicYear,
            p => new WrongWeekDayChangesReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r23GroupSizesAnalize", "ჯგუფების ზომების ანალიზი",
            "ჯგუფები მოსწავლეების რაოდენობით და შევსების პროცენტით, ზომების მიხედვით; ბოლოს ჯგუფების რაოდენობა, " +
            $"საშუალო დატვირთვა და შევსების საშუალო პროცენტი. {ActiveGroupsNote}", [GroupsCategoryKey, ChecksCategoryKey],
            ForDate, p => new GroupSizesAnalysisReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r24GroupsOptimization", "ჯგუფების ოპტიმიზაციის შესაძლებლობა",
            "ერთი საგნის, მოსწავლეების სტატუსისა და ზომის შეუვსებელი ჯგუფები, რომლებიც იმაზე მეტია, ვიდრე მათ " +
            $"მოსწავლეებს სჭირდება: ჯგუფები მასწავლებლებით და მოსწავლეებით. {ActiveGroupsNote}",
            [GroupsCategoryKey, ChecksCategoryKey], ForDate, p => new GroupsOptimizationReportQuery(p.EndDate!.Value)),
        ReportDefinition.Create("r25TecherSalaryByGroups", "მასწავლებლების გამომუშავებული ხელფასი ჯგუფების მიხედვით",
            "ხელფასების უწყისების გამოთვლის დეტალები: მასწავლებლის საათები, საათის ფასი და ღირებულება ჯგუფების " +
            "მიხედვით, თვისა და მასწავლებლის ჯამებით. თვეები „თარიღიდან\"-ის თვიდან „თარიღამდე\"-ს თვის ჩათვლით.",
            [FinanceCategoryKey], [.. Period, Teacher],
            p => new TeacherSalaryByGroupsReportQuery(p.StartDate!.Value, p.EndDate!.Value, p.TeacherId)),
        ReportDefinition.Create("r26StudMissDate", "მოსწავლეების აცდენილი გადასვლები",
            "მოსწავლის ჯგუფიდან გასვლის თარიღი არ ემთხვევა იმავე საგნის შემდეგი სტრიქონის (სხვა ან იმავე ჯგუფში) " +
            $"დაწყებას: შუალედში მოსწავლეს გაკვეთილი და დარიცხვა არ აქვს. {AllGroupRowsNote}", [ChecksCategoryKey],
            AcademicYear, p => new StudentMissedTransitionsReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r27TeacherMissDate", "მასწავლებლების აცდენილი გადასვლები",
            "ჯგუფში მასწავლებლის სტრიქონის დასრულება არ ემთხვევა ჯგუფის შემდეგი მასწავლებლის სტრიქონის დაწყებას: " +
            $"შუალედში ჯგუფს მასწავლებელი არ ჰყავს და გაკვეთილები არ იქმნება. {AllGroupRowsNote}",
            [ChecksCategoryKey], AcademicYear, p => new TeacherMissedTransitionsReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r28DayTimesMissDate", "დროების აცდენილი გადასვლები",
            "ჯგუფის განრიგის სტრიქონის დასრულება არ ემთხვევა ჯგუფის შემდეგი განრიგის სტრიქონის დაწყებას (კვირის " +
            $"დღის მიუხედავად). {AllGroupRowsNote}", [ChecksCategoryKey], AcademicYear,
            p => new DayTimeMissedTransitionsReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r29TeacherMissSalary", "მასწავლებლების აცდენილი ხელფასები",
            "ჯგუფის მასწავლებლის სტრიქონები, რომელთა ხელფასის სქემა მასწავლებლის კონტრაქტის ძირითადი სქემისგან " +
            $"განსხვავდება (ძირითადი სქემის გარეშე კონტრაქტი არ მოწმდება). {AllGroupRowsNote}", [ChecksCategoryKey],
            AcademicYear, p => new TeacherSchemeMismatchesReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r30StudentMissFees", "მოსწავლის სწავლის საფასურის აცდენა",
            "მოსწავლის 4 კვირის გადასახადი 0.01-ზე მეტით განსხვავდება საათის ღირებულება × კოეფიციენტი × ჯგუფის " +
            "კვირის საათები × 4-ისგან იმ თარიღებზე, როცა მოსწავლის, მასწავლებლის ან განრიგის სტრიქონი იწყება ან " +
            $"მთავრდება. {AllGroupRowsNote}", [ChecksCategoryKey], AcademicYear,
            p => new StudentFeeMismatchesReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r31StudSameStartEndDate",
            "მოსწავლის სწავლის დაწყება და დამთავრება ერთსა და იმავე თარიღზე",
            "ჯგუფის მოსწავლის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს " +
            $"არ მოქმედებს. {AllGroupRowsNote}", [ChecksCategoryKey], AcademicYear,
            p => new StudentSameStartEndDateReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r32TeachSameStartEndDate",
            "მასწავლებლის ჯგუფში მუშაობის დაწყება და დამთავრება ერთსა და იმავე თარიღზე",
            "ჯგუფის მასწავლებლის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს " +
            $"არ მოქმედებს. {AllGroupRowsNote}", [ChecksCategoryKey], AcademicYear,
            p => new TeacherSameStartEndDateReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r33DTPSameStartEndDate",
            "დღეების განაწილება, დაწყებული და დამთავრებული ერთსა და იმავე თარიღზე",
            "ჯგუფის განრიგის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს არ " +
            $"მოქმედებს. {AllGroupRowsNote}", [ChecksCategoryKey], AcademicYear,
            p => new DayTimeSameStartEndDateReportQuery(p.AcademicYearId)),
        ReportDefinition.Create("r34TeacherMissAndSubstitutes", "გაუქმებები და ჩანაცვლებები",
            "გაკვეთილები სტატუსით „გაუქმდა\" ან შემცვლელი მასწავლებლით, გაკვეთილის მასწავლებლების მიხედვით: " +
            "შემთხვევების რაოდენობა და წილი ყველა შემთხვევიდან.", [ChecksCategoryKey], Period,
            p => new TeacherMissAndSubstitutesReportQuery(p.StartDate!.Value, p.EndDate!.Value)),
        //Access-ში r36 მხოლოდ "ყველა"-ში იყო
        ReportDefinition.Create("r36", "სამუშაო დროის აღრიცხვის ფორმა",
            "თანამშრომლების ნამუშევარი საათები თვის დღეების მიხედვით: ჩატარებული გაკვეთილები (გაუქმებულის გარდა, " +
            "აღდგენილი — აღდგენის დღეს) და სამუშაო საათების დასრულებული ჩანაწერები; ერთდროული დრო ერთხელ ითვლება. " +
            "თითო თვე ცალკე, თვის ნახევრების, დღეებისა და საათების ჯამებით.", [], Period,
            p => new TimeSheetReportQuery(p.StartDate!.Value, p.EndDate!.Value))
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
