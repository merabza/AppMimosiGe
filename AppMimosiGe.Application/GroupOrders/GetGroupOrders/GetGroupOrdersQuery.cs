using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrders;

//FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetGroupOrdersQuery(string FilterSortRequest) : IQuery<GroupOrdersRowsDataResponse>;
