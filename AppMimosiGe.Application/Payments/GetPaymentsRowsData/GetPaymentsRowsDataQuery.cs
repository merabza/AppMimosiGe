using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.GetPaymentsRowsData;

//FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetPaymentsRowsDataQuery(string FilterSortRequest) : IQuery<PaymentsRowsDataResponse>;
