using System;
using System.Linq;
using System.Reflection;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupErrorsTests
{
    private const string LessonsWithErrorsStart =
        "ცვლილება შეცდომიან გაკვეთილებს შექმნიდა (რეპორტი „შეცდომიანი გაკვეთილები\"): ეს გაკვეთილები ან მათი " +
        "ზოგიერთი მოსწავლე ზედმეტი გახდებოდა, ჟურნალში კი დასწრება, თემა, შეფასება ან კომენტარი აქვთ შეტანილი, " +
        "ამიტომ გენერატორი მათ ვეღარ წაშლის: ";

    private const string LessonsWithErrorsEnd =
        ". შეასწორეთ თარიღები ან ჯერ ჟურნალში გაასუფთავეთ ამ გაკვეთილების მონაცემები";

    private const string TeacherTimesOverlapStart =
        "ჯგუფი არ შეინახება: მის მასწავლებელს იმავე დროს სხვა ჯგუფშიც აქვს გაკვეთილი (რეპორტი „მასწავლებლების " +
        "დროების გადაფარვა\"): ";

    private const string TeacherTimesOverlapEnd = ". შეასწორეთ მასწავლებლების ან განრიგის თარიღები, დრო ან კვირის დღე";

    private const string StudentTimesOverlapStart =
        "ჯგუფი არ შეინახება: მის მოსწავლეს იმავე დროს სხვა ჯგუფშიც აქვს გაკვეთილი (რეპორტი „მოსწავლეების " +
        "დროების გადაფარვა\"): ";

    private const string StudentTimesOverlapEnd = ". შეასწორეთ მოსწავლეების ან განრიგის თარიღები, დრო ან კვირის დღე";

    //every error property of the group editor with its type and the exact text the user sees
    public static readonly TheoryData<Error, ErrorType, string> FixedErrors = new()
    {
        { GroupErrors.GroupNotFound, ErrorType.NotFound, "ჯგუფი ვერ მოიძებნა" },
        { GroupErrors.GroupCodeIsRequired, ErrorType.Problem, "ჯგუფის კოდი შევსებული უნდა იყოს" },
        { GroupErrors.GroupCodeIsTooLong, ErrorType.Problem, "ჯგუფის კოდი 5 სიმბოლოზე გრძელი ვერ იქნება" },
        {
            GroupErrors.GroupCodeAlreadyExists, ErrorType.Conflict,
            "ამ სასწავლო წელში ჯგუფი ასეთი კოდით უკვე არსებობს"
        },
        { GroupErrors.AcademicYearNotFound, ErrorType.Problem, "სასწავლო წელი ვერ მოიძებნა" },
        { GroupErrors.CourseNotFound, ErrorType.Problem, "საგანი ვერ მოიძებნა" },
        { GroupErrors.GroupSizeNotFound, ErrorType.Problem, "ჯგუფის ზომა ვერ მოიძებნა" },
        { GroupErrors.StudentStatusNotFound, ErrorType.Problem, "მოსწავლის სტატუსი ვერ მოიძებნა" },
        { GroupErrors.TeacherContractNotFound, ErrorType.Problem, "მასწავლებლის კონტრაქტი ვერ მოიძებნა" },
        { GroupErrors.SalarySchemeNotFound, ErrorType.Problem, "ხელფასის სქემა ვერ მოიძებნა" },
        {
            GroupErrors.SalarySchemeIsRequired, ErrorType.Problem,
            "ხელფასის სქემა მითითებული უნდა იყოს: მასწავლებლის კონტრაქტს ძირითადი სქემა არ აქვს"
        },
        { GroupErrors.StudentContractNotFound, ErrorType.Problem, "მოსწავლის კონტრაქტი ვერ მოიძებნა" },
        {
            GroupErrors.FourWeekHoursMustBePositive, ErrorType.Problem,
            "4 კვირაში საათების რაოდენობა 0-ზე მეტი უნდა იყოს"
        },
        {
            GroupErrors.FeeMustBePositive, ErrorType.Problem,
            "4 კვირის გადასახადი და საათის ღირებულება 0-ზე მეტი უნდა იყოს"
        },
        { GroupErrors.HoursCoefficientMustBePositive, ErrorType.Problem, "საათის კოეფიციენტი 0-ზე მეტი უნდა იყოს" },
        { GroupErrors.NoteIsTooLong, ErrorType.Problem, "შენიშვნა 255 სიმბოლოზე გრძელი ვერ იქნება" },
        { GroupErrors.WeekDayNotFound, ErrorType.Problem, "კვირის დღე ვერ მოიძებნა" },
        { GroupErrors.LessonStartTimeNotFound, ErrorType.Problem, "გაკვეთილის დაწყების დრო ვერ მოიძებნა" },
        { GroupErrors.RoomNotFound, ErrorType.Problem, "ოთახი ვერ მოიძებნა" },
        { GroupErrors.HoursCountMustBePositive, ErrorType.Problem, "გაკვეთილის საათები 0-ზე მეტი უნდა იყოს" },
        { GroupErrors.StartDateIsRequired, ErrorType.Problem, "დაწყების თარიღი შევსებული უნდა იყოს" },
        {
            GroupErrors.EndDateMustBeAfterStartDate, ErrorType.Problem,
            "დასრულების თარიღი დაწყების თარიღზე გვიან უნდა იყოს"
        },
        {
            GroupErrors.TeacherPeriodsOverlap, ErrorType.Problem,
            "ერთ დღეს ჯგუფს ორი მასწავლებელი ვერ ეყოლება: მასწავლებლების პერიოდები ერთმანეთს ფარავს"
        },
        {
            GroupErrors.DayTimePlacePeriodsOverlap, ErrorType.Problem,
            "ერთ კვირის დღეზე ჯგუფს ორი განრიგი ვერ ექნება: ერთი დღის განრიგების პერიოდები ერთმანეთს ფარავს"
        },
        {
            GroupErrors.StudentPeriodsOverlap, ErrorType.Problem,
            "ერთი მოსწავლე ჯგუფში ერთ დღეს ორჯერ ვერ იქნება: ერთი კონტრაქტის სტრიქონების პერიოდები ერთმანეთს " +
            "ფარავს"
        },
        {
            GroupErrors.RowNotFound, ErrorType.Problem,
            "მასწავლებლის, მოსწავლის ან განრიგის სტრიქონი ამ ჯგუფს არ ეკუთვნის"
        },
        {
            GroupErrors.GroupStudentIsInUse, ErrorType.Conflict,
            "მოსწავლის ჯგუფიდან წაშლა შეუძლებელია: მას ამ ჯგუფში უკვე აქვს გაკვეთილები. წაშლის ნაცვლად " +
            "მიუთითეთ დასრულების თარიღი"
        },
        {
            GroupErrors.GroupIsInUse, ErrorType.Conflict,
            "ჯგუფის წაშლა შეუძლებელია: მას უკვე აქვს გაკვეთილები ან ხელფასის ჩანაწერები. გასაუქმებლად " +
            "მიუთითეთ გაუქმების თარიღი"
        },
        {
            GroupErrors.FilterSortRequestIsInvalid, ErrorType.Problem,
            "სიის ფილტრის ან დალაგების პარამეტრები არასწორია"
        }
    };

    private static DateTime November(int day, int hour = 15) =>
        new(2026, 11, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static PropertyInfo[] ErrorProperties() =>
        typeof(GroupErrors).GetProperties(BindingFlags.Public | BindingFlags.Static);

    [Theory]
    [MemberData(nameof(FixedErrors))]
    public void FixedError_HasItsTypeAndText(Error error, ErrorType type, string description)
    {
        Assert.Equal(type, error.Type);
        Assert.Equal(description, error.Description);
    }

    //a new error property must get its row in FixedErrors
    [Fact]
    public void FixedErrors_ListEveryErrorProperty()
    {
        Assert.Equal(ErrorProperties().Length, FixedErrors.Count);
    }

    [Fact]
    public void ErrorProperties_CodeIsThePropertyName()
    {
        Assert.All(ErrorProperties(), p => Assert.Equal(p.Name, ((Error)p.GetValue(null)!).Code));
    }

    //D177: the lesson times as dd.MM.yyyy HH:mm in the given order
    [Fact]
    public void ChangeCreatesLessonsWithErrors_ListsTheLessonTimes()
    {
        Error error = GroupErrors.ChangeCreatesLessonsWithErrors([November(4), November(6, 9)]);

        Assert.Equal(nameof(GroupErrors.ChangeCreatesLessonsWithErrors), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(LessonsWithErrorsStart + "04.11.2026 15:00, 06.11.2026 09:00" + LessonsWithErrorsEnd,
            error.Description);
    }

    [Fact]
    public void ChangeCreatesLessonsWithErrors_TenLessons_ListsThemAllWithoutACount()
    {
        Error error = GroupErrors.ChangeCreatesLessonsWithErrors([.. Enumerable.Range(1, 10).Select(d => November(d))]);

        Assert.Equal(
            LessonsWithErrorsStart + string.Join(", ", Enumerable.Range(1, 10).Select(d => $"{d:00}.11.2026 15:00")) +
            LessonsWithErrorsEnd, error.Description);
    }

    [Fact]
    public void ChangeCreatesLessonsWithErrors_MoreThanTenLessons_ListsTheFirstTenAndTheCount()
    {
        Error error = GroupErrors.ChangeCreatesLessonsWithErrors([.. Enumerable.Range(1, 11).Select(d => November(d))]);

        Assert.Equal(
            LessonsWithErrorsStart + string.Join(", ", Enumerable.Range(1, 10).Select(d => $"{d:00}.11.2026 15:00")) +
            " და სხვა, სულ 11" + LessonsWithErrorsEnd, error.Description);
    }

    //D178: the overlaps in the given order, separated by semicolons
    [Fact]
    public void TeacherTimesOverlap_ListsTheOverlaps()
    {
        Error error = GroupErrors.TeacherTimesOverlap(["first", "second"]);

        Assert.Equal(nameof(GroupErrors.TeacherTimesOverlap), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(TeacherTimesOverlapStart + "first; second" + TeacherTimesOverlapEnd, error.Description);
    }

    [Fact]
    public void TeacherTimesOverlap_TenOverlaps_ListsThemAllWithoutACount()
    {
        Error error = GroupErrors.TeacherTimesOverlap([.. Enumerable.Range(1, 10).Select(i => $"o{i}")]);

        Assert.Equal(
            TeacherTimesOverlapStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"o{i}")) +
            TeacherTimesOverlapEnd, error.Description);
    }

    [Fact]
    public void TeacherTimesOverlap_MoreThanTenOverlaps_ListsTheFirstTenAndTheCount()
    {
        Error error = GroupErrors.TeacherTimesOverlap([.. Enumerable.Range(1, 12).Select(i => $"o{i}")]);

        Assert.Equal(
            TeacherTimesOverlapStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"o{i}")) +
            " და სხვა, სულ 12" + TeacherTimesOverlapEnd, error.Description);
    }

    //D179: the same list as for the teachers, with the students' text
    [Fact]
    public void StudentTimesOverlap_ListsTheOverlaps()
    {
        Error error = GroupErrors.StudentTimesOverlap(["first", "second"]);

        Assert.Equal(nameof(GroupErrors.StudentTimesOverlap), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(StudentTimesOverlapStart + "first; second" + StudentTimesOverlapEnd, error.Description);
    }

    [Fact]
    public void StudentTimesOverlap_TenOverlaps_ListsThemAllWithoutACount()
    {
        Error error = GroupErrors.StudentTimesOverlap([.. Enumerable.Range(1, 10).Select(i => $"o{i}")]);

        Assert.Equal(
            StudentTimesOverlapStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"o{i}")) +
            StudentTimesOverlapEnd, error.Description);
    }

    [Fact]
    public void StudentTimesOverlap_MoreThanTenOverlaps_ListsTheFirstTenAndTheCount()
    {
        Error error = GroupErrors.StudentTimesOverlap([.. Enumerable.Range(1, 11).Select(i => $"o{i}")]);

        Assert.Equal(
            StudentTimesOverlapStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"o{i}")) +
            " და სხვა, სულ 11" + StudentTimesOverlapEnd, error.Description);
    }
}
