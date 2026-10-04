using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Infrastructure.Rights;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Repositories;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using Moq;
using SystemTools.SystemToolsShared;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class UserClaimRightsTests
{
    private readonly Mock<IUserClaimsRepository> _claimsRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDatabaseAbstraction> _databaseAbstraction = new();
    private readonly Mock<IUserRightsRepository> _repository = new();

    public UserClaimRightsTests()
    {
        _currentUser.Setup(u => u.Roles).Returns(["Manager", "Admin"]);
        _currentUser.Setup(u => u.Name).Returns("user1");
        _databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        _repository.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
    }

    private UserClaimRights CreateRights()
    {
        return new UserClaimRights(_repository.Object, _claimsRepository.Object, _currentUser.Object,
            _databaseAbstraction.Object, new Mock<ILogger<UserClaimRights>>().Object);
    }

    private Task<bool> HasClaim()
    {
        return CreateRights().HasClaim("CheckPayments");
    }

    // the same list the carcass gives the SPA at login: user -> roles -> app claims
    [Fact]
    public async Task GetClaims_ReturnsTheClaimsOfTheCurrentUser()
    {
        // Arrange
        _claimsRepository.Setup(r => r.UserAppClaims("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["r03RoomsAgenda", "CheckPayments", "r03RoomsAgenda"]);

        // Act
        IReadOnlySet<string> claims = await CreateRights().GetClaims();

        // Assert
        Assert.Equal(2, claims.Count);
        Assert.Contains("r03RoomsAgenda", claims);
        Assert.Contains("CheckPayments", claims);
        Assert.DoesNotContain("R03ROOMSAGENDA", claims);
    }

    [Fact]
    public async Task HasClaim_OneOfTheRolesHasTheClaim_IsTrue()
    {
        // Arrange
        _repository.Setup(r => r.CheckRight(5, "Admin", 5, "CheckPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        bool result = await HasClaim();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasClaim_NoRoleHasTheClaim_IsFalse()
    {
        Assert.False(await HasClaim());
    }

    // the carcass data types are missing, so the right cannot be determined: no right
    [Fact]
    public async Task HasClaim_RightsCannotBeDetermined_IsFalse()
    {
        // Arrange
        _repository.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        _repository.Setup(r => r.CheckRight(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        bool result = await HasClaim();

        // Assert
        Assert.False(result);
    }
}
