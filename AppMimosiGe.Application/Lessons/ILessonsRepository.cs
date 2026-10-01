using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Lessons;

/// <summary>
///     გაკვეთილების ჟურნალის რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface ILessonsRepository
{
    Task<LessonsRowsDataResponse> GetRowsData(LessonsListQuery query, CancellationToken cancellationToken = default);

    Task<LessonResponse?> GetOne(int lessonId, CancellationToken cancellationToken = default);

    //შესაცვლელად: გაკვეთილი მოსწავლეების სტრიქონებით, EF-ის თვალყურის დევნებით
    Task<Lesson?> GetForChange(int lessonId, CancellationToken cancellationToken = default);

    Task<bool> LessonStatusExists(int id, CancellationToken cancellationToken = default);
    Task<bool> TeacherContractExists(int id, CancellationToken cancellationToken = default);

    //"კოდი / სასწავლო წელი", ახალი წლები პირველად, წლის შიგნით კოდით
    Task<List<LookupItemResponse>> GetGroups(CancellationToken cancellationToken = default);

    //"გვარი სახელი / ნომერი"-თ დალაგებული, როგორც Access-ის ფორმის ჩამოსაშლელ სიებში
    Task<List<LookupItemResponse>> GetTeacherContracts(CancellationToken cancellationToken = default);

    //ID-ით, როგორც Access-ის ფორმაზე
    Task<List<LookupItemResponse>> GetLessonStatuses(CancellationToken cancellationToken = default);

    //dirty ალმის დასაყენებლად, EF-ის თვალყურის დევნებით: ჯგუფის ყველა მოსწავლის კონტრაქტი და ამ გაკვეთილის
    //მოსწავლეების კონტრაქტები
    Task<List<StudentContract>> GetStudentContractsForChange(int grpId, int lessonId,
        CancellationToken cancellationToken = default);
}
