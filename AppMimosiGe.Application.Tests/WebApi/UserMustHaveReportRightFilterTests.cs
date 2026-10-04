using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.WebApi.Filters;
using BackendCarcassShared.Contracts.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

public sealed class UserMustHaveReportRightFilterTests
{
    private readonly Mock<IUserClaimRights> _claimRights = new();

    public UserMustHaveReportRightFilterTests()
    {
        _claimRights.Setup(c => c.GetClaims(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "r03RoomsAgenda", "CheckPayments" });
    }

    private async Task<(object? Result, bool NextCalled)> Invoke(string? key)
    {
        var httpContext = new DefaultHttpContext();
        if (key is not null)
        {
            httpContext.Request.RouteValues[UserMustHaveReportRightFilter.KeyRouteValue] = key;
        }

        bool nextCalled = false;
        object? result = await new UserMustHaveReportRightFilter(_claimRights.Object).InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext, new List<object?>()), _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("next");
            });
        return (result, nextCalled);
    }

    // the report's app claim has the report's key, in any case of the link
    [Theory]
    [InlineData("r03RoomsAgenda")]
    [InlineData("R03ROOMSAGENDA")]
    public async Task InvokeAsync_UserHasTheReportsClaim_CallsNext(string key)
    {
        // Act
        (object? result, bool nextCalled) = await Invoke(key);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
    }

    // 403 with the carcass's error body
    [Fact]
    public async Task InvokeAsync_UserLacksTheReportsClaim_Returns403()
    {
        // Act
        (object? result, bool nextCalled) = await Invoke("r06RoomOver");

        // Assert
        Assert.False(nextCalled);
        var json = Assert.IsType<JsonHttpResult<Error[]>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        Assert.Equal(RightsApiErrors.InsufficientRights.Code, Assert.Single(json.Value!).Code);
    }

    // an unknown report is the handler's 404; the claims are not even read
    [Fact]
    public async Task InvokeAsync_UnknownReport_CallsNextWithoutReadingTheClaims()
    {
        // Act
        (object? result, bool nextCalled) = await Invoke("r35TeacherLineOver");

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
        _claimRights.Verify(c => c.GetClaims(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NoKeyInTheRoute_CallsNext()
    {
        // Act
        (_, bool nextCalled) = await Invoke(null);

        // Assert
        Assert.True(nextCalled);
    }
}
