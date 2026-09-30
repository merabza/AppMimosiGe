using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.CreateGroup;

//პასუხი ახალი ჯგუფის იდენტიფიკატორია
public sealed record CreateGroupCommand(GroupRequest? Request) : ICommand<int>;
