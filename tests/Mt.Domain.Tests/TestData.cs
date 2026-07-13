using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Tests;

/// <summary>Small helpers to build valid value objects and unwrap results in tests.</summary>
internal static class TestData
{
    public static readonly Result<ValueTuple> Ok = default(ValueTuple);

    public static T Unwrap<T>(this Result<T> result) =>
        result.IsCompleted(out var value, out var failures)
            ? value
            : throw new InvalidOperationException($"Expected completed but was failed: {failures[0].Message}");

    public static Id Id(long value = 1) => Migrations.Id.Create(value).Unwrap();

    public static OrganizationNumber Org(string value = "ORG-123") =>
        OrganizationNumber.Create(value).Unwrap();

    public static Attempt Attempt(int value) => Domain.Attempt.Create(value).Unwrap();

    public static Address Address() => Domain.Address.Create("Main Street 1", "Oslo").Unwrap();
}
