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

public sealed class UserMustHaveGroupOrdersModeRightsFilterTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDatabaseAbstraction> _databaseAbstraction = new();
    private readonly Mock<IUserRightsRepository> _repository = new();

    public UserMustHaveGroupOrdersModeRightsFilterTests()
    {
        _currentUser.Setup(u => u.Roles).Returns(["Admin"]);
        _databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        _repository.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
    }

    private async Task<(object? Result, bool NextCalled)> Invoke()
    {
        var filter = new UserMustHaveGroupOrdersModeRightsFilter(_repository.Object, _databaseAbstraction.Object,
            new Mock<ILogger<UserMustHaveGroupOrdersModeRightsFilter>>().Object, _currentUser.Object);
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
    public async Task InvokeAsync_RoleSeesTheModeMenuItem_CallsNext()
    {
        // Arrange
        _repository.Setup(r =>
                r.CheckMenuRight(5, "Admin", 5, 5, "groupOrdersMode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("next", result);
    }

    //ბრძანებების მენიუს უფლება ვიზარდებს არ ხსნის
    [Fact]
    public async Task InvokeAsync_RoleSeesOnlyTheGroupOrdersMenuItem_Returns403()
    {
        // Arrange
        _repository.Setup(r => r.CheckMenuRight(5, "Admin", 5, 5, "groupOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        (object? result, bool nextCalled) = await Invoke();

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    //D184: მენიუს პუნქტი „ბრძანებების რეჟიმი“, მხოლოდ Admin-ისთვის
    [Fact]
    public void MenuKey_IsTheGroupOrdersModeMenuItemKey()
    {
        Assert.Equal("groupOrdersMode", UserMustHaveGroupOrdersModeRightsFilter.MenuKey);
    }
}
