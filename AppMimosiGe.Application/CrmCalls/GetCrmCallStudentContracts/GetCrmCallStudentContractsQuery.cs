using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;

//სასწავლო წლის მოსწავლეების კონტრაქტები ზარის ფორმისა და სიის ფილტრის ძებნადი არჩევისთვის
public sealed record GetCrmCallStudentContractsQuery(int AcademicYearId) : IQuery<List<LookupItemResponse>>;
