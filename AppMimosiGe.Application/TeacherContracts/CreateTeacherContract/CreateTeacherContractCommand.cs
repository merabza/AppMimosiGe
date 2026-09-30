using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;

//პასუხი ახალი კონტრაქტის იდენტიფიკატორია
public sealed record CreateTeacherContractCommand(TeacherContractRequest? Request) : ICommand<int>;
