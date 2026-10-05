using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class StudentAccountsTests
{
    //contracts 1 and 2 are student 100's (two academic years), contract 3 is student 200's
    private static readonly Dictionary<int, int> StudentByContract = new() { [1] = 100, [2] = 100, [3] = 200 };

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 100)]
    [InlineData(3, 200)]
    public void AccountOf_IsTheStudentOfTheContract(int studentContractId, int expected)
    {
        Assert.Equal(expected, StudentAccounts.AccountOf(StudentByContract, studentContractId));
    }

    // a contract outside the map is its own account, with a key no student id can have
    [Fact]
    public void AccountOf_UnknownContract_IsTheNegativeContractId()
    {
        Assert.Equal(-7, StudentAccounts.AccountOf(StudentByContract, 7));
    }

    [Fact]
    public void ByAccount_GroupsTheItemsOfEveryContractOfTheStudentInTheirOrder()
    {
        // Arrange: (item, contract)
        (string Name, int ScId)[] items = [("a", 2), ("b", 3), ("c", 1), ("d", 2), ("e", 7)];

        // Act
        ILookup<int, (string Name, int ScId)> accounts =
            StudentAccounts.ByAccount(items, StudentByContract, item => item.ScId);

        // Assert
        Assert.Equal([100, 200, -7], accounts.Select(g => g.Key));
        Assert.Equal(["a", "c", "d"], accounts[100].Select(i => i.Name));
        Assert.Equal(["b"], accounts[200].Select(i => i.Name));
        Assert.Equal(["e"], accounts[-7].Select(i => i.Name));
    }
}
