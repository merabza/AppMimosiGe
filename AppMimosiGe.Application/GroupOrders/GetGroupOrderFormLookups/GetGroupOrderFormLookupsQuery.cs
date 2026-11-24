using System;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrderFormLookups;

//ყველა პარამეტრი არასავალდებულოა. AcademicYearId: წლის ჯგუფები, კონტრაქტები და მათი ნაგულისხმევი ტარიფი. GrpId და
//TargetGrpId: ჯგუფის (2–10) და გადაყვანის სამიზნე ჯგუფის (5) მდგომარეობა OrderDate-ზე, რომელიც მაშინ სავალდებულოა.
//StudentStatusId: წელთან ერთად კოდის შეთავაზება (D186). CourseId და GroupSizeId: შექმნის ბრძანების ნაგულისხმევი
//ტარიფი (D218). OrderId: შესაცვლელი ბრძანება; მდგომარეობა მის გარეშე ითვლება და მისი კოდი თავისუფლად ითვლება
public sealed record GetGroupOrderFormLookupsQuery(
    int? AcademicYearId,
    DateTime? OrderDate,
    int? GrpId,
    int? TargetGrpId,
    int? StudentStatusId,
    int? CourseId,
    int? GroupSizeId,
    int? OrderId) : IQuery<GroupOrderFormLookupsResponse>;
