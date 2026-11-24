using System;
using System.Linq;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Validation;

/// <summary>
///     ბრძანების მოთხოვნის შემოწმება (GROUP_ORDERS.md §4, GroupOrderRequest): ტიპის მიხედვით საჭირო და აკრძალული
///     ნაწილები, ჯგუფის ველები (0, 1) და ხაზების ცნობარები და მნიშვნელობები. ტიპის პირობებს (§4), კოდის უნიკალურობას
///     (D186) და ჯგუფის სამიზნე მდგომარეობის წესებს handler-ი ამოწმებს
/// </summary>
public sealed class GroupOrderRequestValidator : AbstractValidator<GroupOrderRequest>
{
    public GroupOrderRequestValidator(IGroupOrdersRepository repository, IGroupsRepository groupsRepository,
        IStudentContractsRepository studentContractsRepository, ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.OrderType).Must(orderType => Enum.IsDefined((EGroupOrderType)orderType))
            .WithError(GroupOrderErrors.OrderTypeIsInvalid);
        RuleFor(x => x.OrderDate).NotEmpty().WithError(GroupOrderErrors.OrderDateIsRequired);
        RuleFor(x => x.Teachers).NotNull();
        RuleFor(x => x.Students).NotNull();
        RuleFor(x => x.DayTimePlaces).NotNull();

