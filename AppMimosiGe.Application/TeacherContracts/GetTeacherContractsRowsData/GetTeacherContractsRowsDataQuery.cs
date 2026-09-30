using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;

//FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetTeacherContractsRowsDataQuery(string FilterSortRequest)
    : IQuery<TeacherContractsRowsDataResponse>;
