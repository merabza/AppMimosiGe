using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.GetPaymentStudentContracts;

//სასწავლო წლის მოსწავლეების კონტრაქტები გადახდის ფორმისა და სიის ფილტრის ძებნადი არჩევისთვის
public sealed record GetPaymentStudentContractsQuery(int AcademicYearId) : IQuery<List<LookupItemResponse>>;
