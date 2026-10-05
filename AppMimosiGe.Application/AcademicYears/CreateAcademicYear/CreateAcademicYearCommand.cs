using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.AcademicYears.CreateAcademicYear;

//ოსტატის ნაბიჯი 1: მომდევნო სასწავლო წლის დამატება. DryRun: მხოლოდ გეგმა, ბაზაში არაფერი იწერება
public sealed record CreateAcademicYearCommand(bool DryRun) : ICommand<NewAcademicYearResponse>;
