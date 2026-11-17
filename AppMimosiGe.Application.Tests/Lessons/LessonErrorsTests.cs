using System.Reflection;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonErrorsTests
{
    //every error property of the lessons journal with its type and the exact text the user sees
    public static readonly TheoryData<Error, ErrorType, string> FixedErrors = new()
    {
        { LessonErrors.LessonNotFound, ErrorType.NotFound, "გაკვეთილი ვერ მოიძებნა" },
        { LessonErrors.LessonStatusNotFound, ErrorType.Problem, "გაკვეთილის სტატუსი ვერ მოიძებნა" },
        {
            LessonErrors.SubstituteTeacherContractNotFound, ErrorType.Problem,
            "შემცვლელი მასწავლებლის კონტრაქტი ვერ მოიძებნა"
        },
        {
            LessonErrors.TeacherLateMinutesMustNotBeNegative, ErrorType.Problem,
            "მასწავლებლის დაგვიანების წუთები უარყოფითი ვერ იქნება"
        },
        {
            LessonErrors.StudentLateMinutesMustNotBeNegative, ErrorType.Problem,
            "მოსწავლის დაგვიანების წუთები უარყოფითი ვერ იქნება"
        },
        { LessonErrors.NoteIsTooLong, ErrorType.Problem, "შენიშვნა 255 სიმბოლოზე გრძელი ვერ იქნება" },
        { LessonErrors.ThemeIsTooLong, ErrorType.Problem, "თემა 255 სიმბოლოზე გრძელი ვერ იქნება" },
        {
            LessonErrors.TeacherCommentIsTooLong, ErrorType.Problem,
            "მასწავლებლის კომენტარი 255 სიმბოლოზე გრძელი ვერ იქნება"
        },
        {
            LessonErrors.StudentCommentIsTooLong, ErrorType.Problem,
            "მოსწავლის კომენტარი 255 სიმბოლოზე გრძელი ვერ იქნება"
        },
        { LessonErrors.StudentRowIsDuplicated, ErrorType.Problem, "მოსწავლის სტრიქონი მოთხოვნაში ორჯერ არის" },
        { LessonErrors.StudentRowNotFound, ErrorType.Problem, "მოსწავლის სტრიქონი ამ გაკვეთილს არ ეკუთვნის" },
        {
            LessonErrors.NotHeldLessonCannotHavePresentStudents, ErrorType.Problem,
            "გაუქმებულ გაკვეთილს დამსწრე მოსწავლე ვერ ექნება: მოხსენით დასწრება ან სტატუსი შეცვალეთ \"არ " +
            "გაუქმებულა\"-თი"
        },
        {
            LessonErrors.NotHeldLessonCannotHaveRecoverDate, ErrorType.Problem,
            "გაუქმებულ გაკვეთილს აღდგენის თარიღი ვერ ექნება: წაშალეთ თარიღი ან სტატუსი შეცვალეთ \"არ " +
            "გაუქმებულა\"-თი"
        },
        {
            LessonErrors.ExtraLessonCannotHaveEnteredData, ErrorType.Conflict,
            "ჯგუფის ახლანდელი მონაცემებით (განრიგი, მასწავლებლები, მოსწავლეები, გაუქმების თარიღი) ეს " +
            "გაკვეთილი ზედმეტია და გენერატორმა ის უნდა წაშალოს, ამიტომ მოსწავლეებს დასწრებას, თემას, " +
            "შეფასებას და კომენტარებს ვეღარ შეუტანთ. თუ გაკვეთილი ჩატარდა, ჯერ ჯგუფი შეასწორეთ"
        },
        {
            LessonErrors.FilterSortRequestIsInvalid, ErrorType.Problem,
            "სიის ფილტრის ან დალაგების პარამეტრები არასწორია"
        }
    };

    private static PropertyInfo[] ErrorProperties() =>
        typeof(LessonErrors).GetProperties(BindingFlags.Public | BindingFlags.Static);

    [Theory]
    [MemberData(nameof(FixedErrors))]
    public void FixedError_HasItsTypeAndText(Error error, ErrorType type, string description)
    {
        Assert.Equal(type, error.Type);
        Assert.Equal(description, error.Description);
    }

    //a new error property must get its row in FixedErrors
    [Fact]
    public void FixedErrors_ListEveryErrorProperty()
    {
        Assert.Equal(ErrorProperties().Length, FixedErrors.Count);
    }

    [Fact]
    public void ErrorProperties_CodeIsThePropertyName()
    {
        Assert.All(ErrorProperties(), p => Assert.Equal(p.Name, ((Error)p.GetValue(null)!).Code));
    }

    //D177: the names in the given order, separated by commas
    [Fact]
    public void ExtraStudentsCannotHaveEnteredData_ListsTheStudents()
    {
        Error error = LessonErrors.ExtraStudentsCannotHaveEnteredData(["Alpha Ann", "Beta Bob"]);

        Assert.Equal(nameof(LessonErrors.ExtraStudentsCannotHaveEnteredData), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            "ჯგუფის ახლანდელი მონაცემებით ამ მოსწავლეების სტრიქონები გაკვეთილში ზედმეტია და გენერატორმა ისინი " +
            "უნდა წაშალოს, ამიტომ მათ დასწრებას, თემას, შეფასებას და კომენტარებს ვეღარ შეუტანთ: Alpha Ann, Beta Bob. " +
            "თუ მოსწავლე გაკვეთილს დაესწრო, ჯერ ჯგუფში მისი თარიღები შეასწორეთ", error.Description);
    }

    //D178: the teacher and the lessons in the given order, separated by semicolons
    [Fact]
    public void TeacherHasAnotherLessonAtThisTime_NamesTheTeacherAndTheLessons()
    {
        Error error = LessonErrors.TeacherHasAnotherLessonAtThisTime("Alpha Ann / T5", ["first", "second"]);

        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            "Alpha Ann / T5 ამ გაკვეთილის დროს სხვა გაკვეთილს ატარებს: first; second. ერთი მასწავლებელი ერთსა და " +
            "იმავე დროს ორ გაკვეთილს ვერ ჩაატარებს: შეცვალეთ შემცვლელი ან აღდგენის თარიღი", error.Description);
    }
}
