using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.CreateSalaryPart;

//პასუხი ახალი მდგენელის იდენტიფიკატორია
public sealed record CreateSalaryPartCommand(int ShId, SalaryPartRequest? Request) : ICommand<int>;
