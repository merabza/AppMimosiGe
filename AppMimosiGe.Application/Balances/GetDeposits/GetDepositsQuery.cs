using System;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Balances.GetDeposits;

//ბალანსები (Access-ის FrmDeposites). AcademicYearId null: ყველა წლის კონტრაქტები. DateTo: "თარიღამდე" (დღე, ჩათვლით).
//Filter: ცარიელი, "filter" ან "call" (EDepositsFilter-ის სახელი, რეგისტრის მიუხედავად)
public sealed record GetDepositsQuery(int? AcademicYearId, decimal Maximum, DateTime DateTo, string? Filter)
    : IQuery<DepositsResponse>;
