using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;

//FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetStudentContractsRowsDataQuery(string FilterSortRequest)
    : IQuery<StudentContractsRowsDataResponse>;
