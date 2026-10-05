using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStudentContractNextNumberQueryHandler(IStudentContractsRepository repository)
    : IQueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>
{
    //ახალი კონტრაქტის ფორმა ნომერს ამით ავსებს; უნიკალურობას შენახვისას ვალიდატორი ისევ ამოწმებს
    public async Task<Result<StudentContractNextNumberResponse>> Handle(GetStudentContractNextNumberQuery request,
        CancellationToken cancellationToken)
    {
        AcademicYear? academicYear = await repository.GetAcademicYear(request.AcademicYearId, cancellationToken);
        if (academicYear is null)
        {
            return StudentContractErrors.AcademicYearNotFound;
        }

        return new StudentContractNextNumberResponse(ContractNumbers.Next(academicYear.StartDate,
            await repository.GetContractNumbers(request.AcademicYearId, cancellationToken)));
    }
}
