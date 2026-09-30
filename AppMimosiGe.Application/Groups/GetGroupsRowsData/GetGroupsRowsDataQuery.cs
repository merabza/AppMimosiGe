using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.GetGroupsRowsData;

//FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetGroupsRowsDataQuery(string FilterSortRequest) : IQuery<GroupsRowsDataResponse>;
