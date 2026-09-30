using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.GetGroupStudentContracts;

//ჯგუფის სასწავლო წლის მოსწავლეების კონტრაქტები ტარიფებით (ჯგუფის ფორმის მოსწავლის ასარჩევად)
public sealed record GetGroupStudentContractsQuery(int AcademicYearId)
    : IQuery<List<GroupStudentContractLookupResponse>>;
