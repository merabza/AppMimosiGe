using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Balances.GetStatementStudentContracts;

//ამონაწერის ფილტრის "მოსწავლე": სასწავლო წლის კონტრაქტები "გვარი სახელი ნომერი"-თ (Access-ის ჩამოსაშლელი სია)
public sealed record GetStatementStudentContractsQuery(int AcademicYearId) : IQuery<List<LookupItemResponse>>;
