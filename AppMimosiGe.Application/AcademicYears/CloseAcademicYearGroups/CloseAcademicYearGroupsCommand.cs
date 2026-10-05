using System;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;

//ოსტატის ნაბიჯი 2: სასწავლო წლის ღია ჯგუფებს VoidDate = CloseDate (ცარიელი: წლის დასრულება) ესმება და გენერატორი
//თარიღის შემდგომ გაკვეთილებს შლის. DryRun: მხოლოდ გეგმა, ბაზაში არაფერი იწერება
public sealed record CloseAcademicYearGroupsCommand(int AyId, DateTime? CloseDate, bool DryRun)
    : ICommand<CloseAcademicYearGroupsResponse>;
