using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.LessonGenerator;

/// <summary>
///     გაკვეთილების გენერატორის რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface ILessonGeneratorRepository
{
    //ბოლო სამუშაო თვე (OperationMonths); null, თუ კალენდარი ცარიელია
    Task<DateTime?> GetLastOperationMonth(CancellationToken cancellationToken = default);

    void AddOperationMonths(IEnumerable<DateTime> months);

    //Access-ის SetAllDirty: ყველა ჯგუფს DirtyLessons, ყველა კონტრაქტს DirtyNextPayDate (EF-ის თვალყურის დევნებით)
    Task MarkAllDirty(CancellationToken cancellationToken = default);

    //ჯგუფები ID-ის ზრდადობით, როგორც Access-ის CheckAllLessons-ში; onlyDirty: მხოლოდ DirtyLessons-იანი
    Task<List<int>> GetGroupIds(bool onlyDirty, CancellationToken cancellationToken = default);

    //ჯგუფი მასწავლებლებით, მოსწავლეებით, განრიგით (დაწყების დროით), გაკვეთილებით (მოსწავლეებით) და გენერატორის ლოგით.
    //ყველაფერი ერთად იტვირთება; forChange: EF-ის თვალყურის დევნებით
    Task<Group?> GetGroupForGeneration(int grpId, bool forChange, CancellationToken cancellationToken = default);

    //DirtyNextPayDate-ის დასაყენებლად, EF-ის თვალყურის დევნებით
    Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    //შეცდომების ტექსტები კოდით (ErrorLogTexts)
    Task<Dictionary<int, string>> GetErrorLogTexts(CancellationToken cancellationToken = default);

    //გენერატორის ლოგი ჯგუფის კოდით, თარიღით და ID-ით დალაგებული; grpId: მხოლოდ ერთი ჯგუფის
    Task<List<LessonGeneratorLogRowResponse>> GetLog(int? grpId, CancellationToken cancellationToken = default);

    //ახალი გაკვეთილი მოსწავლეების სტრიქონებით
    void AddLesson(Lesson lesson);

    //გაკვეთილი იშლება მოსწავლეების სტრიქონებთან ერთად (ისინი გაკვეთილს Restrict-ით ებმის)
    void RemoveLesson(Lesson lesson);

    void RemoveLessonStudent(LessonByStudent lessonStudent);

    //ჯგუფის ძველი ლოგი ახლით იცვლება (Access-ის ClearErrorMessage)
    void ReplaceLogs(Group group, IEnumerable<LessonCheckCreateErrorLog> logs);
}
