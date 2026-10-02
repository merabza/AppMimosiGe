using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary;
using AppMimosiGe.Application.Salary.CountSalary;
using AppMimosiGe.Application.Salary.CreateSalaryHeader;
using AppMimosiGe.Application.Salary.CreateSalaryPart;
using AppMimosiGe.Application.Salary.DeleteSalaryHeader;
using AppMimosiGe.Application.Salary.DeleteSalaryPart;
using AppMimosiGe.Application.Salary.GetDeclarationFile;
using AppMimosiGe.Application.Salary.GetSalaryFormLookups;
using AppMimosiGe.Application.Salary.GetSalaryHeader;
using AppMimosiGe.Application.Salary.GetSalaryHeaders;
using AppMimosiGe.Application.Salary.GetTransferFile;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGe.Application.Salary.UpdateSalaryHeader;
using AppMimosiGe.Application.Salary.UpdateSalaryPart;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Salary;

public sealed class SalaryHandlersTests
{
    private const int AdditionType = 3;
    private const int DeductionType = 4;

    private readonly Mock<ISalaryRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public SalaryHandlersTests()
    {
        _repository.Setup(r => r.GetPartType(AdditionType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryPartTypeLookupResponse(AdditionType, "დანამატი", 1));
        _repository.Setup(r => r.GetPartType(DeductionType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryPartTypeLookupResponse(DeductionType, "გამოქვითვა", 2));
    }

    private static DateTime Day(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static SalaryPartRequest PartRequest(int? typeId = AdditionType, decimal amount = 50m) =>
        new() { TeacherContractId = 7, SalaryPartTypeId = typeId, SpAmount = amount };

    private static void AssertError(Result result, Error error)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(error.Code, result.Error.Code);
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private SalaryHeader WithHeader(SalaryHeader? header = null)
    {
        header ??= new SalaryHeader { ShId = 2, ShChargeDate = Day(2026, 10, 5), ShTransferDate = Day(2026, 10, 4) };
        _repository.Setup(r => r.GetHeaderForChange(header.ShId, It.IsAny<CancellationToken>())).ReturnsAsync(header);
        _repository.Setup(r => r.HeaderExists(header.ShId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return header;
    }

    private SalaryPart WithPart(int? typeId)
    {
        var part = new SalaryPart { SpId = 9, ShId = 2, TeacherContractId = 1, SalaryPartTypeId = typeId, SpAmount = 1m };
        _repository.Setup(r => r.GetPartForChange(9, It.IsAny<CancellationToken>())).ReturnsAsync(part);
        return part;
    }

    // --- უწყისები ---

    [Fact]
    public async Task GetHeaders_ReturnsTheRepositoryList()
    {
        // Arrange
        List<SalaryHeaderRowResponse> headers = [new(1, Day(2026, 10, 5), Day(2026, 10, 5), 6, 3771.44m)];
        _repository.Setup(r => r.GetHeaders(It.IsAny<CancellationToken>())).ReturnsAsync(headers);

        // Act
        Result<List<SalaryHeaderRowResponse>> result =
            await new GetSalaryHeadersQueryHandler(_repository.Object).Handle(new GetSalaryHeadersQuery(),
                CancellationToken.None);

        // Assert
        Assert.Same(headers, result.Value);
    }

    [Fact]
    public async Task GetFormLookups_ReturnsEmployeesAndPartTypes()
    {
        // Arrange
        List<LookupItemResponse> employees = [new(1, "A / T1")];
        List<SalaryPartTypeLookupResponse> types = [new(1, "ხელფასი", 1)];
        _repository.Setup(r => r.GetEmployeeLookups(It.IsAny<CancellationToken>())).ReturnsAsync(employees);
        _repository.Setup(r => r.GetPartTypeLookups(It.IsAny<CancellationToken>())).ReturnsAsync(types);

        // Act
        Result<SalaryFormLookupsResponse> result =
            await new GetSalaryFormLookupsQueryHandler(_repository.Object).Handle(new GetSalaryFormLookupsQuery(),
                CancellationToken.None);

        // Assert
        Assert.Same(employees, result.Value.Employees);
        Assert.Same(types, result.Value.PartTypes);
    }

    [Fact]
    public async Task GetHeader_Found_ReturnsIt()
    {
        // Arrange
        var header = new SalaryHeaderResponse(2, Day(2026, 10, 5), Day(2026, 10, 5), [], [], []);
        _repository.Setup(r => r.GetHeader(2, It.IsAny<CancellationToken>())).ReturnsAsync(header);

        // Act
        Result<SalaryHeaderResponse> result =
            await new GetSalaryHeaderQueryHandler(_repository.Object).Handle(new GetSalaryHeaderQuery(2),
                CancellationToken.None);

        // Assert
        Assert.Same(header, result.Value);
    }

    [Fact]
    public async Task GetHeader_Missing_ReturnsNotFound()
    {
        // Act
        Result<SalaryHeaderResponse> result =
            await new GetSalaryHeaderQueryHandler(_repository.Object).Handle(new GetSalaryHeaderQuery(2),
                CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
    }

    [Fact]
    public async Task CreateHeader_StoresTheDatesWithoutTimeAndReturnsTheId()
    {
        // Arrange
        SalaryHeader? added = null;
        _repository.Setup(r => r.AddHeader(It.IsAny<SalaryHeader>())).Callback<SalaryHeader>(h => added = h);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask).Callback(() => added!.ShId = 4);

        // Act
        Result<int> result = await new CreateSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateSalaryHeaderCommand(new SalaryHeaderRequest
            {
                ShChargeDate = Day(2026, 11, 5, 13), ShTransferDate = Day(2026, 11, 4, 9)
            }), CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Value);
        Assert.Equal(Day(2026, 11, 5), added!.ShChargeDate);
        Assert.Equal(Day(2026, 11, 4), added.ShTransferDate);
    }

    [Fact]
    public async Task UpdateHeader_ChangesTheDates()
    {
        // Arrange
        SalaryHeader header = WithHeader();

        // Act
        Result result = await new UpdateSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryHeaderCommand(2,
                new SalaryHeaderRequest { ShChargeDate = Day(2026, 12, 5, 8), ShTransferDate = Day(2026, 12, 6) }),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Day(2026, 12, 5), header.ShChargeDate);
        Assert.Equal(Day(2026, 12, 6), header.ShTransferDate);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task UpdateHeader_Missing_ReturnsNotFound()
    {
        // Act
        Result result = await new UpdateSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryHeaderCommand(2, new SalaryHeaderRequest()), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task DeleteHeader_WithoutData_RemovesIt()
    {
        // Arrange
        SalaryHeader header = WithHeader();

        // Act
        Result result = await new DeleteSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryHeaderCommand(2), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.RemoveHeader(header));
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task DeleteHeader_WithData_ReturnsConflict()
    {
        // Arrange
        WithHeader();
        _repository.Setup(r => r.HeaderHasData(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        Result result = await new DeleteSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryHeaderCommand(2), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderHasData);
        _repository.Verify(r => r.RemoveHeader(It.IsAny<SalaryHeader>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task DeleteHeader_Missing_ReturnsNotFound()
    {
        // Act
        Result result = await new DeleteSalaryHeaderCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryHeaderCommand(2), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
    }

    // --- მდგენელები ---

    [Fact]
    public async Task CreatePart_AddsThePartToTheHeader()
    {
        // Arrange
        WithHeader();
        SalaryPart? added = null;
        _repository.Setup(r => r.AddPart(It.IsAny<SalaryPart>())).Callback<SalaryPart>(p => added = p);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask).Callback(() => added!.SpId = 11);

        // Act
        Result<int> result = await new CreateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateSalaryPartCommand(2, PartRequest(DeductionType, 12.5m)), CancellationToken.None);

        // Assert
        Assert.Equal(11, result.Value);
        Assert.Equal((2, 7, (int?)DeductionType, 12.5m),
            (added!.ShId, added.TeacherContractId, added.SalaryPartTypeId, added.SpAmount));
    }

    [Fact]
    public async Task CreatePart_MissingHeader_ReturnsNotFound()
    {
        // Act
        Result<int> result = await new CreateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateSalaryPartCommand(2, PartRequest()), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
        _repository.Verify(r => r.AddPart(It.IsAny<SalaryPart>()), Times.Never);
    }

    [Theory]
    [InlineData(1, 50, nameof(SalaryErrors.PartIsCalculated))]
    [InlineData(77, 50, nameof(SalaryErrors.PartTypeNotFound))]
    [InlineData(DeductionType, 0, nameof(SalaryErrors.DeductionMustBePositive))]
    [InlineData(DeductionType, -5, nameof(SalaryErrors.DeductionMustBePositive))]
    public async Task CreatePart_BreakingATypeRule_ReturnsTheError(int typeId, decimal amount, string code)
    {
        // Arrange
        WithHeader();

        // Act
        Result<int> result = await new CreateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateSalaryPartCommand(2, PartRequest(typeId, amount)), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task CreatePart_NegativeAddition_IsAllowed()
    {
        // Arrange
        WithHeader();

        // Act
        Result<int> result = await new CreateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateSalaryPartCommand(2, PartRequest(AdditionType, -5m)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task UpdatePart_ChangesTheFields()
    {
        // Arrange
        SalaryPart part = WithPart(AdditionType);

        // Act
        Result result = await new UpdateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryPartCommand(9, PartRequest(DeductionType, 3m)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal((7, (int?)DeductionType, 3m), (part.TeacherContractId, part.SalaryPartTypeId, part.SpAmount));
        Assert.Equal(2, part.ShId);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task UpdatePart_CalculatedPart_ReturnsConflict()
    {
        // Arrange
        SalaryPart part = WithPart(1);

        // Act
        Result result = await new UpdateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryPartCommand(9, PartRequest()), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.PartIsCalculated);
        Assert.Equal(1m, part.SpAmount);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task UpdatePart_ToTheCalculatedType_ReturnsConflict()
    {
        // Arrange
        WithPart(AdditionType);

        // Act
        Result result = await new UpdateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryPartCommand(9, PartRequest(1)), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.PartIsCalculated);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task UpdatePart_Missing_ReturnsNotFound()
    {
        // Act
        Result result = await new UpdateSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateSalaryPartCommand(9, PartRequest()), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryPartNotFound);
    }

    [Fact]
    public async Task DeletePart_ManualPart_RemovesIt()
    {
        // Arrange
        SalaryPart part = WithPart(AdditionType);

        // Act
        Result result = await new DeleteSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryPartCommand(9), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.RemovePart(part));
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task DeletePart_CalculatedPart_ReturnsConflict()
    {
        // Arrange
        WithPart(1);

        // Act
        Result result = await new DeleteSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryPartCommand(9), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.PartIsCalculated);
        _repository.Verify(r => r.RemovePart(It.IsAny<SalaryPart>()), Times.Never);
    }

    [Fact]
    public async Task DeletePart_Missing_ReturnsNotFound()
    {
        // Act
        Result result = await new DeleteSalaryPartCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteSalaryPartCommand(9), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryPartNotFound);
    }

    // --- გამოთვლა ---

    private SalaryHeader WithCountData()
    {
        var oldLine = new SalaryLine { SaId = 30, ShId = 2, TeacherContractId = 1 };
        var oldLessonPart = new SalaryPart { SpId = 20, ShId = 2, TeacherContractId = 1, SalaryPartTypeId = 1 };
        var manualPart = new SalaryPart
        {
            SpId = 21, ShId = 2, TeacherContractId = 1, SalaryPartTypeId = AdditionType, SpAmount = 3m
        };
        var header = new SalaryHeader
        {
            ShId = 2,
            ShChargeDate = Day(2026, 10, 5),
            ShTransferDate = Day(2026, 10, 5),
            SalaryLines = [oldLine],
            SalaryParts = [oldLessonPart, manualPart]
        };
        _repository.Setup(r => r.GetHeaderWithDataForChange(2, It.IsAny<CancellationToken>())).ReturnsAsync(header);
        _repository.Setup(r => r.GetContracts(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SalaryContractData(1, true, false, false)]);
        _repository.Setup(r => r.GetLessonRows(Day(2026, 9, 1), Day(2026, 11, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SalaryLessonStudentRow(1, 100, 1, null, 11, Day(2026, 9, 10, 10), 1, 2f)]);
        _repository.Setup(r => r.GetOperationMonths(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Day(2026, 9, 1), Day(2026, 10, 1)]);
        _repository.Setup(r => r.GetHourRates(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [11] = 8m });
        _repository.Setup(r => r.GetPartTypeCountPlaces(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int?> { [1] = 1, [AdditionType] = 1 });
        _repository.Setup(r => r.RemoveLine(It.IsAny<SalaryLine>()))
            .Callback<SalaryLine>(l => header.SalaryLines.Remove(l));
        _repository.Setup(r => r.RemovePart(It.IsAny<SalaryPart>()))
            .Callback<SalaryPart>(p => header.SalaryParts.Remove(p));
        return header;
    }

    [Fact]
    public async Task Count_ReplacesTheLinesAndTheLessonPartsAndKeepsTheManualParts()
    {
        // Arrange
        SalaryHeader header = WithCountData();

        // Act
        Result<SalaryCountResponse> result =
            await new CountSalaryCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
                new CountSalaryCommand(2), CancellationToken.None);

        // Assert: 2 სთ × 8 = 16 (ტიპი 1) + 3 = 19 → 20
        Assert.Equal(new SalaryCountResponse(1, 1, 1), result.Value);
        _repository.Verify(r => r.RemoveLine(It.Is<SalaryLine>(l => l.SaId == 30)), Times.Once);
        _repository.Verify(r => r.RemovePart(It.Is<SalaryPart>(p => p.SpId == 20)), Times.Once);
        _repository.Verify(r => r.RemovePart(It.Is<SalaryPart>(p => p.SpId == 21)), Times.Never);
        Assert.Equal([(21, AdditionType, 3m), (0, 1, 16m)],
            header.SalaryParts.Select(p => (p.SpId, p.SalaryPartTypeId ?? 0, p.SpAmount)));
        SalaryLine line = Assert.Single(header.SalaryLines);
        Assert.Equal((1, 20m, 25m, 0.5m, 24.5m, 4.9m, 0m, 1m, 19.6m, Day(2026, 9, 1), (int?)1, 0m),
            (line.TeacherContractId, line.SaNetAmountRound, line.SaAmountGross, line.SaPension2,
                line.SaGrossMinusPension, line.SaIncomeTax, line.SaGamokvitva, line.SaPension4, line.SaAmountNet,
                line.SaMonthDate, line.RsQuoteTypeId, line.SaIndividualIncomeTax));
        SalaryLineDetail detail = Assert.Single(line.SalaryLinesDetails);
        Assert.Equal((100, 16m, 2f, 8m), (detail.GroupId, detail.SadAmount, detail.SadHoursCount, detail.SadHourCost));
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Count_ReportsTheDetailsOfAllLines()
    {
        // Arrange: contract 1 teaches one group, contract 2 two groups
        WithCountData();
        _repository.Setup(r => r.GetContracts(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SalaryContractData(1, true, false, false), new SalaryContractData(2, true, false, false)]);
        _repository.Setup(r => r.GetLessonRows(Day(2026, 9, 1), Day(2026, 11, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new SalaryLessonStudentRow(1, 100, 1, null, 11, Day(2026, 9, 10, 10), 1, 2f),
                new SalaryLessonStudentRow(2, 200, 2, null, 11, Day(2026, 9, 11, 10), 1, 1f),
                new SalaryLessonStudentRow(3, 300, 2, null, 11, Day(2026, 9, 12, 10), 1, 1f)
            ]);

        // Act
        Result<SalaryCountResponse> result =
            await new CountSalaryCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
                new CountSalaryCommand(2), CancellationToken.None);

        // Assert
        Assert.Equal(new SalaryCountResponse(2, 2, 3), result.Value);
    }

    [Fact]
    public async Task Count_MissingHeader_ReturnsNotFound()
    {
        // Act
        Result<SalaryCountResponse> result =
            await new CountSalaryCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
                new CountSalaryCommand(2), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
        VerifySaved(Times.Never());
    }

    // --- ფაილები ---

    [Fact]
    public async Task TransferFile_IsNamedByTheTransferDate()
    {
        // Arrange
        WithHeader();
        _repository.Setup(r => r.GetTransferFileRows(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TransferFileRow("GE01", null, "ა", "ბ", "1", 10m, null, "ხელფასი", "სექტემბერი", 2026)]);

        // Act
        Result<SalaryFile> result = await new GetTransferFileQueryHandler(_repository.Object).Handle(
            new GetTransferFileQuery(2), CancellationToken.None);

        // Assert
        Assert.Equal("salary_2026_10_4.csv", result.Value.FileName);
        Assert.EndsWith(" 1,GE01,ა ბ,\"1\",10,ხელფასი,სექტემბერი  2026",
            Encoding.UTF8.GetString(result.Value.Content), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferFile_MissingHeader_ReturnsNotFound()
    {
        // Act
        Result<SalaryFile> result = await new GetTransferFileQueryHandler(_repository.Object).Handle(
            new GetTransferFileQuery(2), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.SalaryHeaderNotFound);
    }

    [Fact]
    public async Task DeclarationFile_WithoutMonth_ReturnsTheError()
    {
        // Act
        Result<SalaryFile> result = await new GetDeclarationFileQueryHandler(_repository.Object).Handle(
            new GetDeclarationFileQuery(null), CancellationToken.None);

        // Assert
        AssertError(result, SalaryErrors.DeclarationMonthIsRequired);
        _repository.Verify(r => r.GetDeclarationFileRows(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeclarationFile_ReadsTheTransferDatesOfTheWholeMonth()
    {
        // Arrange
        _repository.Setup(r => r.GetDeclarationFileRows(Day(2026, 10, 1), Day(2026, 11, 1),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        Result<SalaryFile> result = await new GetDeclarationFileQueryHandler(_repository.Object).Handle(
            new GetDeclarationFileQuery(Day(2026, 10, 17, 15)), CancellationToken.None);

        // Assert
        Assert.Equal("TaxDepDeclaration_2026_10.csv", result.Value.FileName);
        Assert.Equal(SalaryFilesGenerator.DeclarationFile([]), result.Value.Content);
        _repository.Verify(r => r.GetDeclarationFileRows(Day(2026, 10, 1), Day(2026, 11, 1),
            It.IsAny<CancellationToken>()));
    }
}
