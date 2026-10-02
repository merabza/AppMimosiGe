using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class SalaryRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly SalaryRepository _repository;

    public SalaryRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new SalaryRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Day(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //contracts: 1 (Alpha Ann T3.01, pension, account, quote 1), 2 (Beta Bob T1.02, IndEnt, NextMonth, no quote,
    //description). Headers: 3 (05.09, no data), 1 (05.10, transfer 04.10: lines 30 and 31, parts 20 and 21),
    //2 (05.11, transfer 05.11: line 32). Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(
            new Human { HumId = 1, LastName = "Alpha", FirstName = "Ann", PersonalId = "01000000001" },
            new Human
            {
                HumId = 2, LastName = "Beta", FirstName = "Bob", LegalName = "Ltd", PersonalId = "1000000002",
                LegalAddress = "City, Street"
            });
        _context.RsCountries.Add(new RsCountry { Id = 1, Code = "268", CountryName = "Georgia" });
        _context.RsQuoteTypes.Add(new RsQuoteType { QtId = 1, QtName = "Salary" });
        _context.GeoMonths.AddRange(new GeoMonth { GmnId = 9, GmnName = "September", GmnDative = "S" },
            new GeoMonth { GmnId = 10, GmnName = "October", GmnDative = "O" });
        _context.TeacherContracts.AddRange(
            new TeacherContract
            {
                Id = 1, ContractNumber = "T3.01", TeacherHumanId = 1, RsCountryId = 1, RsQuoteTypeId = 1,
                PensionScheme = true, BankAccount = "GE01"
            },
            new TeacherContract
            {
                Id = 2, ContractNumber = "T1.02", TeacherHumanId = 2, RsCountryId = 1, IndEnt = true,
                NextMonth = true, Description = "Dividend"
            });
        _context.SalaryPartTypes.AddRange(new SalaryPartType { SptId = 1, SptName = "Lessons", SptCountPlaceId = 1 },
            new SalaryPartType { SptId = 4, SptName = "Deduction", SptCountPlaceId = 2 },
            new SalaryPartType { SptId = 6, SptName = "Other" });
        _context.TeacherSalarySchemes.Add(new TeacherSalaryScheme { Id = 11, SchemaName = "8", HourSalaryNet = 8m });
        _context.OperationMonths.Add(new OperationMonth { Id = 1, MonthDate = Day(2026, 9, 1) });
        _context.Groups.AddRange(new Group { GrpId = 100, GroupCode = "G-100" },
            new Group { GrpId = 200, GroupCode = "G-200" });
        _context.GroupsByStudents.AddRange(new GroupByStudent { GbsId = 1, GroupId = 100, StudentContractId = 50 },
            new GroupByStudent { GbsId = 2, GroupId = 200, StudentContractId = 51 });
        _context.Lessons.AddRange(Lesson(1, Day(2026, 9, 10, 10), 1, null),
            Lesson(2, Day(2026, 10, 10, 10), 1, 2), Lesson(3, Day(2026, 8, 31, 23), 1, null));
        _context.LessonsByStudents.AddRange(StudentRow(1, 1, 50, 1, 2f), StudentRow(2, 1, 51, 2, 1f),
            StudentRow(3, 1, 50, null, 3f), StudentRow(4, 1, 51, 1, 4f), StudentRow(5, 2, 50, 1, 1.5f),
            StudentRow(6, 3, 50, 1, 1f));
        _context.SalaryHeaders.AddRange(Header(1, Day(2026, 10, 5), Day(2026, 10, 4)),
            Header(2, Day(2026, 11, 5), Day(2026, 11, 5)), Header(3, Day(2026, 9, 5), Day(2026, 9, 5)));
        _context.SalaryParts.AddRange(
            new SalaryPart { SpId = 20, ShId = 1, TeacherContractId = 1, SalaryPartTypeId = 1, SpAmount = 16m },
            new SalaryPart { SpId = 21, ShId = 1, TeacherContractId = 2, SalaryPartTypeId = 4, SpAmount = 5m },
            new SalaryPart { SpId = 22, ShId = 1, TeacherContractId = 1, SalaryPartTypeId = null, SpAmount = 1m });
        _context.SalaryLines.AddRange(Line(30, 1, 1, 100m, 125m), Line(31, 1, 2, 50m, 60m),
            Line(32, 2, 1, 20m, 25m));
        _context.SalaryLinesDetails.AddRange(
            new SalaryLineDetail
            {
                SadId = 40, SaId = 30, GroupId = 200, SadAmount = 4m, SadHoursCount = 0.5f, SadHourCost = 8m
            },
            new SalaryLineDetail
            {
                SadId = 41, SaId = 30, GroupId = 100, SadAmount = 12m, SadHoursCount = 1.5f, SadHourCost = 8m
            },
            new SalaryLineDetail
            {
                SadId = 42, SaId = 32, GroupId = 100, SadAmount = 1m, SadHoursCount = 1f, SadHourCost = 1m
            });
        _context.SaveChanges();
    }

    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static Lesson Lesson(int id, DateTime lessonDt, int teacher, int? substitute)
    {
        return new Lesson
        {
            Id = id,
            GroupId = 100,
            TeacherContractId = teacher,
            SubstituteTeacherContractId = substitute,
            LessonDt = lessonDt,
            SalarySchemaId = 11,
            LessonStatusId = 1,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
    }

    private static LessonByStudent StudentRow(int id, int lessonId, int studentContractId, int? groupByStudentId,
        float hours)
    {
        return new LessonByStudent
        {
            Id = id,
            LessonId = lessonId,
            StudentContractId = studentContractId,
            GroupByStudentId = groupByStudentId,
            HoursCount = hours
        };
    }

    private static SalaryHeader Header(int id, DateTime chargeDate, DateTime transferDate)
    {
        return new SalaryHeader { ShId = id, ShChargeDate = chargeDate, ShTransferDate = transferDate };
    }

    private static SalaryLine Line(int id, int shId, int contractId, decimal net, decimal gross)
    {
        return new SalaryLine
        {
            SaId = id,
            ShId = shId,
            TeacherContractId = contractId,
            SaNetAmountRound = net,
            SaAmountGross = gross,
            SaAmountNet = net - 1m,
            SaMonthDate = Day(2026, shId == 1 ? 9 : 10, 1),
            RsQuoteTypeId = 1
        };
    }

    [Fact]
    public async Task GetHeaders_AreOrderedByChargeDateWithLineCountsAndNetSums()
    {
        // Act
        List<SalaryHeaderRowResponse> headers = await _repository.GetHeaders();

        // Assert
        Assert.Equal(
        [
            new SalaryHeaderRowResponse(3, Day(2026, 9, 5), Day(2026, 9, 5), 0, 0m),
            new SalaryHeaderRowResponse(1, Day(2026, 10, 5), Day(2026, 10, 4), 2, 148m),
            new SalaryHeaderRowResponse(2, Day(2026, 11, 5), Day(2026, 11, 5), 1, 19m)
        ], headers);
    }

    [Fact]
    public async Task GetHeader_ReturnsThePartsLinesAndDetailsOfTheHeader()
    {
        // Act
        SalaryHeaderResponse? header = await _repository.GetHeader(1);

        // Assert
        Assert.NotNull(header);
        Assert.Equal((1, Day(2026, 10, 5), Day(2026, 10, 4)),
            (header.ShId, header.ShChargeDate, header.ShTransferDate));
        Assert.Equal(
        [
            new SalaryPartResponse(22, 1, "Alpha Ann / T3.01", null, null, 1m),
            new SalaryPartResponse(20, 1, "Alpha Ann / T3.01", 1, "Lessons", 16m),
            new SalaryPartResponse(21, 2, "Beta Bob / T1.02", 4, "Deduction", 5m)
        ], header.Parts);
        //Access-ის ქვეფორმის რიგი: დარიცხული თანხა კლებადობით
        Assert.Equal([30, 31], header.Lines.Select(l => l.SaId));
        SalaryLineResponse line = header.Lines[0];
        Assert.Equal((1, "Alpha Ann / T3.01", 100m, 125m, 99m, Day(2026, 9, 1), (int?)1),
            (line.TeacherContractId, line.EmployeeName, line.SaNetAmountRound, line.SaAmountGross, line.SaAmountNet,
                line.SaMonthDate, line.RsQuoteTypeId));
        Assert.Equal(
        [
            new SalaryLineDetailResponse(41, 30, "Alpha Ann / T3.01", 100, "G-100", 1.5f, 12m, 8m),
            new SalaryLineDetailResponse(40, 30, "Alpha Ann / T3.01", 200, "G-200", 0.5f, 4m, 8m)
        ], header.Details);
    }

    // two headers on one charge date keep the order they were created in
    [Fact]
    public async Task GetHeaders_SameChargeDate_AreOrderedById()
    {
        // Arrange
        AddAndSave(Header(5, Day(2026, 10, 5), Day(2026, 10, 1)), Header(4, Day(2026, 10, 5), Day(2026, 10, 2)));

        // Act
        List<SalaryHeaderRowResponse> headers = await _repository.GetHeaders();

        // Assert
        Assert.Equal([3, 1, 4, 5, 2], headers.Select(h => h.ShId));
    }

    [Fact]
    public async Task GetHeader_PartsOfOneEmployeeAndType_AreOrderedById()
    {
        // Arrange
        AddAndSave(new SalaryPart { SpId = 25, ShId = 1, TeacherContractId = 2, SalaryPartTypeId = 4, SpAmount = 1m });

        // Act
        SalaryHeaderResponse? header = await _repository.GetHeader(1);

        // Assert
        Assert.Equal([22, 20, 21, 25], header!.Parts.Select(p => p.SpId));
    }

    [Fact]
    public async Task GetHeader_LinesWithEqualGross_AreOrderedByEmployeeThenId()
    {
        // Arrange
        AddAndSave(Line(33, 2, 2, 20m, 25m), Line(34, 2, 1, 20m, 25m));

        // Act
        SalaryHeaderResponse? header = await _repository.GetHeader(2);

        // Assert
        Assert.Equal([32, 34, 33], header!.Lines.Select(l => l.SaId));
    }

    // a group code is unique only within its academic year
    [Fact]
    public async Task GetHeader_DetailsAreOrderedByEmployeeGroupCodeThenId()
    {
        // Arrange
        AddAndSave(new Group { GrpId = 300, GroupCode = "G-100" }, Line(33, 2, 2, 20m, 25m),
            new SalaryLineDetail
            {
                SadId = 43, SaId = 33, GroupId = 100, SadAmount = 1m, SadHoursCount = 1f, SadHourCost = 1m
            },
            new SalaryLineDetail
            {
                SadId = 44, SaId = 32, GroupId = 300, SadAmount = 1m, SadHoursCount = 1f, SadHourCost = 1m
            });

        // Act
        SalaryHeaderResponse? header = await _repository.GetHeader(2);

        // Assert
        Assert.Equal([42, 44, 43], header!.Details.Select(d => d.SadId));
    }

    [Fact]
    public async Task GetHeader_Missing_ReturnsNull()
    {
        Assert.Null(await _repository.GetHeader(99));
    }

    [Fact]
    public async Task HeaderExistsAndHasData()
    {
        Assert.True(await _repository.HeaderExists(3));
        Assert.False(await _repository.HeaderExists(99));
        Assert.True(await _repository.HeaderHasData(1));
        Assert.True(await _repository.HeaderHasData(2));
        Assert.False(await _repository.HeaderHasData(3));
    }

    [Fact]
    public async Task HeaderHasData_PartsOnly_IsTrue()
    {
        // Arrange
        AddAndSave(new SalaryPart { SpId = 23, ShId = 3, TeacherContractId = 1, SpAmount = 1m });

        // Act + Assert
        Assert.True(await _repository.HeaderHasData(3));
    }

    [Fact]
    public async Task GetHeaderWithDataForChange_LoadsThePartsAndLinesTracked()
    {
        // Act
        SalaryHeader? header = await _repository.GetHeaderWithDataForChange(1);

        // Assert
        Assert.NotNull(header);
        Assert.Equal([20, 21, 22], header.SalaryParts.Select(p => p.SpId).Order());
        Assert.Equal([30, 31], header.SalaryLines.Select(l => l.SaId).Order());
        Assert.Equal(EntityState.Unchanged, _context.Entry(header).State);
    }

    [Fact]
    public async Task GetForChange_ReturnsTrackedEntities()
    {
        SalaryHeader? header = await _repository.GetHeaderForChange(2);
        SalaryPart? part = await _repository.GetPartForChange(21);

        Assert.Equal(EntityState.Unchanged, _context.Entry(header!).State);
        Assert.Equal(EntityState.Unchanged, _context.Entry(part!).State);
        Assert.Null(await _repository.GetHeaderForChange(99));
        Assert.Null(await _repository.GetPartForChange(99));
    }

    [Fact]
    public async Task EmployeeExists_ChecksEveryContract()
    {
        Assert.True(await _repository.EmployeeExists(2));
        Assert.False(await _repository.EmployeeExists(3));
    }

    [Fact]
    public async Task Lookups_AreOrdered()
    {
        Assert.Equal([new LookupItemResponse(1, "Alpha Ann / T3.01"), new LookupItemResponse(2, "Beta Bob / T1.02")],
            await _repository.GetEmployeeLookups());
        Assert.Equal(
        [
            new SalaryPartTypeLookupResponse(1, "Lessons", 1), new SalaryPartTypeLookupResponse(4, "Deduction", 2),
            new SalaryPartTypeLookupResponse(6, "Other", null)
        ], await _repository.GetPartTypeLookups());
        Assert.Equal(new SalaryPartTypeLookupResponse(4, "Deduction", 2), await _repository.GetPartType(4));
        Assert.Null(await _repository.GetPartType(99));
    }

    [Fact]
    public async Task CalculationData_IsReadFromTheTables()
    {
        Assert.Equal([new SalaryContractData(1, true, false, false), new SalaryContractData(2, false, true, true)],
            (await _repository.GetContracts()).OrderBy(c => c.Id));
        Assert.Equal([Day(2026, 9, 1)], await _repository.GetOperationMonths());
        Assert.Equal(new Dictionary<int, decimal> { [11] = 8m }, await _repository.GetHourRates());
        Assert.Equal(new Dictionary<int, int?> { [1] = 1, [4] = 2, [6] = null },
            await _repository.GetPartTypeCountPlaces());
    }

    // only a student row of the lesson's group, through the group student row of the same student contract
    [Fact]
    public async Task GetLessonRows_JoinsTheStudentRowsOfTheLessonsGroupInThePeriod()
    {
        // Act
        List<SalaryLessonStudentRow> rows = await _repository.GetLessonRows(Day(2026, 9, 1), Day(2026, 11, 1));

        // Assert
        Assert.Equal(
        [
            new SalaryLessonStudentRow(1, 100, 1, null, 11, Day(2026, 9, 10, 10), 1, 2f),
            new SalaryLessonStudentRow(2, 100, 1, 2, 11, Day(2026, 10, 10, 10), 1, 1.5f)
        ], rows.OrderBy(r => r.LessonId));
    }

    [Fact]
    public async Task GetLessonRows_StartIsInclusive()
    {
        // Arrange: a lesson exactly at midnight of the period's first day
        AddAndSave(Lesson(4, Day(2026, 9, 1), 1, null), StudentRow(7, 4, 50, 1, 1f));

        // Act
        List<SalaryLessonStudentRow> rows = await _repository.GetLessonRows(Day(2026, 9, 1), Day(2026, 9, 2));

        // Assert
        Assert.Equal([4], rows.Select(r => r.LessonId));
    }

    [Fact]
    public async Task GetLessonRows_EndIsExclusive()
    {
        Assert.Equal([1], (await _repository.GetLessonRows(Day(2026, 9, 1), Day(2026, 10, 10, 10)))
            .Select(r => r.LessonId));
    }

    [Fact]
    public async Task GetTransferFileRows_AreTheLinesOfTheHeaderWithTheMonthName()
    {
        // Act
        List<TransferFileRow> rows = await _repository.GetTransferFileRows(1);

        // Assert
        Assert.Equal(
        [
            new TransferFileRow("GE01", null, "Ann", "Alpha", "01000000001", 99m, null, "Salary", "September", 2026),
            new TransferFileRow(null, "Ltd", "Bob", "Beta", "1000000002", 49m, "Dividend", null, "September", 2026)
        ], rows.OrderBy(r => r.PersonalId, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetTransferFileRows_MonthMissingFromGeoMonths_HasAnEmptyName()
    {
        // Arrange
        AddAndSave(Header(6, Day(2026, 12, 5), Day(2026, 12, 5)),
            new SalaryLine { SaId = 35, ShId = 6, TeacherContractId = 1, SaMonthDate = Day(2026, 11, 1) });

        // Act
        TransferFileRow row = Assert.Single(await _repository.GetTransferFileRows(6));

        // Assert
        Assert.Equal((string.Empty, 2026), (row.MonthName, row.Year));
    }

    [Fact]
    public async Task GetDeclarationFileRows_PeriodStartIsInclusiveAndEndExclusive()
    {
        Assert.Equal([30, 31],
            (await _repository.GetDeclarationFileRows(Day(2026, 10, 4), Day(2026, 10, 5))).Select(r => r.SaId)
            .Order());
        Assert.Empty(await _repository.GetDeclarationFileRows(Day(2026, 10, 3), Day(2026, 10, 4)));
    }

    [Fact]
    public async Task GetDeclarationFileRows_AreTheLinesTransferredInThePeriod()
    {
        // Act
        List<DeclarationFileRow> rows = await _repository.GetDeclarationFileRows(Day(2026, 10, 1), Day(2026, 11, 1));

        // Assert
        Assert.Equal(
        [
            new DeclarationFileRow(30, "01000000001", null, "Ann", "Alpha", null, "268", 1, 125m, Day(2026, 10, 4)),
            new DeclarationFileRow(31, "1000000002", "Ltd", "Bob", "Beta", "City, Street", "268", null, 60m,
                Day(2026, 10, 4))
        ], rows.OrderBy(r => r.SaId));
        Assert.Empty(await _repository.GetDeclarationFileRows(Day(2026, 10, 5), Day(2026, 11, 5)));
    }

    [Fact]
    public void AddAndRemove_ChangeTheContext()
    {
        // Arrange
        var header = new SalaryHeader { ShId = 9, ShChargeDate = Day(2027, 1, 5), ShTransferDate = Day(2027, 1, 5) };
        var part = new SalaryPart { SpId = 29, ShId = 9, TeacherContractId = 1, SpAmount = 1m };
        SalaryLine line = _context.SalaryLines.Single(l => l.SaId == 32);
        SalaryHeader removedHeader = _context.SalaryHeaders.Single(h => h.ShId == 3);
        SalaryPart removedPart = _context.SalaryParts.Single(p => p.SpId == 22);

        // Act
        _repository.AddHeader(header);
        _repository.AddPart(part);
        _repository.RemoveLine(line);
        _repository.RemoveHeader(removedHeader);
        _repository.RemovePart(removedPart);

        // Assert
        Assert.Equal(EntityState.Added, _context.Entry(header).State);
        Assert.Equal(EntityState.Added, _context.Entry(part).State);
        Assert.Equal(EntityState.Deleted, _context.Entry(line).State);
        Assert.Equal(EntityState.Deleted, _context.Entry(removedHeader).State);
        Assert.Equal(EntityState.Deleted, _context.Entry(removedPart).State);
    }
}
