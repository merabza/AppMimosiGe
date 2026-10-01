using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.WebApi.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SystemTools.SystemToolsShared;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

public sealed class UserMustHaveDepositsRightsFilterTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDatabaseAbstraction> _databaseAbstraction = new();
    private readonly Mock<IUserRightsRepository> _repository = new();

    public UserMustHaveDepositsRightsFilterTests()
    {
        _currentUser.Setup(u => u.Roles).Returns(["Manager"]);
        _databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        _repository.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
    }

    private async Task<(object? Result, bool NextCalled)> Invoke()
    {
        var filter = new UserMustHaveDepositsRightsFilter(_repository.Object, _databaseAbstraction.Object,
            new Mock<ILogger<UserMustHaveDepositsRightsFilter>>().Object, _currentUser.Object);
        bool nextCalled = false;
        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new List<object?>()), _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("next");
            });
        return (result, nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RoleSeesTheDepositsMenuItem_CallsNext()
    {
        // Arrange
        _repository.Setup(r => r.CheckMenuRight(5, "Manager", 5, 5, "deposits", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
    }

    [Fact]
    public async Task InvokeAsync_RoleDoesNotSeeTheMenuItem_Returns403()
    {
        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public void MenuKey_IsTheDepositsMenuItemKey()
    {
        Assert.Equal("deposits", UserMustHaveDepositsRightsFilter.MenuKey);
    }
}
