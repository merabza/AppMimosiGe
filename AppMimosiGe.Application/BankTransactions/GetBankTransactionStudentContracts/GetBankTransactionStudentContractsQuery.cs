using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransactionStudentContracts;

//სასწავლო წლის კონტრაქტები ჩარიცხვის ხელით განაწილების ძებნადი არჩევისთვის (გადახდების ფორმის სია)
public sealed record GetBankTransactionStudentContractsQuery(int AcademicYearId) : IQuery<List<LookupItemResponse>>;
