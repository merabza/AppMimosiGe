using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     ბალანსების სიის პარამეტრები: Access-ის FrmDeposites-ის "მაქსიმუმი", "თარიღამდე" (დღე, ჩათვლით), ფილტრის ღილაკი და
///     დღევანდელი თარიღი
/// </summary>
public sealed record DepositsParameters(decimal Maximum, DateTime DateTo, EDepositsFilter Filter, DateTime Today);
