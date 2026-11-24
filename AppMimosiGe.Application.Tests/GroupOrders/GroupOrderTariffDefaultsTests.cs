using AppMimosiGe.Application.GroupOrders.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ნაგულისხმევი ტარიფი (D187). ჯგუფის საგანი 6-ია, ზომა 2 (GroupOrdersTestData.Fields)
public sealed class GroupOrderTariffDefaultsTests
{
    //კონტრაქტის დეტალები: საგანი 6 ზომით 3, საგანი 7 ზომით 2 და საგანი 6 ზომით 2 ორჯერ
    private static readonly StudentContractTariff[] ContractTariffs =
    [
        new(6, 3, 6f, 42m, 7m), new(7, 2, 12f, 96m, 8m), new(6, 2, 12f, 72m, 6m), new(6, 2, 8f, 48m, 6m)
    ];

    //„საიდან“ ჯგუფის სტრიქონი კოეფიციენტით 1.5
    private static readonly GroupStudentRow SourceRow = new(21, 20, 18f, 108m, 6m, 1.5f, null, Day(9, 1), null);

    //D58: კონტრაქტის დეტალი ჯგუფის საგნითა და ზომით, კოეფიციენტით 1; რამდენიმედან პირველი, როგორც ჯგუფის რედაქტორში
    [Fact]
    public void StudentTariff_TakesTheDetailOfTheGroupCourseAndSize()
    {
        Assert.Equal(new GroupStudentTariff(12f, 72m, 6m, 1f),
            GroupOrderTariffDefaults.StudentTariff(ContractTariffs, Fields.CourseId, Fields.GroupSizeId));
    }

    //D58: ასეთი დეტალი თუ არ არის, ნაგულისხმევი ტარიფი არ არის: საგანიც და ზომაც უნდა ემთხვეოდეს
    [Theory]
    [InlineData(8, 2)]
    [InlineData(7, 3)]
    public void StudentTariff_NoDetailOfTheCourseAndSize_IsNull(int courseId, int groupSizeId)
    {
        Assert.Null(GroupOrderTariffDefaults.StudentTariff(ContractTariffs, courseId, groupSizeId));
    }

    //D187: გადაყვანისას სამიზნე ჯგუფის საგნისა და ზომის დეტალი, „საიდან“-ის ტარიფის მიუხედავად
    [Fact]
    public void TransferStudentTariff_TakesTheDetailOfTheTargetCourseAndSize()
    {
        Assert.Equal(new GroupStudentTariff(6f, 42m, 7m, 1f),
            GroupOrderTariffDefaults.TransferStudentTariff(ContractTariffs, Fields with { GroupSizeId = 3 },
                SourceRow));
    }

    //D187: ასეთი დეტალი თუ არ არის, „საიდან“-ის სტრიქონის ტარიფი, კოეფიციენტის ჩათვლით
    [Fact]
    public void TransferStudentTariff_NoDetail_TakesTheSourceRowTariff()
    {
        Assert.Equal(new GroupStudentTariff(18f, 108m, 6m, 1.5f),
            GroupOrderTariffDefaults.TransferStudentTariff(ContractTariffs, Fields with { CourseId = 8 }, SourceRow));
    }

    //D187: მოთხოვნის სქემა, ხოლო თუ ის არ არის, მასწავლებლის კონტრაქტის ძირითადი სქემა
    [Theory]
    [InlineData(9, 8, 9)]
    [InlineData(9, null, 9)]
    [InlineData(null, 8, 8)]
    [InlineData(null, null, null)]
    public void TeacherSalarySchemeId_TheRequestedOrTheContractMainScheme(int? requestedSalarySchemaId,
        int? contractMainSalarySchemaId, int? salarySchemaId)
    {
        Assert.Equal(salarySchemaId,
            GroupOrderTariffDefaults.TeacherSalarySchemeId(requestedSalarySchemaId, contractMainSalarySchemaId));
    }
}