        When(x => Enum.IsDefined((EGroupOrderType)x.OrderType) && x.Teachers is not null && x.Students is not null &&
                  x.DayTimePlaces is not null, () =>
        {
            AddGroupRules(repository);
            AddGroupFieldRules(studentContractsRepository);
            AddLineCountRules();
            AddStudentTariffRules();
            AddLineDateRules();
            RuleForEach(x => x.Teachers)
                .SetValidator(new GroupOrderTeacherRequestValidator(groupsRepository, teacherContractsRepository));
            RuleForEach(x => x.Students).SetValidator(new GroupOrderStudentRequestValidator(groupsRepository));
            RuleForEach(x => x.DayTimePlaces)
                .SetValidator(new GroupOrderDayTimePlaceRequestValidator(groupsRepository));
        });
    }

    //ბრძანებები 2–10 ჯგუფს ეხება, გადაყვანა სამიზნე ჯგუფსაც. საწყისი და შექმნის ბრძანების ჯგუფს სერვერი იცნობს
    private void AddGroupRules(IGroupOrdersRepository repository)
    {
        RuleFor(x => x.GroupId).NotNull().WithError(GroupOrderErrors.GroupIsRequired).When(x => !CreatesGroup(x));
        RuleFor(x => x.GroupId).MustAsync((id, ct) => repository.GroupExists(id!.Value, ct))
            .WithError(GroupOrderErrors.GroupNotFound).When(x => !CreatesGroup(x) && x.GroupId is not null);
        RuleFor(x => x.GroupId).Null().WithError(GroupOrderErrors.CreateOrderCannotHaveGroup)
            .When(x => Type(x) == EGroupOrderType.CreateGroup);
        RuleFor(x => x.GroupId).Null().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => Type(x) == EGroupOrderType.Initial);

        RuleFor(x => x.TargetGroupId).NotNull().WithError(GroupOrderErrors.TargetGroupIsRequired)
            .When(IsTransfer);
        RuleFor(x => x.TargetGroupId).MustAsync((id, ct) => repository.GroupExists(id!.Value, ct))
            .WithError(GroupOrderErrors.TargetGroupNotFound).When(x => IsTransfer(x) && x.TargetGroupId is not null);
        RuleFor(x => x.TargetGroupId).Must((request, targetGroupId) => targetGroupId != request.GroupId)
            .WithError(GroupOrderErrors.TargetGroupIsSourceGroup)
            .When(x => IsTransfer(x) && x.TargetGroupId is not null);
        RuleFor(x => x.TargetGroupId).Null().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !IsTransfer(x));
    }

    //ჯგუფის ველები მხოლოდ საწყის და შექმნის ბრძანებაშია (D186), გაუქმების თარიღი მხოლოდ საწყისში (D196)
    private void AddGroupFieldRules(IStudentContractsRepository studentContractsRepository)
    {
        RuleFor(x => x.AcademicYearId)
            .MustAsync(async (id, ct) => id is { } value && await studentContractsRepository.AcademicYearExists(value, ct))
            .WithError(GroupErrors.AcademicYearNotFound).When(CreatesGroup);
        RuleFor(x => x.GroupCode).NotEmpty().WithError(GroupErrors.GroupCodeIsRequired).When(CreatesGroup);
        RuleFor(x => x.GroupCode).Must(code => code!.Trim().Length <= GroupRequestValidator.GroupCodeMaxLength)
            .WithError(GroupErrors.GroupCodeIsTooLong)
            .When(x => CreatesGroup(x) && !string.IsNullOrWhiteSpace(x.GroupCode));
        RuleFor(x => x.CourseId)
            .MustAsync(async (id, ct) => id is { } value && await studentContractsRepository.CourseExists(value, ct))
            .WithError(GroupErrors.CourseNotFound).When(CreatesGroup);
        RuleFor(x => x.GroupSizeId)
            .MustAsync(async (id, ct) => id is { } value && await studentContractsRepository.GroupSizeExists(value, ct))
            .WithError(GroupErrors.GroupSizeNotFound).When(CreatesGroup);
        RuleFor(x => x.StudentStatusId)
            .MustAsync(async (id, ct) =>
                id is { } value && await studentContractsRepository.StudentStatusExists(value, ct))
            .WithError(GroupErrors.StudentStatusNotFound).When(CreatesGroup);

        RuleFor(x => x)
            .Must(x => x.AcademicYearId is null && x.GroupCode is null && x.CourseId is null && x.GroupSizeId is null &&
                       x.StudentStatusId is null).WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !CreatesGroup(x));
        RuleFor(x => x.VoidDate).Null().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !IsInitial(x));
    }

    //§4: 2 და 9 ერთ მასწავლებელს ცვლის, შექმნის ბრძანებას მასწავლებელი შეიძლება ჯერ არ ჰქონდეს (დაპაუზდება, D186);
    //მოსწავლის გარეშე შექმნის ბრძანებაც არ ინახება; 6 და 10 ახალ განრიგს იწყებს. საწყის ბრძანებაში ყველა სტრიქონია
    private void AddLineCountRules()
    {
        RuleFor(x => x.Teachers).NotEmpty().WithError(GroupOrderErrors.TeacherIsRequired).When(ChangesTeacherRow);
        RuleFor(x => x.Teachers).Must(teachers => teachers.Count <= 1)
            .WithError(GroupOrderErrors.OnlyOneTeacherIsAllowed)
            .When(x => ChangesTeacherRow(x) || Type(x) == EGroupOrderType.CreateGroup);
        RuleFor(x => x.Teachers).Empty().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !ChangesTeacherRow(x) && !CreatesGroup(x));

        RuleFor(x => x.Students).NotEmpty().WithError(GroupOrderErrors.StudentsAreRequired).When(ChangesStudentRows);
        RuleFor(x => x.Students)
            .Must(students => students.Select(s => s.StudentContractId).Distinct().Count() == students.Count)
            .WithError(GroupOrderErrors.StudentIsRepeated).When(x => !IsInitial(x));
        RuleFor(x => x.Students).Empty().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !ChangesStudentRows(x) && !IsInitial(x));

        RuleFor(x => x.DayTimePlaces).NotEmpty().WithError(GroupOrderErrors.DayTimePlacesAreRequired)
            .When(ChangesSchedule);
        RuleFor(x => x.DayTimePlaces).Empty().WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => !ChangesSchedule(x) && !CreatesGroup(x));
    }

    //მოსწავლის ტარიფი ყველა მოსწავლის ბრძანებას აქვს, გაყვანის (4) გარდა. 4 კვირის საათებს სერვერი ფორმულით ითვლის
    //(D187): ხაზს ისინი მხოლოდ საწყის ბრძანებაში აქვს
    private void AddStudentTariffRules()
    {
        RuleForEach(x => x.Students)
            .Must((request, line) => line.FourWeekFee is not null && line.OneHourFee is not null &&
                                     line.HoursCoefficient is not null &&
                                     (!IsInitial(request) || line.FourWeekHours is not null))
            .WithError(GroupOrderErrors.StudentTariffIsRequired)
            .When(x => Type(x) != EGroupOrderType.RemoveStudents);
        RuleForEach(x => x.Students)
            .Must(line => line.FourWeekFee is null && line.OneHourFee is null && line.HoursCoefficient is null)
            .WithError(GroupOrderErrors.PartIsNotAllowedForOrderType)
            .When(x => Type(x) == EGroupOrderType.RemoveStudents);
        RuleForEach(x => x.Students).Must(line => line.FourWeekHours is null)
            .WithError(GroupOrderErrors.PartIsNotAllowedForOrderType).When(x => !IsInitial(x));
    }

    //ხაზის თარიღები მხოლოდ საწყის ბრძანებაშია, სადაც ყველა სტრიქონი თავისი თარიღებითაა (D196); სხვა ბრძანების
    //სტრიქონი მისი თარიღიდან იწყება
    private void AddLineDateRules()
    {
        RuleForEach(x => x.Teachers).Must(line => line.StartDate is not null)
            .WithError(GroupErrors.StartDateIsRequired).When(IsInitial);
        RuleForEach(x => x.Teachers).Must(line => line.StartDate is null && line.EndDate is null)
            .WithError(GroupOrderErrors.PartIsNotAllowedForOrderType).When(x => !IsInitial(x));
        RuleForEach(x => x.Students).Must(line => line.StartDate is not null)
            .WithError(GroupErrors.StartDateIsRequired).When(IsInitial);
        RuleForEach(x => x.Students).Must(line => line.StartDate is null && line.EndDate is null)
            .WithError(GroupOrderErrors.PartIsNotAllowedForOrderType).When(x => !IsInitial(x));
        RuleForEach(x => x.DayTimePlaces).Must(line => line.StartDate is not null)
            .WithError(GroupErrors.StartDateIsRequired).When(IsInitial);
        RuleForEach(x => x.DayTimePlaces).Must(line => line.StartDate is null && line.EndDate is null)
            .WithError(GroupOrderErrors.PartIsNotAllowedForOrderType).When(x => !IsInitial(x));
    }

    private static EGroupOrderType Type(GroupOrderRequest request)
    {
        return (EGroupOrderType)request.OrderType;
    }

    private static bool IsInitial(GroupOrderRequest request)
    {
        return Type(request) == EGroupOrderType.Initial;
    }

    //საწყისი და შექმნის ბრძანება ჯგუფის ველებს წერს
    private static bool CreatesGroup(GroupOrderRequest request)
    {
        return Type(request) is EGroupOrderType.Initial or EGroupOrderType.CreateGroup;
    }

    private static bool IsTransfer(GroupOrderRequest request)
    {
        return Type(request) == EGroupOrderType.TransferStudents;
    }

    private static bool ChangesTeacherRow(GroupOrderRequest request)
    {
        return Type(request) is EGroupOrderType.ChangeTeacher or EGroupOrderType.ChangeSalaryScheme;
    }

    private static bool ChangesStudentRows(GroupOrderRequest request)
    {
        return Type(request) is EGroupOrderType.CreateGroup or EGroupOrderType.AddStudents
            or EGroupOrderType.RemoveStudents or EGroupOrderType.TransferStudents or EGroupOrderType.ChangeStudentFees
            or EGroupOrderType.ChangeWeeklyHours;
    }

    private static bool ChangesSchedule(GroupOrderRequest request)
    {
        return Type(request) is EGroupOrderType.ChangeSchedule or EGroupOrderType.ChangeWeeklyHours;
    }
}
