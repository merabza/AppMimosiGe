using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;

//ვიზარდი 2, „ჯგუფების რედაქტირებაზე დაბრუნება" (D195)
public sealed record SwitchToGroupEditingCommand : ICommand<SwitchToGroupEditingResponse>;
