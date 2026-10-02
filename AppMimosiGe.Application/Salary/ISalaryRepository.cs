using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Salary;

/// <summary>
///     ხელფასის უწყისების, მდგენელებისა და სტრიქონების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface ISalaryRepository
{
    //დარიცხვის თარიღით (Access-ის ფორმის რიგი)
    Task<List<SalaryHeaderRowResponse>> GetHeaders(CancellationToken cancellationToken = default);

    Task<SalaryHeaderResponse?> GetHeader(int shId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად, EF-ის თვალყურის დევნებით
    Task<SalaryHeader?> GetHeaderForChange(int shId, CancellationToken cancellationToken = default);

    //გამოთვლისთვის: უწყისი მისი მდგენელებით და სტრიქონებით, EF-ის თვალყურის დევნებით
    Task<SalaryHeader?> GetHeaderWithDataForChange(int shId, CancellationToken cancellationToken = default);

    Task<bool> HeaderExists(int shId, CancellationToken cancellationToken = default);
    Task<bool> HeaderHasData(int shId, CancellationToken cancellationToken = default);

    Task<SalaryPart?> GetPartForChange(int spId, CancellationToken cancellationToken = default);

    Task<bool> EmployeeExists(int teacherContractId, CancellationToken cancellationToken = default);

    //null: ტიპი არ არსებობს
    Task<SalaryPartTypeLookupResponse?> GetPartType(int sptId, CancellationToken cancellationToken = default);

    Task<List<LookupItemResponse>> GetEmployeeLookups(CancellationToken cancellationToken = default);
    Task<List<SalaryPartTypeLookupResponse>> GetPartTypeLookups(CancellationToken cancellationToken = default);

    //გამოთვლის მონაცემები: ყველა კონტრაქტი, [from, toExclusive)-ის გაკვეთილები, სამუშაო თვეები, სქემები და ტიპები
    Task<List<SalaryContractData>> GetContracts(CancellationToken cancellationToken = default);

    Task<List<SalaryLessonStudentRow>> GetLessonRows(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default);

    Task<List<DateTime>> GetOperationMonths(CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetHourRates(CancellationToken cancellationToken = default);
    Task<Dictionary<int, int?>> GetPartTypeCountPlaces(CancellationToken cancellationToken = default);

    Task<List<TransferFileRow>> GetTransferFileRows(int shId, CancellationToken cancellationToken = default);

    //უწყისები, რომელთა გადარიცხვის თარიღი [from, toExclusive)-შია
    Task<List<DeclarationFileRow>> GetDeclarationFileRows(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default);

    void AddHeader(SalaryHeader header);
    void RemoveHeader(SalaryHeader header);
    void AddPart(SalaryPart part);
    void RemovePart(SalaryPart part);
    void RemoveLine(SalaryLine line);
}
