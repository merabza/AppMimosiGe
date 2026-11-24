using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;

//ვიზარდი 1, „ბრძანებებზე გადასვლა" (D194): DryRun-ში მხოლოდ შემოწმების რეპორტი, ბაზაში არაფერი იწერება
public sealed record SwitchToGroupOrdersCommand(bool DryRun) : ICommand<SwitchToGroupOrdersResponse>;
