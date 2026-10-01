using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Balances.GetStatement;

//ამონაწერი (Access-ის FrmChargesAndPayments). FilterSortRequest carcass-ის GridView-ის ფორმატითაა: base64-ში ჩაწერილი JSON
public sealed record GetStatementQuery(string FilterSortRequest) : IQuery<StatementRowsDataResponse>;
