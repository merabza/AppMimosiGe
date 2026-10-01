using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Lessons.UpdateLesson;

// ReSharper disable once UnusedType.Global
public sealed class UpdateLessonCommandValidator : AbstractValidator<UpdateLessonCommand>
{
    //სვეტების სიგრძე (Access-ისაც)
    public const int TextMaxLength = 255;

    public UpdateLessonCommandValidator(ILessonsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!.LessonStatusId).MustAsync((id, ct) => repository.LessonStatusExists(id, ct))
                .WithErrorCode(LessonErrors.LessonStatusNotFound.Code)
                .WithMessage(LessonErrors.LessonStatusNotFound.Description);

            //შემცვლელი შეიძლება არ იყოს
            RuleFor(x => x.Request!.SubstituteTeacherContractId)
                .MustAsync((id, ct) => repository.TeacherContractExists(id!.Value, ct))
                .When(x => x.Request!.SubstituteTeacherContractId is not null)
                .WithErrorCode(LessonErrors.SubstituteTeacherContractNotFound.Code)
                .WithMessage(LessonErrors.SubstituteTeacherContractNotFound.Description);

            //Access-ის და ბაზის CHECK: >= 0
            RuleFor(x => x.Request!.TeacherLateMinutes).GreaterThanOrEqualTo(0)
                .WithErrorCode(LessonErrors.TeacherLateMinutesMustNotBeNegative.Code)
                .WithMessage(LessonErrors.TeacherLateMinutesMustNotBeNegative.Description);

            RuleFor(x => x.Request!.Note).Must(IsShortText).WithErrorCode(LessonErrors.NoteIsTooLong.Code)
                .WithMessage(LessonErrors.NoteIsTooLong.Description);

            RuleFor(x => x.Request!.Students).NotNull();
            RuleFor(x => x.Request!.Students)
                .Must(students => students.Select(s => s.Id).Distinct().Count() == students.Count)
                .When(x => x.Request!.Students is not null).WithErrorCode(LessonErrors.StudentRowIsDuplicated.Code)
                .WithMessage(LessonErrors.StudentRowIsDuplicated.Description);
            RuleForEach(x => x.Request!.Students).SetValidator(new LessonStudentRequestValidator());
        });
    }

    //სიგრძე მოწმდება ისე, როგორც ინახება: trim-ის შემდეგ (LessonMapper.NormalizeText)
    public static bool IsShortText(string? value)
    {
        return (LessonMapper.NormalizeText(value)?.Length ?? 0) <= TextMaxLength;
    }

    private sealed class LessonStudentRequestValidator : AbstractValidator<LessonStudentRequest>
    {
        public LessonStudentRequestValidator()
        {
            RuleFor(x => x.StudentLateMinutes).GreaterThanOrEqualTo(0)
                .WithErrorCode(LessonErrors.StudentLateMinutesMustNotBeNegative.Code)
                .WithMessage(LessonErrors.StudentLateMinutesMustNotBeNegative.Description);
            RuleFor(x => x.Theme).Must(IsShortText).WithErrorCode(LessonErrors.ThemeIsTooLong.Code)
                .WithMessage(LessonErrors.ThemeIsTooLong.Description);
            RuleFor(x => x.TeacherComment).Must(IsShortText).WithErrorCode(LessonErrors.TeacherCommentIsTooLong.Code)
                .WithMessage(LessonErrors.TeacherCommentIsTooLong.Description);
            RuleFor(x => x.StudentComment).Must(IsShortText).WithErrorCode(LessonErrors.StudentCommentIsTooLong.Code)
                .WithMessage(LessonErrors.StudentCommentIsTooLong.Description);
        }
    }
}
