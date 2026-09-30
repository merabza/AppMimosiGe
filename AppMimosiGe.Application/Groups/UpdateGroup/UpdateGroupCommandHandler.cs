using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.UpdateGroup;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateGroupCommandHandler(IGroupsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateGroupCommand>
{
    public async Task<Result> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        GroupRequest request = command.Request!;

        Group? group = await repository.GetForChange(command.GrpId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.GroupNotFound);
        }

        if (!GroupMapper.RowsBelongToGroup(group, request))
        {
            return Result.Failure(GroupErrors.RowNotFound);
        }

        //მოსწავლის სტრიქონს, რომელზეც უკვე არის გაკვეთილები, LessonsByStudents Restrict-ით ებმის (Access-ის RI)
        GroupRemovedRows removedRows = GroupMapper.FindRemovedRows(group, request);
        if (removedRows.Students.Count > 0 && await repository.AnyStudentRowIsInUse(
                [.. removedRows.Students.Select(x => x.GbsId)], cancellationToken))
        {
            return Result.Failure(GroupErrors.GroupStudentIsInUse);
        }

        //ცვლილებამდელი მოსწავლეებიც: ამოშლილი ან სხვა კონტრაქტზე გადაყვანილი სტრიქონის კონტრაქტის დარიცხვაც იცვლება
        List<int> affectedStudentContractIds =
        [
            .. group.GroupsByStudents.Select(x => x.StudentContractId),
            .. request.Students.Select(x => x.StudentContractId)
        ];

        Dictionary<int, int> defaultSalarySchemes =
            await repository.GetDefaultSalarySchemes(request.Teachers, cancellationToken);
        GroupMapper.ApplyFields(group, request);
        GroupMapper.ApplyRows(group, request, defaultSalarySchemes);
        repository.RemoveRows(removedRows);

        //Access-ის FrmGroups.Form_BeforeUpdate და ქვე-ფორმების AfterUpdate/AfterDelConfirm: ჯგუფის ან მისი
        //ნებისმიერი სტრიქონის ცვლილება გაკვეთილების დაზუსტებას და მოსწავლეების შემდეგი გადახდის თარიღის
        //გადათვლას მოითხოვს. სხვა ჯგუფები და კონტრაქტები არ ინიშნება (D56)
        group.DirtyLessons = true;
        await repository.MarkNextPayDatesDirty(affectedStudentContractIds, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
