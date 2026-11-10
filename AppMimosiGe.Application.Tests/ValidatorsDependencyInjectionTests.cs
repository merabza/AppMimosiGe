using System;
using System.Linq;
using System.Reflection;
using FluentValidation;
using Xunit;

namespace AppMimosiGe.Application.Tests;

public sealed class ValidatorsDependencyInjectionTests
{
    // AddFluentValidation registers every validator of the Application assembly in DI, nested request validators
    // too, and the host validates the container at start: a constructor parameter DI can't give (a record, a
    // nullable value) stops the app. So every parameter must be a service interface
    [Fact]
    public void EveryValidator_TakesOnlyServiceInterfaces()
    {
        Type[] validators =
        [
            .. typeof(AssemblyReference).Assembly.GetTypes().Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
        ];

        Assert.NotEmpty(validators);
        Assert.All(validators, validator =>
        {
            ConstructorInfo constructor = Assert.Single(validator.GetConstructors());
            Assert.All(constructor.GetParameters(), p => Assert.True(p.ParameterType.IsInterface,
                $"{validator.Name}({p.ParameterType.Name} {p.Name})"));
        });
    }
}
