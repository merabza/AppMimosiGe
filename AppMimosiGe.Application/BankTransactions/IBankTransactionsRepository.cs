using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.BankTransactions;

/// <summary>
///     ბანკის ჩარიცხვების, მათი განაწილებისა და დასწავლილი გადამხდელების რეპოზიტორია. რეალიზაცია
///     AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IBankTransactionsRepository
{
    Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default);

    //ანგარიშის ბოლო ჩარიცხვის თარიღი; null, თუ ჩარიცხვა ჯერ არ არის
    Task<DateTime?> GetLastTransactionDate(int bankAccountId, CancellationToken cancellationToken = default);

    //ამ იდენტიფიკატორებიდან რომელი ჩარიცხვაა უკვე შენახული
    Task<HashSet<string>> GetExistingMovementIds(int bankAccountId, IReadOnlyCollection<string> movementIds,
        CancellationToken cancellationToken = default);

    Task<List<MatchContractData>> GetMatchContracts(int academicYearId, CancellationToken cancellationToken = default);

    //ყველა წლის კონტრაქტის ნომერი → მოსწავლე (ერთი ნომრის რამდენიმე კონტრაქტისას ბოლო ID-ისა)
    Task<Dictionary<string, int>> GetStudentByContractNumber(CancellationToken cancellationToken = default);

    Task<List<LearnedPayer>> GetLearnedPayers(CancellationToken cancellationToken = default);

    //თარიღების ფილტრი ორივე ჩათვლით; თარიღით (ბოლო პირველი) და ID-ით დალაგებული
    Task<List<BankTransactionRowResponse>> GetRows(DateTime? dateFrom, DateTime? dateTo,
        EBankTransactionsFilter filter, CancellationToken cancellationToken = default);

    Task<BankTransactionResponse?> GetOne(int bankTransactionId, CancellationToken cancellationToken = default);

    //შესაცვლელად, გადახდებით, EF-ის თვალყურის დევნებით
    Task<BankTransaction?> GetForChange(int bankTransactionId, CancellationToken cancellationToken = default);

    //კონტრაქტი → მოსწავლე
    Task<Dictionary<int, int>> GetStudentHumanIds(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    Task<List<StudentPayer>> GetStudentPayers(IReadOnlyCollection<int> studentHumanIds,
        CancellationToken cancellationToken = default);

    void Add(BankTransaction bankTransaction);
    void AddStudentPayer(StudentPayer studentPayer);
    void RemovePayment(Payment payment);
}
