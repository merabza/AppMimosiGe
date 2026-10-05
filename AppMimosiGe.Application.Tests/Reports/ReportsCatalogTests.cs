using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
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
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule;
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
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportsCatalogTests
{
    private static readonly DateTime Date = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime StartDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

    //part 16; r35TeacherLineOver is not ported (Q9)
    private static readonly string[] Part16Keys =
    [
        "r03RoomsAgenda", "r04StudentsAgenda", "r05TeachersAgenda", "r06RoomOver", "r07UsedDayTimes",
        "r18TeacherOver", "r19StudentOver", "r20StudentDoubleCources", "r21TeacherDoubleGroups"
    ];

    //part 17: the lessons and the checks
    private static readonly string[] PeriodKeys =
    [
        "r11WrongStatuseLessons", "r12LessonsWithWrongVoidStatus", "r14Missings", "r34TeacherMissAndSubstitutes"
    ];

    private static readonly string[] NoParameterKeys = ["r15BlackList"];

    //Access's reports without RepFltNames that check every group row or lesson: an optional academic year (part 20)
    private static readonly string[] AcademicYearKeys =
    [
        "r13LessonsWithErrors", "r22", "r26StudMissDate", "r27TeacherMissDate", "r28DayTimesMissDate",
        "r29TeacherMissSalary", "r30StudentMissFees", "r31StudSameStartEndDate", "r32TeachSameStartEndDate",
        "r33DTPSameStartEndDate"
    ];

    private const string AllGroupRows =
        "არჩეული სასწავლო წლის (ცარიელი — ყველა წლის) ყველა ჯგუფის ყველა სტრიქონი, თარიღების მიუხედავად.";

    //part 18: the groups' state for one date (r09 had no filter in Access, D132)
    private static readonly string[] Part18OneDateKeys =
    [
        "r08LessSizeGroups", "r09Optimization", "r23GroupSizesAnalize", "r24GroupsOptimization"
    ];

    private static void AssertKeys(IEnumerable<string> actual, params string[] expected)
    {
        Assert.Equal(expected, actual);
    }

    // Access's ReportName keys in the order of Access's ids; r02 (SummaryComments, D8) and r35 (Q9) are not ported
    [Fact]
    public void Definitions_AreTheReportsInAccessOrder()
    {
        AssertKeys(ReportsCatalog.Definitions.Select(d => d.Key), "r01Comments", "r03RoomsAgenda",
            "r04StudentsAgenda", "r05TeachersAgenda", "r06RoomOver", "r07UsedDayTimes", "r08LessSizeGroups",
            "r09Optimization", "r10Groups", "r11WrongStatuseLessons", "r12LessonsWithWrongVoidStatus",
            "r13LessonsWithErrors", "r14Missings", "r15BlackList", "r17MissingsInRow", "r18TeacherOver",
            "r19StudentOver", "r20StudentDoubleCources", "r21TeacherDoubleGroups", "r22", "r23GroupSizesAnalize",
            "r24GroupsOptimization", "r25TecherSalaryByGroups", "r26StudMissDate", "r27TeacherMissDate",
            "r28DayTimesMissDate", "r29TeacherMissSalary", "r30StudentMissFees", "r31StudSameStartEndDate",
            "r32TeachSameStartEndDate", "r33DTPSameStartEndDate", "r34TeacherMissAndSubstitutes", "r36");
    }

    [Fact]
    public void Definitions_KeysAreUniqueIgnoringCase()
    {
        Assert.Equal(ReportsCatalog.Definitions.Count,
            ReportsCatalog.Definitions.Select(d => d.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // every report but r36 has a category of its own (in Access r36 was only in "all")
    [Fact]
    public void Definitions_HaveTitlesDescriptionsAndKnownCategories()
    {
        HashSet<string> categoryKeys = [.. ReportsCatalog.Categories.Select(c => c.Key)];
        Assert.All(ReportsCatalog.Definitions, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.False(string.IsNullOrWhiteSpace(d.Description));
            Assert.Equal(d.Key == "r36", d.CategoryKeys.Count == 0);
            //"all" holds every report by itself
            Assert.DoesNotContain(ReportsCatalog.AllCategoryKey, d.CategoryKeys);
            Assert.All(d.CategoryKeys, key => Assert.Contains(key, categoryKeys));
        });
    }

    // the schedule reports, r17 and the part 18 group reports show the state for one date: "თარიღისთვის", required
    [Fact]
    public void Definitions_OneDateReportsHaveTheRequiredEndDate()
    {
        Assert.All(
            ReportsCatalog.Definitions.Where(d =>
                Part16Keys.Contains(d.Key) || Part18OneDateKeys.Contains(d.Key) || d.Key == "r17MissingsInRow"),
            d => Assert.Equal(new ReportParameter(ReportParameterNames.EndDate, "თარიღისთვის", true),
                Assert.Single(d.Parameters)));
        Assert.Equal(14, ReportsCatalog.Definitions.Count(d =>
            Part16Keys.Contains(d.Key) || Part18OneDateKeys.Contains(d.Key) || d.Key == "r17MissingsInRow"));
    }

    // Access's StartDate, EndDate reports: both required
    [Fact]
    public void Definitions_PeriodReportsHaveTheRequiredDates()
    {
        string[] keys = [.. PeriodKeys, "r36"];
        Assert.All(keys, key => Assert.Equal([
            new ReportParameter(ReportParameterNames.StartDate, "თარიღიდან", true),
            new ReportParameter(ReportParameterNames.EndDate, "თარიღამდე", true)
        ], ReportsCatalog.Find(key)!.Parameters));
    }

    // r01: the month of the date (required) and Access's optional teacher
    [Fact]
    public void Definitions_CommentsHaveTheMonthAndTheOptionalTeacher()
    {
        Assert.Equal([
            new ReportParameter(ReportParameterNames.EndDate, "თვე", true),
            new ReportParameter(ReportParameterNames.TeacherId, "მასწავლებელი", false)
        ], ReportsCatalog.Find("r01Comments")!.Parameters);
    }

    // r10: the date and Access's three optional filters (an empty one is "all")
    [Fact]
    public void Definitions_GroupsHaveTheDateAndThreeOptionalFilters()
    {
        Assert.Equal([
            new ReportParameter(ReportParameterNames.EndDate, "თარიღისთვის", true),
            new ReportParameter(ReportParameterNames.TeacherId, "მასწავლებელი", false),
            new ReportParameter(ReportParameterNames.CourseId, "საგანი", false),
            new ReportParameter(ReportParameterNames.StudentId, "მოსწავლე", false)
        ], ReportsCatalog.Find("r10Groups")!.Parameters);
    }

    // r25: the period and the optional teacher
    [Fact]
    public void Definitions_SalaryByGroupsHasThePeriodAndTheOptionalTeacher()
    {
        Assert.Equal([
            new ReportParameter(ReportParameterNames.StartDate, "თარიღიდან", true),
            new ReportParameter(ReportParameterNames.EndDate, "თარიღამდე", true),
            new ReportParameter(ReportParameterNames.TeacherId, "მასწავლებელი", false)
        ], ReportsCatalog.Find("r25TecherSalaryByGroups")!.Parameters);
    }

    // AppMimosiGeMenu.txt's titles
    [Theory]
    [InlineData("r01Comments", "დღის კომენტარები 1 თვისთვის")]
    [InlineData("r08LessSizeGroups", "შეუვსებელი ჯგუფები")]
    [InlineData("r09Optimization", "ოპტიმიზაციის შესაძლებლობა")]
    [InlineData("r10Groups", "ჯგუფები")]
    [InlineData("r15BlackList", "შავი სია")]
    [InlineData("r23GroupSizesAnalize", "ჯგუფების ზომების ანალიზი")]
    [InlineData("r24GroupsOptimization", "ჯგუფების ოპტიმიზაციის შესაძლებლობა")]
    [InlineData("r25TecherSalaryByGroups", "მასწავლებლების გამომუშავებული ხელფასი ჯგუფების მიხედვით")]
    [InlineData("r36", "სამუშაო დროის აღრიცხვის ფორმა")]
    public void Definitions_Part18TitlesAreThoseOfTheMenu(string key, string title)
    {
        Assert.Equal(title, ReportsCatalog.Find(key)!.Title);
    }

    // the description says what the report shows; the page shows it under the report's title
    [Theory]
    [InlineData("r01Comments",
        "თვის გაკვეთილები ჯგუფებით: მოსწავლეები (გაკვეთილში დანომრილი) ჟურნალში შეტანილი კომენტარებით, აღდგენის " +
        "თარიღი და შემცვლელი მასწავლებელი; შეუვსებელი ადგილი ცარიელია ხელით შესავსებად. „თვე\" — არჩეული " +
        "თარიღის თვე; მასწავლებლით მხოლოდ მისი გაკვეთილები.")]
    [InlineData("r08LessSizeGroups",
        "ჯგუფები, რომლებშიც მოსწავლეები ადგილებზე (ჯგუფის ზომაზე) ნაკლებია, ზომების მიხედვით. მხოლოდ " +
        "თარიღისთვის აქტიური ჯგუფები: მოქმედი მოსწავლით, მასწავლებლით და განრიგით, გაუქმების გარეშე.")]
    [InlineData("r09Optimization",
        "შეუვსებელი ჯგუფების წყვილები ერთი საგნით, მოსწავლეების სტატუსით და ზომით, რომელთა მოსწავლეები ერთ " +
        "ჯგუფში ეტევა. მხოლოდ თარიღისთვის აქტიური ჯგუფები: მოქმედი მოსწავლით, მასწავლებლით და განრიგით, " +
        "გაუქმების გარეშე.")]
    [InlineData("r10Groups",
        "ჯგუფები მასწავლებლებით (სქემა, დაწყება) და მოსწავლეებით (4 კვირის საათები, გადასახადი, კოეფიციენტი, " +
        "დაწყება). მასწავლებელი, საგანი და მოსწავლე არასავალდებულო ფილტრებია: მასწავლებლით მხოლოდ ის ჯგუფები, " +
        "სადაც ის ასწავლის, მოსწავლით — სადაც ის სწავლობს. მხოლოდ თარიღისთვის აქტიური ჯგუფები: მოქმედი " +
        "მოსწავლით, მასწავლებლით და განრიგით, გაუქმების გარეშე.")]
    [InlineData("r15BlackList",
        "ადამიანები (მოსწავლე და გადამხდელი), რომელთა კონტრაქტზე გადახდა „უიმედო ვალის\" ანგარიშით არის " +
        "გატარებული, და ამ გადახდების ჯამი.")]
    [InlineData("r23GroupSizesAnalize",
        "ჯგუფები მოსწავლეების რაოდენობით და შევსების პროცენტით, ზომების მიხედვით; ბოლოს ჯგუფების რაოდენობა, " +
        "საშუალო დატვირთვა და შევსების საშუალო პროცენტი. მხოლოდ თარიღისთვის აქტიური ჯგუფები: მოქმედი მოსწავლით, " +
        "მასწავლებლით და განრიგით, გაუქმების გარეშე.")]
    [InlineData("r24GroupsOptimization",
        "ერთი საგნის, მოსწავლეების სტატუსისა და ზომის შეუვსებელი ჯგუფები, რომლებიც იმაზე მეტია, ვიდრე მათ " +
        "მოსწავლეებს სჭირდება: ჯგუფები მასწავლებლებით და მოსწავლეებით. მხოლოდ თარიღისთვის აქტიური ჯგუფები: " +
        "მოქმედი მოსწავლით, მასწავლებლით და განრიგით, გაუქმების გარეშე.")]
    [InlineData("r25TecherSalaryByGroups",
        "ხელფასების უწყისების გამოთვლის დეტალები: მასწავლებლის საათები, საათის ფასი და ღირებულება ჯგუფების " +
        "მიხედვით, თვისა და მასწავლებლის ჯამებით. თვეები „თარიღიდან\"-ის თვიდან „თარიღამდე\"-ს თვის ჩათვლით.")]
    [InlineData("r36",
        "თანამშრომლების ნამუშევარი საათები თვის დღეების მიხედვით: ჩატარებული გაკვეთილები (გაუქმებულის გარდა, " +
        "აღდგენილი — აღდგენის დღეს) და სამუშაო საათების დასრულებული ჩანაწერები; ერთდროული დრო ერთხელ ითვლება. " +
        "თითო თვე ცალკე, თვის ნახევრების, დღეებისა და საათების ჯამებით.")]
    public void Definitions_Part18DescriptionsSayWhatTheReportShows(string key, string description)
    {
        Assert.Equal(description, ReportsCatalog.Find(key)!.Description);
    }

    // Access's ReportsByCategories for the part 18 reports; r36 was only in "all"
    [Theory]
    [InlineData("r01Comments", "comments")]
    [InlineData("r08LessSizeGroups", "groups", "checks")]
    [InlineData("r09Optimization", "groups", "checks")]
    [InlineData("r10Groups", "groups")]
    [InlineData("r15BlackList", "finance")]
    [InlineData("r23GroupSizesAnalize", "groups", "checks")]
    [InlineData("r24GroupsOptimization", "groups", "checks")]
    [InlineData("r25TecherSalaryByGroups", "finance")]
    [InlineData("r36")]
    public void Definitions_Part18CategoriesAreThoseOfAccess(string key, params string[] categoryKeys)
    {
        Assert.Equal(categoryKeys, ReportsCatalog.Find(key)!.CategoryKeys);
    }

    // a report without a category of its own is listed under "all" only
    [Fact]
    public void ForClaims_ReportWithoutCategory_IsUnderAllOnly()
    {
        // Act
        ReportCatalogResponse catalog = ReportsCatalog.ForClaims(new HashSet<string> { "r36" });

        // Assert
        ReportCategoryResponse category = Assert.Single(catalog.Categories);
        Assert.Equal("all", category.Key);
        AssertKeys(category.ReportKeys, "r36");
    }

    // Access's reports without RepFltNames
    [Fact]
    public void Definitions_CheckReportsHaveNoParameters()
    {
        Assert.All(NoParameterKeys, key => Assert.Empty(ReportsCatalog.Find(key)!.Parameters));
    }

    [Fact]
    public void Definitions_GroupRowChecksHaveAnOptionalAcademicYear()
    {
        Assert.All(AcademicYearKeys,
            key => Assert.Equal(
                [new ReportParameter(ReportParameterNames.AcademicYearId, ReportParameterCaptions.AcademicYear, false)],
                ReportsCatalog.Find(key)!.Parameters));
    }

    // AppMimosiGeMenu.txt's titles, the typos fixed ("აცდელინი", "ერთიდაიგივე")
    [Theory]
    [InlineData("r11WrongStatuseLessons", "გასაუქმებელი გაკვეთილები")]
    [InlineData("r12LessonsWithWrongVoidStatus", "არასწორად გაუქმებული გაკვეთილები")]
    [InlineData("r13LessonsWithErrors", "შეცდომიანი გაკვეთილები")]
    [InlineData("r14Missings", "გაცდენები")]
    [InlineData("r17MissingsInRow", "ზედიზედ გაცდენები")]
    [InlineData("r22", "კვირის დღეების არასწორი ცვლილებები")]
    [InlineData("r26StudMissDate", "მოსწავლეების აცდენილი გადასვლები")]
    [InlineData("r27TeacherMissDate", "მასწავლებლების აცდენილი გადასვლები")]
    [InlineData("r28DayTimesMissDate", "დროების აცდენილი გადასვლები")]
    [InlineData("r29TeacherMissSalary", "მასწავლებლების აცდენილი ხელფასები")]
    [InlineData("r30StudentMissFees", "მოსწავლის სწავლის საფასურის აცდენა")]
    [InlineData("r31StudSameStartEndDate", "მოსწავლის სწავლის დაწყება და დამთავრება ერთსა და იმავე თარიღზე")]
    [InlineData("r32TeachSameStartEndDate",
        "მასწავლებლის ჯგუფში მუშაობის დაწყება და დამთავრება ერთსა და იმავე თარიღზე")]
    [InlineData("r33DTPSameStartEndDate", "დღეების განაწილება, დაწყებული და დამთავრებული ერთსა და იმავე თარიღზე")]
    [InlineData("r34TeacherMissAndSubstitutes", "გაუქმებები და ჩანაცვლებები")]
    public void Definitions_Part17TitlesAreThoseOfTheMenu(string key, string title)
    {
        Assert.Equal(title, ReportsCatalog.Find(key)!.Title);
    }

    // the description says what the report checks (D119); the page shows it under the report's title
    [Theory]
    [InlineData("r11WrongStatuseLessons",
        "გაკვეთილები სტატუსით „არ გაუქმებულა\", რომლებსაც არცერთი მოსწავლე არ დასწრებია: ან გაუქმებულად უნდა " +
        "მოინიშნოს, ან დასწრება შეივსოს. მხოლოდ უკვე დაწყებული გაკვეთილები.")]
    [InlineData("r12LessonsWithWrongVoidStatus",
        "გაუქმებული გაკვეთილები (სტატუსი „არ გაუქმებულა\"-ს გარდა), რომლებსაც დამსწრე მოსწავლე ან აღდგენის " +
        "თარიღი აქვს.")]
    [InlineData("r13LessonsWithErrors",
        "გაკვეთილები, რომლებზეც გენერატორმა ბოლო გაშვებისას შეცდომა ჩაწერა: ზედმეტი გაკვეთილი ან მოსწავლე " +
        "შეტანილი მონაცემით, რომელიც ამიტომ არ წაიშალა. ჯგუფის შეცდომები გენერატორის ლოგშია.")]
    [InlineData("r14Missings",
        "გაცდენების რაოდენობა მოსწავლისა და საგნის მიხედვით, ყველაზე მეტიდან: უკვე დაწყებული გაკვეთილები " +
        "„გაუქმდა\" სტატუსის გარდა.")]
    [InlineData("r17MissingsInRow",
        "მოსწავლეები, რომლებმაც ბოლო დასწრების შემდეგ (ან, თუ ჯერ არ დასწრებიან, თავიდან) ორი ან მეტი " +
        "გაკვეთილი გააცდინეს: თარიღისთვის აქტიურ ჯგუფებში, უკვე დაწყებული გაკვეთილებით.")]
    [InlineData("r22",
        "გაკვეთილები, რომელთა თეორიულად მინიმალური ან მაქსიმალური თარიღის დრო 00:00:00-ია, ანუ თეორიული " +
        "თარიღები ჯგუფის განრიგით არ არის დათვლილი (კვირის დღეების არასწორი ცვლილების ნიშანი).")]
    [InlineData("r26StudMissDate",
        "მოსწავლის ჯგუფიდან გასვლის თარიღი არ ემთხვევა იმავე საგნის შემდეგი სტრიქონის (სხვა ან იმავე ჯგუფში) " +
        "დაწყებას: შუალედში მოსწავლეს გაკვეთილი და დარიცხვა არ აქვს. " + AllGroupRows)]
    [InlineData("r27TeacherMissDate",
        "ჯგუფში მასწავლებლის სტრიქონის დასრულება არ ემთხვევა ჯგუფის შემდეგი მასწავლებლის სტრიქონის დაწყებას: " +
        "შუალედში ჯგუფს მასწავლებელი არ ჰყავს და გაკვეთილები არ იქმნება. " + AllGroupRows)]
    [InlineData("r28DayTimesMissDate",
        "ჯგუფის განრიგის სტრიქონის დასრულება არ ემთხვევა ჯგუფის შემდეგი განრიგის სტრიქონის დაწყებას (კვირის " +
        "დღის მიუხედავად). " + AllGroupRows)]
    [InlineData("r29TeacherMissSalary",
        "ჯგუფის მასწავლებლის სტრიქონები, რომელთა ხელფასის სქემა მასწავლებლის კონტრაქტის ძირითადი სქემისგან " +
        "განსხვავდება (ძირითადი სქემის გარეშე კონტრაქტი არ მოწმდება). " + AllGroupRows)]
    [InlineData("r30StudentMissFees",
        "მოსწავლის 4 კვირის გადასახადი 0.01-ზე მეტით განსხვავდება საათის ღირებულება × კოეფიციენტი × ჯგუფის " +
        "კვირის საათები × 4-ისგან იმ თარიღებზე, როცა მოსწავლის, მასწავლებლის ან განრიგის სტრიქონი იწყება ან " +
        "მთავრდება. " + AllGroupRows)]
    [InlineData("r31StudSameStartEndDate",
        "ჯგუფის მოსწავლის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს " +
        "არ მოქმედებს. " + AllGroupRows)]
    [InlineData("r32TeachSameStartEndDate",
        "ჯგუფის მასწავლებლის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს " +
        "არ მოქმედებს. " + AllGroupRows)]
    [InlineData("r33DTPSameStartEndDate",
        "ჯგუფის განრიგის სტრიქონები, რომლებიც ერთსა და იმავე თარიღზე იწყება და მთავრდება, ანუ არცერთ დღეს არ " +
        "მოქმედებს. " + AllGroupRows)]
    [InlineData("r34TeacherMissAndSubstitutes",
        "გაკვეთილები სტატუსით „გაუქმდა\" ან შემცვლელი მასწავლებლით, გაკვეთილის მასწავლებლების მიხედვით: " +
        "შემთხვევების რაოდენობა და წილი ყველა შემთხვევიდან.")]
    public void Definitions_Part17DescriptionsSayWhatTheReportChecks(string key, string description)
    {
        Assert.Equal(description, ReportsCatalog.Find(key)!.Description);
    }

    // Access's ReportCategories, in the order of their ids
    [Fact]
    public void Categories_AreTheAccessCategories()
    {
        Assert.Equal([
            new ReportCategory("all", "ყველა"), new ReportCategory("schedule", "გაკვეთილების ცხრილი"),
            new ReportCategory("groups", "ჯგუფები"), new ReportCategory("comments", "კომენტარები"),
            new ReportCategory("lessons", "გაკვეთილები"), new ReportCategory("finance", "ფინანსები"),
            new ReportCategory("checks", "შემოწმება")
        ], ReportsCatalog.Categories);
    }

    // Access's ReportsByCategories for the part 16 reports
    [Theory]
    [InlineData("r03RoomsAgenda", false)]
    [InlineData("r04StudentsAgenda", false)]
    [InlineData("r05TeachersAgenda", false)]
    [InlineData("r06RoomOver", true)]
    [InlineData("r07UsedDayTimes", true)]
    [InlineData("r18TeacherOver", true)]
    [InlineData("r19StudentOver", true)]
    [InlineData("r20StudentDoubleCources", true)]
    [InlineData("r21TeacherDoubleGroups", true)]
    public void Definitions_CategoriesAreThoseOfAccess(string key, bool isCheck)
    {
        string[] expected = isCheck ? ["schedule", "checks"] : ["schedule"];
        Assert.Equal(expected, ReportsCatalog.Find(key)!.CategoryKeys);
    }

    // Access's ReportsByCategories for the part 17 reports: the lessons' checks are in both categories
    [Theory]
    [InlineData("r11WrongStatuseLessons", true)]
    [InlineData("r12LessonsWithWrongVoidStatus", true)]
    [InlineData("r13LessonsWithErrors", true)]
    [InlineData("r14Missings", true)]
    [InlineData("r17MissingsInRow", true)]
    [InlineData("r22", true)]
    [InlineData("r26StudMissDate", false)]
    [InlineData("r27TeacherMissDate", false)]
    [InlineData("r28DayTimesMissDate", false)]
    [InlineData("r29TeacherMissSalary", false)]
    [InlineData("r30StudentMissFees", false)]
    [InlineData("r31StudSameStartEndDate", false)]
    [InlineData("r32TeachSameStartEndDate", false)]
    [InlineData("r33DTPSameStartEndDate", false)]
    [InlineData("r34TeacherMissAndSubstitutes", false)]
    public void Definitions_Part17CategoriesAreThoseOfAccess(string key, bool isLessons)
    {
        string[] expected = isLessons ? ["lessons", "checks"] : ["checks"];
        Assert.Equal(expected, ReportsCatalog.Find(key)!.CategoryKeys);
    }

    [Theory]
    [InlineData("r03RoomsAgenda")]
    [InlineData("R03ROOMSAGENDA")]
    public void Find_IgnoresTheCase(string key)
    {
        Assert.Equal("r03RoomsAgenda", ReportsCatalog.Find(key)?.Key);
    }

    // the key is the ReportName of Access's Reports table ("r34…"), the Access report object was "R34…"
    [Fact]
    public void Find_R34ReportObjectName_IsTheR34Report()
    {
        Assert.Equal("r34TeacherMissAndSubstitutes", ReportsCatalog.Find("R34TeacherMissAndSubstitutes")?.Key);
    }

    [Theory]
    [InlineData("r35TeacherLineOver")]
    [InlineData("catalog")]
    [InlineData("")]
    public void Find_UnknownKey_IsNull(string key)
    {
        Assert.Null(ReportsCatalog.Find(key));
    }

    // only the reports whose app claim the user has, and only the categories that have one of them
    [Fact]
    public void ForClaims_ShowsTheClaimedReportsAndTheirCategories()
    {
        // Act
        ReportCatalogResponse catalog =
            ReportsCatalog.ForClaims(new HashSet<string> { "r06RoomOver", "CheckPayments", "r03RoomsAgenda" });

        // Assert
        AssertKeys(catalog.Reports.Select(r => r.Key), "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories.Select(c => c.Key), "all", "schedule", "checks");
        AssertKeys(catalog.Categories[0].ReportKeys, "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories[1].ReportKeys, "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories[2].ReportKeys, "r06RoomOver");
        Assert.Equal("შემოწმება", catalog.Categories[2].Name);
        ReportInfoResponse report = catalog.Reports[0];
        Assert.Equal("ოთახების ცხრილი", report.Title);
        Assert.Equal(ReportsCatalog.Find("r03RoomsAgenda")!.Description, report.Description);
        Assert.Equal(new ReportParameterInfoResponse(ReportParameterNames.EndDate, "თარიღისთვის", true),
            Assert.Single(report.Parameters));
    }

    // claims are compared exactly, as the carcass stores them
    [Fact]
    public void ForClaims_ClaimInAnotherCase_DoesNotShowTheReport()
    {
        Assert.Empty(ReportsCatalog.ForClaims(new HashSet<string> { "R03ROOMSAGENDA" }).Reports);
    }

    [Fact]
    public void ForClaims_NoClaims_IsEmpty()
    {
        // Act
        ReportCatalogResponse catalog = ReportsCatalog.ForClaims(new HashSet<string>());

        // Assert
        Assert.Empty(catalog.Reports);
        Assert.Empty(catalog.Categories);
    }

    // every report calls its own query handler with the end date
    [Theory]
    [InlineData("r03RoomsAgenda", typeof(RoomsAgendaReportQuery))]
    [InlineData("r04StudentsAgenda", typeof(StudentsAgendaReportQuery))]
    [InlineData("r05TeachersAgenda", typeof(TeachersAgendaReportQuery))]
    [InlineData("r06RoomOver", typeof(RoomOverlapsReportQuery))]
    [InlineData("r07UsedDayTimes", typeof(UsedDayTimesReportQuery))]
    [InlineData("r18TeacherOver", typeof(TeacherOverlapsReportQuery))]
    [InlineData("r19StudentOver", typeof(StudentOverlapsReportQuery))]
    [InlineData("r20StudentDoubleCources", typeof(StudentDoubleCoursesReportQuery))]
    [InlineData("r21TeacherDoubleGroups", typeof(TeacherDoubleGroupsReportQuery))]
    public async Task Run_CallsTheReportsOwnHandlerWithTheEndDate(string key, Type queryType)
    {
        // Arrange
        var services = new ServiceCollection();
        List<object> queries = [];
        Dictionary<Type, ReportTable> tables = [];
        Add<RoomsAgendaReportQuery>();
        Add<StudentsAgendaReportQuery>();
        Add<TeachersAgendaReportQuery>();
        Add<RoomOverlapsReportQuery>();
        Add<UsedDayTimesReportQuery>();
        Add<TeacherOverlapsReportQuery>();
        Add<StudentOverlapsReportQuery>();
        Add<StudentDoubleCoursesReportQuery>();
        Add<TeacherDoubleGroupsReportQuery>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        Result<ReportTable> result = await ReportsCatalog.Find(key)!.Run(
            new ReportParametersRequest(null, Date, null, null, null), provider, CancellationToken.None);

        // Assert
        Assert.Same(tables[queryType], result.Value);
        object query = Assert.Single(queries);
        Assert.IsType(queryType, query);
        Assert.Equal(Date, ((ScheduleReportQuery)query).Date);
        return;

        void Add<TQuery>() where TQuery : IQuery<ReportTable>
        {
            ReportTable table = ReportTable.Flat([new ReportColumnResponse(typeof(TQuery).Name, "", "text")], []);
            tables[typeof(TQuery)] = table;
            var handler = new Mock<IQueryHandler<TQuery, ReportTable>>();
            handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>()))
                .Callback<TQuery, CancellationToken>((q, _) => queries.Add(q)).ReturnsAsync(table);
            services.AddSingleton(handler.Object);
        }
    }

    //the query a part 17 report's handler gets for the period StartDate–Date (r17: the date only) and the academic
    //year 11 (the checks without dates, part 20)
    private static object Part17Query(string key)
    {
        return key switch
        {
            "r11WrongStatuseLessons" => new WrongStatusLessonsReportQuery(StartDate, Date),
            "r12LessonsWithWrongVoidStatus" => new LessonsWithWrongVoidStatusReportQuery(StartDate, Date),
            "r13LessonsWithErrors" => new LessonsWithErrorsReportQuery(11),
            "r14Missings" => new MissingsReportQuery(StartDate, Date),
            "r17MissingsInRow" => new MissingsInRowReportQuery(Date),
            "r22" => new WrongWeekDayChangesReportQuery(11),
            "r26StudMissDate" => new StudentMissedTransitionsReportQuery(11),
            "r27TeacherMissDate" => new TeacherMissedTransitionsReportQuery(11),
            "r28DayTimesMissDate" => new DayTimeMissedTransitionsReportQuery(11),
            "r29TeacherMissSalary" => new TeacherSchemeMismatchesReportQuery(11),
            "r30StudentMissFees" => new StudentFeeMismatchesReportQuery(11),
            "r31StudSameStartEndDate" => new StudentSameStartEndDateReportQuery(11),
            "r32TeachSameStartEndDate" => new TeacherSameStartEndDateReportQuery(11),
            "r33DTPSameStartEndDate" => new DayTimeSameStartEndDateReportQuery(11),
            _ => new TeacherMissAndSubstitutesReportQuery(StartDate, Date)
        };
    }

    //the query a part 18 report's handler gets for the period StartDate–Date and the filters teacher 5, course 6,
    //student 7 (each report takes only its own parameters)
    private static object Part18Query(string key)
    {
        return key switch
        {
            "r01Comments" => new DailyCommentsReportQuery(Date, 5),
            "r08LessSizeGroups" => new LessSizeGroupsReportQuery(Date),
            "r09Optimization" => new MergeableGroupPairsReportQuery(Date),
            "r10Groups" => new GroupListReportQuery(Date, 5, 6, 7),
            "r15BlackList" => new BlackListReportQuery(),
            "r23GroupSizesAnalize" => new GroupSizesAnalysisReportQuery(Date),
            "r24GroupsOptimization" => new GroupsOptimizationReportQuery(Date),
            "r25TecherSalaryByGroups" => new TeacherSalaryByGroupsReportQuery(StartDate, Date, 5),
            _ => new TimeSheetReportQuery(StartDate, Date)
        };
    }

    // every part 18 report calls its own query handler with its parameters
    [Theory]
    [InlineData("r01Comments")]
    [InlineData("r08LessSizeGroups")]
    [InlineData("r09Optimization")]
    [InlineData("r10Groups")]
    [InlineData("r15BlackList")]
    [InlineData("r23GroupSizesAnalize")]
    [InlineData("r24GroupsOptimization")]
    [InlineData("r25TecherSalaryByGroups")]
    [InlineData("r36")]
    public async Task Run_CallsThePart18ReportsOwnHandler(string key)
    {
        // Arrange
        var services = new ServiceCollection();
        List<object> queries = [];
        Dictionary<Type, ReportTable> tables = [];
        Add<DailyCommentsReportQuery>();
        Add<LessSizeGroupsReportQuery>();
        Add<MergeableGroupPairsReportQuery>();
        Add<GroupListReportQuery>();
        Add<BlackListReportQuery>();
        Add<GroupSizesAnalysisReportQuery>();
        Add<GroupsOptimizationReportQuery>();
        Add<TeacherSalaryByGroupsReportQuery>();
        Add<TimeSheetReportQuery>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        object expected = Part18Query(key);

        // Act
        Result<ReportTable> result = await ReportsCatalog.Find(key)!.Run(
            new ReportParametersRequest(StartDate, Date, 5, 6, 7), provider, CancellationToken.None);

        // Assert
        Assert.Same(tables[expected.GetType()], result.Value);
        Assert.Equal(expected, Assert.Single(queries));
        return;

        void Add<TQuery>() where TQuery : IQuery<ReportTable>
        {
            ReportTable table = ReportTable.Flat([new ReportColumnResponse(typeof(TQuery).Name, "", "text")], []);
            tables[typeof(TQuery)] = table;
            var handler = new Mock<IQueryHandler<TQuery, ReportTable>>();
            handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>()))
                .Callback<TQuery, CancellationToken>((q, _) => queries.Add(q)).ReturnsAsync(table);
            services.AddSingleton(handler.Object);
        }
    }

    // the optional filters stay empty when the request has none
    [Fact]
    public async Task Run_GroupsWithoutFilters_PassesNoFilter()
    {
        // Arrange
        var services = new ServiceCollection();
        GroupListReportQuery? query = null;
        var handler = new Mock<IQueryHandler<GroupListReportQuery, ReportTable>>();
        handler.Setup(h => h.Handle(It.IsAny<GroupListReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GroupListReportQuery, CancellationToken>((q, _) => query = q)
            .ReturnsAsync(ReportTable.Flat([], []));
        services.AddSingleton(handler.Object);
        await using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        await ReportsCatalog.Find("r10Groups")!.Run(new ReportParametersRequest(null, Date, null, null, null),
            provider, CancellationToken.None);

        // Assert
        Assert.Equal(new GroupListReportQuery(Date, null, null, null), query);
    }

    // every part 17 report calls its own query handler with its dates
    [Theory]
    [InlineData("r11WrongStatuseLessons")]
    [InlineData("r12LessonsWithWrongVoidStatus")]
    [InlineData("r13LessonsWithErrors")]
    [InlineData("r14Missings")]
    [InlineData("r17MissingsInRow")]
    [InlineData("r22")]
    [InlineData("r26StudMissDate")]
    [InlineData("r27TeacherMissDate")]
    [InlineData("r28DayTimesMissDate")]
    [InlineData("r29TeacherMissSalary")]
    [InlineData("r30StudentMissFees")]
    [InlineData("r31StudSameStartEndDate")]
    [InlineData("r32TeachSameStartEndDate")]
    [InlineData("r33DTPSameStartEndDate")]
    [InlineData("r34TeacherMissAndSubstitutes")]
    public async Task Run_CallsThePart17ReportsOwnHandler(string key)
    {
        // Arrange
        var services = new ServiceCollection();
        List<object> queries = [];
        Dictionary<Type, ReportTable> tables = [];
        Add<WrongStatusLessonsReportQuery>();
        Add<LessonsWithWrongVoidStatusReportQuery>();
        Add<LessonsWithErrorsReportQuery>();
        Add<MissingsReportQuery>();
        Add<MissingsInRowReportQuery>();
        Add<WrongWeekDayChangesReportQuery>();
        Add<StudentMissedTransitionsReportQuery>();
        Add<TeacherMissedTransitionsReportQuery>();
        Add<DayTimeMissedTransitionsReportQuery>();
        Add<TeacherSchemeMismatchesReportQuery>();
        Add<StudentFeeMismatchesReportQuery>();
        Add<StudentSameStartEndDateReportQuery>();
        Add<TeacherSameStartEndDateReportQuery>();
        Add<DayTimeSameStartEndDateReportQuery>();
        Add<TeacherMissAndSubstitutesReportQuery>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        object expected = Part17Query(key);

        // Act
        Result<ReportTable> result = await ReportsCatalog.Find(key)!.Run(
            new ReportParametersRequest(StartDate, Date, null, null, null, 11), provider, CancellationToken.None);

        // Assert
        Assert.Same(tables[expected.GetType()], result.Value);
        Assert.Equal(expected, Assert.Single(queries));
        return;

        void Add<TQuery>() where TQuery : IQuery<ReportTable>
        {
            ReportTable table = ReportTable.Flat([new ReportColumnResponse(typeof(TQuery).Name, "", "text")], []);
            tables[typeof(TQuery)] = table;
            var handler = new Mock<IQueryHandler<TQuery, ReportTable>>();
            handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>()))
                .Callback<TQuery, CancellationToken>((q, _) => queries.Add(q)).ReturnsAsync(table);
            services.AddSingleton(handler.Object);
        }
    }
}
