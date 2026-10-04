using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტების მონაცემების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IReportsRepository
{
    //განრიგი თარიღისთვის: აქტიური ჯგუფები (Access-ის vActiveGroupsForReports) და მათი მოსწავლის, მასწავლებლისა და
    //განრიგის სტრიქონები, რომლებიც ამ დღეს მოქმედებს
    Task<ScheduleSnapshot> GetSchedule(DateTime date, CancellationToken cancellationToken = default);

    //ფილტრების ჩამოსაშლელი სიები: მასწავლებლები, საგნები, მოსწავლეები
    Task<ReportLookupsResponse> GetLookups(CancellationToken cancellationToken = default);
}
