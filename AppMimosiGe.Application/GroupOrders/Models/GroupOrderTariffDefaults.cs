using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების ხაზების ნაგულისხმევი ტარიფი (D187). ყველა ტარიფი ბრძანებაშივე იცვლება. მოსწავლის 4 კვირის საათებს
///     მაინც ფორმულა იძლევა (GroupOrderFormulas): თუ ისინი ნაგულისხმევი ტარიფის საათებისგან განსხვავდება, რედაქტორი
///     გადასახადს ფასითა და ფორმულის საათებით გადაითვლის
/// </summary>
public static class GroupOrderTariffDefaults
{
    /// <summary>
    ///     მოსწავლე (1, 3; D58): კონტრაქტის დეტალი ჯგუფის საგნითა და ზომით, კოეფიციენტით 1. რამდენიმე ასეთი დეტალიდან
    ///     პირველი, როგორც ჯგუფის რედაქტორში; null, თუ ასეთი დეტალი არ არის
    /// </summary>
    public static GroupStudentTariff? StudentTariff(IEnumerable<StudentContractTariff> contractTariffs, int courseId,
        int groupSizeId)
    {
        return contractTariffs.FirstOrDefault(t => t.CourseId == courseId && t.GroupSizeId == groupSizeId) is
            { } detail
            ? new GroupStudentTariff(detail.FourWeekHours, detail.FourWeekFee, detail.OneHourFee, 1f)
            : null;
    }

    /// <summary>
    ///     გადაყვანა (5): კონტრაქტის დეტალი სამიზნე ჯგუფის საგნითა და ზომით; თუ ასეთი არ არის, „საიდან“ ჯგუფის
    ///     სტრიქონის ტარიფი
    /// </summary>
    public static GroupStudentTariff TransferStudentTariff(IEnumerable<StudentContractTariff> contractTariffs,
        GroupFields target, GroupStudentRow sourceRow)
    {
        return StudentTariff(contractTariffs, target.CourseId, target.GroupSizeId) ??
               new GroupStudentTariff(sourceRow.FourWeekHours, sourceRow.FourWeekFee, sourceRow.OneHourFee,
                   sourceRow.HoursCoefficient);
    }

    /// <summary>
    ///     მასწავლებელი (1, 2): მოთხოვნის სქემა, ხოლო თუ ის არ არის, კონტრაქტის ძირითადი სქემა (SalarySchemaByHours,
    ///     D58); null, თუ არცერთი არ არის
    /// </summary>
    public static int? TeacherSalarySchemeId(int? requestedSalarySchemaId, int? contractMainSalarySchemaId)
    {
        return requestedSalarySchemaId ?? contractMainSalarySchemaId;
    }
}
