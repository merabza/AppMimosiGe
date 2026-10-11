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

public sealed class UserMustHaveCheckPaymentsRightFilterTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDatabaseAbstraction> _databaseAbstraction = new();
    private readonly Mock<IUserRightsRepository> _repository = new();

    public UserMustHaveCheckPaymentsRightFilterTests()
    {
        _currentUser.Setup(u => u.Roles).Returns(["Manager", "Accountant"]);
        _databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        _repository.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
    }

    private async Task<(object? Result, bool NextCalled)> Invoke()
    {
        var filter = new UserMustHaveCheckPaymentsRightFilter(_repository.Object, _databaseAbstraction.Object,
            new Mock<ILogger<UserMustHaveCheckPaymentsRightFilter>>().Object, _currentUser.Object);
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
    public async Task InvokeAsync_OneOfTheRolesHasTheClaim_CallsNext()
    {
        // Arrange
        _repository.Setup(r => r.CheckRight(5, "Accountant", 5, "CheckPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
    }

    [Fact]
    public async Task InvokeAsync_NoRoleHasTheClaim_Returns403()
    {
        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    // the handlers' PaymentClaims.CheckPayments, seeded as MimNewAppClaimsRulesCreator.CheckPaymentsKey
    [Fact]
    public void ClaimKey_IsTheSeededAppClaimKey()
    {
        Assert.Equal("CheckPayments", UserMustHaveCheckPaymentsRightFilter.ClaimKey);
    }
}
