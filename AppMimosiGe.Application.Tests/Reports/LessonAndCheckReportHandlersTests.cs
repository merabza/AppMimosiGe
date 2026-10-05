using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Checks;
using AppMimosiGe.Application.Reports.Checks.DayTimeMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.DayTimeSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Checks.StudentFeeMismatches;
using AppMimosiGe.Application.Reports.Checks.StudentMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.StudentSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.TeacherMissedTransitions;
using AppMimosiGe.Application.Reports.Checks.TeacherSameStartEndDate;
using AppMimosiGe.Application.Reports.Checks.TeacherSchemeMismatches;
using AppMimosiGe.Application.Reports.Lessons.LessonsWithErrors;
using AppMimosiGe.Application.Reports.Lessons.LessonsWithWrongVoidStatus;
using AppMimosiGe.Application.Reports.Lessons.Missings;
using AppMimosiGe.Application.Reports.Lessons.MissingsInRow;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Lessons.TeacherMissAndSubstitutes;
using AppMimosiGe.Application.Reports.Lessons.WrongStatusLessons;
using AppMimosiGe.Application.Reports.Lessons.WrongWeekDayChanges;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using Moq;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.GroupRowsSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class LessonAndCheckReportHandlersTests
{
    private static readonly DateTime StartDate = Day(2026, 9, 1);
    private static readonly DateTime EndDate = Day(2026, 9, 30);
    private static readonly DateTime DayAfterEnd = Day(2026, 10, 1);

    private static readonly SchedulePerson Teacher = new("TLast", "TFirst", "T5");

    private readonly Mock<IReportsRepository> _repository = new();

    //a provider whose local now is the given time (as GetLocalNow of the system provider on the Georgian server)
    private static TimeProvider At(DateTime localNow)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(localNow, TimeSpan.Zero));
        return timeProvider.Object;
    }

    private void SetupPeriodLessons(params PeriodLessonRow[] lessons)
    {
        _repository.Setup(r => r.GetPeriodLessons(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([.. lessons]);
    }

    private static PeriodLessonRow Lesson(int id, int statusId, bool present, SchedulePerson? substitute = null)
    {
        return new PeriodLessonRow(id, StartDate.AddHours(id), "G1", 5, Teacher, substitute, statusId, "status",
            false, present);
    }

    private static void AssertSameTable(ReportTable expected, Result<ReportTable> actual)
    {
        Assert.Equal(expected.Columns, actual.Value.Columns);
        Assert.Equal(expected.Sections.Select(s => s.Header), actual.Value.Sections.Select(s => s.Header));
        Assert.Equal(expected.Sections.SelectMany(s => s.Rows), actual.Value.Sections.SelectMany(s => s.Rows));
        Assert.Equal(expected.FooterRows, actual.Value.FooterRows);
    }

    // only lessons that have started (D121): before now, if now is earlier than the end of the end date
    [Fact]
    public async Task WrongStatusLessons_NowInThePeriod_LoadsTheLessonsUntilNow()
    {
        // Arrange
        DateTime now = Day(2026, 9, 15).AddHours(12.5);
        SetupPeriodLessons(Lesson(1, 1, false), Lesson(2, 1, true));

        // Act
        Result<ReportTable> result = await new WrongStatusLessonsReportQueryHandler(_repository.Object, At(now))
            .Handle(new WrongStatusLessonsReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetPeriodLessons(StartDate, now, It.IsAny<CancellationToken>()), Times.Once);
        AssertSameTable(LessonCheckReports.WrongStatusLessons([Lesson(1, 1, false), Lesson(2, 1, true)]), result);
        Assert.Single(Assert.Single(result.Value.Sections).Rows);
    }

    // a past period: the whole end date (until the next midnight)
    [Fact]
    public async Task WrongStatusLessons_PastPeriod_LoadsUntilTheEndOfTheEndDate()
    {
        // Arrange
        SetupPeriodLessons();

        // Act
        await new WrongStatusLessonsReportQueryHandler(_repository.Object, At(Day(2026, 10, 4).AddHours(12)))
            .Handle(new WrongStatusLessonsReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetPeriodLessons(StartDate, DayAfterEnd, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // future lessons too (Access): a cancellation or a recovery date may be entered in advance
    [Fact]
    public async Task LessonsWithWrongVoidStatus_LoadsTheWholePeriod()
    {
        // Arrange
        SetupPeriodLessons(Lesson(1, 2, true), Lesson(2, 2, false));

        // Act
        Result<ReportTable> result = await new LessonsWithWrongVoidStatusReportQueryHandler(_repository.Object)
            .Handle(new LessonsWithWrongVoidStatusReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetPeriodLessons(StartDate, DayAfterEnd, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(Assert.Single(result.Value.Sections).Rows);
    }

    [Fact]
    public async Task TeacherMissAndSubstitutes_LoadsTheWholePeriod()
    {
        // Arrange
        SetupPeriodLessons(Lesson(1, 2, false), Lesson(2, 1, false, new SchedulePerson("S", "S", "T6")),
            Lesson(3, 1, false));

        // Act
        Result<ReportTable> result = await new TeacherMissAndSubstitutesReportQueryHandler(_repository.Object)
            .Handle(new TeacherMissAndSubstitutesReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetPeriodLessons(StartDate, DayAfterEnd, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(2, Assert.Single(result.Value.Sections).Rows.Count);
    }

    [Fact]
    public async Task Missings_NowInThePeriod_CountsTheAbsencesUntilNow()
    {
        // Arrange
        DateTime now = Day(2026, 9, 20).AddHours(9);
        List<AbsenceCountRow> counts = [new(20, new SchedulePerson("S", "S", "6.020"), "Math", 3)];
        _repository.Setup(r => r.GetAbsenceCounts(StartDate, now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        // Act
        Result<ReportTable> result = await new MissingsReportQueryHandler(_repository.Object, At(now))
            .Handle(new MissingsReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        AssertSameTable(AbsenceReports.Missings(counts), result);
    }

    [Fact]
    public async Task Missings_PastPeriod_CountsUntilTheEndOfTheEndDate()
    {
        // Arrange
        _repository.Setup(r => r.GetAbsenceCounts(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        await new MissingsReportQueryHandler(_repository.Object, At(Day(2027, 1, 1)))
            .Handle(new MissingsReportQuery(StartDate, EndDate), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetAbsenceCounts(StartDate, DayAfterEnd, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // the date's state with the lessons started before now (D121, D122): on the date itself until now
    [Fact]
    public async Task MissingsInRow_Today_LoadsTheLessonsUntilNow()
    {
        // Arrange
        DateTime date = Day(2026, 10, 10);
        DateTime now = date.AddHours(15);
        var data = new MissingsInRowData([new StudentAbsence(20, date), new StudentAbsence(20, date.AddHours(1))],
            new Dictionary<int, DateTime>(),
            new Dictionary<int, StudentContact> { [20] = new(new SchedulePerson("S", "S", "6.020"), null, "P P", null) });
        _repository.Setup(r => r.GetMissingsInRow(date, now, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        // Act
        Result<ReportTable> result = await new MissingsInRowReportQueryHandler(_repository.Object, At(now))
            .Handle(new MissingsInRowReportQuery(date), CancellationToken.None);

        // Assert
        AssertSameTable(AbsenceReports.MissingsInRow(data), result);
        Assert.Single(Assert.Single(result.Value.Sections).Rows);
    }

    // a past date: until the end of that day
    [Fact]
    public async Task MissingsInRow_PastDate_LoadsTheLessonsUntilTheEndOfTheDate()
    {
        // Arrange
        DateTime date = Day(2026, 10, 9);
        _repository.Setup(r => r.GetMissingsInRow(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new MissingsInRowData([], new Dictionary<int, DateTime>(),
            new Dictionary<int, StudentContact>()));

        // Act
        await new MissingsInRowReportQueryHandler(_repository.Object, At(Day(2026, 10, 10).AddHours(15)))
            .Handle(new MissingsInRowReportQuery(date), CancellationToken.None);

        // Assert
        _repository.Verify(r => r.GetMissingsInRow(date, Day(2026, 10, 10), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LessonsWithErrors_BuildsTheReportFromTheLog()
    {
        // Arrange
        List<LessonErrorRow> errors = [new(1, "G1", StartDate, "error")];
        _repository.Setup(r => r.GetLessonErrors(null, It.IsAny<CancellationToken>())).ReturnsAsync(errors);

        // Act
        Result<ReportTable> result = await new LessonsWithErrorsReportQueryHandler(_repository.Object)
            .Handle(new LessonsWithErrorsReportQuery(), CancellationToken.None);

        // Assert
        AssertSameTable(LessonCheckReports.LessonsWithErrors(errors), result);
    }

    [Fact]
    public async Task WrongWeekDayChanges_BuildsTheReportFromTheLessons()
    {
        // Arrange
        List<TeoDatesLessonRow> lessons = [new(1, "G1", Teacher, StartDate, StartDate, 8f, StartDate)];
        _repository.Setup(r => r.GetLessonsWithMidnightTeoDates(null, It.IsAny<CancellationToken>())).ReturnsAsync(lessons);

        // Act
        Result<ReportTable> result = await new WrongWeekDayChangesReportQueryHandler(_repository.Object)
            .Handle(new WrongWeekDayChangesReportQuery(), CancellationToken.None);

        // Assert
        AssertSameTable(LessonCheckReports.WrongWeekDayChanges(lessons), result);
    }

    //rows on which every check report has a line
    private static GroupRowsSnapshot BusyRows()
    {
        return new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, Day(2026, 1, 5), Day(2026, 1, 20))
            .DayTime(2, 1, Day(2026, 2, 1), null).DayTime(3, 1, Day(2026, 3, 1), Day(2026, 3, 1))
            .Teacher(1, 1, 5, Day(2026, 1, 5), Day(2026, 1, 20), 2).Teacher(2, 1, 6, Day(2026, 2, 1), null)
            .Teacher(3, 1, 6, Day(2026, 3, 1), Day(2026, 3, 1))
            .Student(1, 1, 20, Day(2026, 1, 5), Day(2026, 1, 20), 1m).Student(2, 1, 20, Day(2026, 2, 1), null, 1m)
            .Student(3, 1, 21, Day(2026, 3, 1), Day(2026, 3, 1)).Build();
    }

    private async Task AssertGroupRowsHandler<TQuery>(IQueryHandler<TQuery, ReportTable> handler, TQuery query,
        Func<GroupRowsSnapshot, ReportTable> build) where TQuery : GroupRowsReportQuery
    {
        // Arrange
        GroupRowsSnapshot snapshot = BusyRows();
        _repository.Setup(r => r.GetGroupRows(query.AcademicYearId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        // Act
        Result<ReportTable> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        ReportTable expected = build(snapshot);
        Assert.NotEmpty(expected.Sections.SelectMany(s => s.Rows));
        AssertSameTable(expected, result);
        _repository.Verify(r => r.GetGroupRows(query.AcademicYearId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // every check report handler loads all group rows and builds its report from them
    [Fact]
    public Task StudentMissedTransitions_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new StudentMissedTransitionsReportQueryHandler(_repository.Object),
            new StudentMissedTransitionsReportQuery(), TransitionReports.StudentMissedTransitions);
    }

    [Fact]
    public Task TeacherMissedTransitions_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new TeacherMissedTransitionsReportQueryHandler(_repository.Object),
            new TeacherMissedTransitionsReportQuery(), TransitionReports.TeacherMissedTransitions);
    }

    [Fact]
    public Task DayTimeMissedTransitions_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new DayTimeMissedTransitionsReportQueryHandler(_repository.Object),
            new DayTimeMissedTransitionsReportQuery(), TransitionReports.DayTimeMissedTransitions);
    }

    [Fact]
    public Task TeacherSchemeMismatches_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new TeacherSchemeMismatchesReportQueryHandler(_repository.Object),
            new TeacherSchemeMismatchesReportQuery(), MismatchReports.TeacherSchemeMismatches);
    }

    [Fact]
    public Task StudentFeeMismatches_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new StudentFeeMismatchesReportQueryHandler(_repository.Object),
            new StudentFeeMismatchesReportQuery(), MismatchReports.StudentFeeMismatches);
    }

    [Fact]
    public Task StudentSameStartEndDate_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new StudentSameStartEndDateReportQueryHandler(_repository.Object),
            new StudentSameStartEndDateReportQuery(), SameStartEndDateReports.StudentSameStartEndDate);
    }

    [Fact]
    public Task TeacherSameStartEndDate_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new TeacherSameStartEndDateReportQueryHandler(_repository.Object),
            new TeacherSameStartEndDateReportQuery(), SameStartEndDateReports.TeacherSameStartEndDate);
    }

    [Fact]
    public Task DayTimeSameStartEndDate_BuildsTheReportFromTheGroupRows()
    {
        return AssertGroupRowsHandler(new DayTimeSameStartEndDateReportQueryHandler(_repository.Object),
            new DayTimeSameStartEndDateReportQuery(), SameStartEndDateReports.DayTimeSameStartEndDate);
    }
}
