using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.CreateSalaryHeader;

//პასუხი ახალი უწყისის იდენტიფიკატორია
public sealed record CreateSalaryHeaderCommand(SalaryHeaderRequest? Request) : ICommand<int>;
