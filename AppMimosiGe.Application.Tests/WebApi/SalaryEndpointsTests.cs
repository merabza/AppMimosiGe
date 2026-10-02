using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Moq;
using Serilog;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

public sealed class SalaryEndpointsTests
{
    private static readonly DateTime Fifth = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<ICommandHandler<TCommand>> CommandHandler<TCommand>(Result result) where TCommand : ICommand
    {
        var handler = new Mock<ICommandHandler<TCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<ICommandHandler<TCommand, TResponse>> CommandHandler<TCommand, TResponse>(
        Result<TResponse> result) where TCommand : ICommand<TResponse>
    {
        var handler = new Mock<ICommandHandler<TCommand, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static void AssertProblem(IResult result, int statusCode)
    {
        Assert.Equal(statusCode, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetHeaders_Success_ReturnsOk()
    {
        // Arrange
        List<SalaryHeaderRowResponse> headers = [new(1, Fifth, Fifth, 0, 0m)];

        // Act
        Results<Ok<List<SalaryHeaderRowResponse>>, ProblemHttpResult> result = await SalaryEndpoints.GetHeaders(
            QueryHandler<GetSalaryHeadersQuery, List<SalaryHeaderRowResponse>>(headers).Object);

        // Assert
        Assert.Same(headers, Assert.IsType<Ok<List<SalaryHeaderRowResponse>>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new SalaryFormLookupsResponse([], []);

        // Act
        Results<Ok<SalaryFormLookupsResponse>, ProblemHttpResult> result = await SalaryEndpoints.GetFormLookups(
            QueryHandler<GetSalaryFormLookupsQuery, SalaryFormLookupsResponse>(lookups).Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<SalaryFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        var header = new SalaryHeaderResponse(2, Fifth, Fifth, [], [], []);
        Mock<IQueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse>> handler =
            QueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse>(header);

        // Act
        Results<Ok<SalaryHeaderResponse>, ProblemHttpResult> result = await SalaryEndpoints.GetOne(2, handler.Object);

        // Assert
        Assert.Same(header, Assert.IsType<Ok<SalaryHeaderResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetSalaryHeaderQuery(2), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404()
    {
        // Act
        Results<Ok<SalaryHeaderResponse>, ProblemHttpResult> result = await SalaryEndpoints.GetOne(2,
            QueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse>(SalaryErrors.SalaryHeaderNotFound).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsTheId()
    {
        // Arrange
        var request = new SalaryHeaderRequest { ShChargeDate = Fifth, ShTransferDate = Fifth };
        Mock<ICommandHandler<CreateSalaryHeaderCommand, int>> handler =
            CommandHandler<CreateSalaryHeaderCommand, int>(4);

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await SalaryEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(4, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateSalaryHeaderCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Invalid_Returns400()
    {
        // Act
        Results<Ok<int>, ProblemHttpResult> result = await SalaryEndpoints.Create(new SalaryHeaderRequest(),
            CommandHandler<CreateSalaryHeaderCommand, int>(SalaryErrors.ChargeDateIsRequired).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        var request = new SalaryHeaderRequest { ShChargeDate = Fifth, ShTransferDate = Fifth };
        Mock<ICommandHandler<UpdateSalaryHeaderCommand>> handler =
            CommandHandler<UpdateSalaryHeaderCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.Update(2, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateSalaryHeaderCommand(2, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.Update(2, new SalaryHeaderRequest(),
            CommandHandler<UpdateSalaryHeaderCommand>(Result.Failure(SalaryErrors.SalaryHeaderNotFound)).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOk()
    {
        // Arrange
        Mock<ICommandHandler<DeleteSalaryHeaderCommand>> handler =
            CommandHandler<DeleteSalaryHeaderCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.Delete(2, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteSalaryHeaderCommand(2), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_WithData_Returns409()
    {
        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.Delete(2,
            CommandHandler<DeleteSalaryHeaderCommand>(Result.Failure(SalaryErrors.SalaryHeaderHasData)).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreatePart_Success_ReturnsTheIdAndPassesTheHeader()
    {
        // Arrange
        var request = new SalaryPartRequest { TeacherContractId = 1, SalaryPartTypeId = 3, SpAmount = 5m };
        Mock<ICommandHandler<CreateSalaryPartCommand, int>> handler = CommandHandler<CreateSalaryPartCommand, int>(9);

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await SalaryEndpoints.CreatePart(2, request, handler.Object);

        // Assert
        Assert.Equal(9, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateSalaryPartCommand(2, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task CreatePart_CalculatedType_Returns409()
    {
        // Act
        Results<Ok<int>, ProblemHttpResult> result = await SalaryEndpoints.CreatePart(2, new SalaryPartRequest(),
            CommandHandler<CreateSalaryPartCommand, int>(SalaryErrors.PartIsCalculated).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UpdatePart_Success_ReturnsOkAndPassesThePart()
    {
        // Arrange
        var request = new SalaryPartRequest { TeacherContractId = 1, SalaryPartTypeId = 4, SpAmount = 5m };
        Mock<ICommandHandler<UpdateSalaryPartCommand>> handler =
            CommandHandler<UpdateSalaryPartCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.UpdatePart(9, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateSalaryPartCommand(9, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task UpdatePart_NegativeDeduction_Returns400()
    {
        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.UpdatePart(9, new SalaryPartRequest(),
            CommandHandler<UpdateSalaryPartCommand>(Result.Failure(SalaryErrors.DeductionMustBePositive)).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeletePart_Success_ReturnsOk()
    {
        // Arrange
        Mock<ICommandHandler<DeleteSalaryPartCommand>> handler =
            CommandHandler<DeleteSalaryPartCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.DeletePart(9, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteSalaryPartCommand(9), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeletePart_NotFound_Returns404()
    {
        // Act
        Results<Ok, ProblemHttpResult> result = await SalaryEndpoints.DeletePart(9,
            CommandHandler<DeleteSalaryPartCommand>(Result.Failure(SalaryErrors.SalaryPartNotFound)).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Count_Success_ReturnsTheCounts()
    {
        // Arrange
        var counts = new SalaryCountResponse(6, 6, 15);
        Mock<ICommandHandler<CountSalaryCommand, SalaryCountResponse>> handler =
            CommandHandler<CountSalaryCommand, SalaryCountResponse>(counts);

        // Act
        Results<Ok<SalaryCountResponse>, ProblemHttpResult> result = await SalaryEndpoints.Count(2, handler.Object);

        // Assert
        Assert.Same(counts, Assert.IsType<Ok<SalaryCountResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CountSalaryCommand(2), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Count_NotFound_Returns404()
    {
        // Act
        Results<Ok<SalaryCountResponse>, ProblemHttpResult> result = await SalaryEndpoints.Count(2,
            CommandHandler<CountSalaryCommand, SalaryCountResponse>(SalaryErrors.SalaryHeaderNotFound).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TransferFile_Success_ReturnsTheCsvFile()
    {
        // Arrange
        var file = new SalaryFile("salary_2026_10_5.csv", [1, 2, 3]);
        Mock<IQueryHandler<GetTransferFileQuery, SalaryFile>> handler =
            QueryHandler<GetTransferFileQuery, SalaryFile>(file);

        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result = await SalaryEndpoints.TransferFile(2, handler.Object);

        // Assert
        FileContentHttpResult content = Assert.IsType<FileContentHttpResult>(result.Result);
        Assert.Equal((file.FileName, "text/csv"), (content.FileDownloadName, content.ContentType));
        Assert.Equal(file.Content, content.FileContents.ToArray());
        handler.Verify(h => h.Handle(new GetTransferFileQuery(2), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task TransferFile_NotFound_Returns404()
    {
        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result = await SalaryEndpoints.TransferFile(2,
            QueryHandler<GetTransferFileQuery, SalaryFile>(SalaryErrors.SalaryHeaderNotFound).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeclarationFile_Success_ReturnsTheCsvFileAndPassesTheMonth()
    {
        // Arrange
        var file = new SalaryFile("TaxDepDeclaration_2026_10.csv", [1]);
        Mock<IQueryHandler<GetDeclarationFileQuery, SalaryFile>> handler =
            QueryHandler<GetDeclarationFileQuery, SalaryFile>(file);

        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result =
            await SalaryEndpoints.DeclarationFile(Fifth, handler.Object);

        // Assert
        FileContentHttpResult content = Assert.IsType<FileContentHttpResult>(result.Result);
        Assert.Equal((file.FileName, "text/csv"), (content.FileDownloadName, content.ContentType));
        handler.Verify(h => h.Handle(new GetDeclarationFileQuery(Fifth), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeclarationFile_NoMonth_Returns400()
    {
        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result = await SalaryEndpoints.DeclarationFile(null,
            QueryHandler<GetDeclarationFileQuery, SalaryFile>(SalaryErrors.DeclarationMonthIsRequired).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/salary/headers", "GET")]
    [InlineData("api/v1/salary/formlookups", "GET")]
    [InlineData("api/v1/salary/{shId:int}", "GET")]
    [InlineData("api/v1/salary/", "POST")]
    [InlineData("api/v1/salary/{shId:int}", "PUT")]
    [InlineData("api/v1/salary/{shId:int}", "DELETE")]
    [InlineData("api/v1/salary/{shId:int}/parts", "POST")]
    [InlineData("api/v1/salary/parts/{spId:int}", "PUT")]
    [InlineData("api/v1/salary/parts/{spId:int}", "DELETE")]
    [InlineData("api/v1/salary/{shId:int}/count", "POST")]
    [InlineData("api/v1/salary/{shId:int}/transferfile", "GET")]
    [InlineData("api/v1/salary/declarationfile", "GET")]
    public async Task UseAppMimosiGeApi_MapsSalaryEndpoints(string pattern, string method)
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseAppMimosiGeApi(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Contains(endpoints,
            e => e.RoutePattern.RawText?.TrimStart('/') == pattern &&
                 e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public async Task UseSalaryEndpoints_MapsTwelveEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseSalaryEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(12, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseSalaryEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseSalaryEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(SalaryEndpoints.UseSalaryEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(SalaryEndpoints.UseSalaryEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the salary endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheSalaryStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(SalaryEndpoints.UseSalaryEndpoints)),
            Times.Once);
    }
}
