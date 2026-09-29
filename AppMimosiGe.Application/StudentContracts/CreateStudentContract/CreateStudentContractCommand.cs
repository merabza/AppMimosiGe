using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.CreateStudentContract;

//პასუხი ახალი კონტრაქტის იდენტიფიკატორია
public sealed record CreateStudentContractCommand(StudentContractRequest? Request) : ICommand<int>;
