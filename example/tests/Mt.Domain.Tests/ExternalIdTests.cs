using Mt.Domain.ExternalIds;
using Mt.Results;
using Xunit;

namespace Mt.Domain.Tests;

/// <summary>The closed value objects of the external-id ledger reject unknown input (spec 8).</summary>
public class ExternalIdTests
{
    [Theory]
    [InlineData("Source")]
    [InlineData("Target")]
    [InlineData("Portal")]
    public void ExternalSystem_accepts_the_closed_set(string value)
    {
        Assert.True(ExternalSystem.Create(value).IsCompleted(out var system, out _));
        Assert.Equal(value, system.Value);
    }

    [Fact]
    public void ExternalSystem_rejects_unknown_strings()
    {
        Assert.True(ExternalSystem.Create("Mordor").IsFailed(out _, out _));
    }

    [Theory]
    [InlineData("PunchCard")]
    [InlineData("HoloCrystal")]
    [InlineData("CarrierPigeon")]
    public void IdType_accepts_the_closed_set(string value)
    {
        Assert.True(IdType.Create(value).IsCompleted(out var type, out _));
        Assert.Equal(value, type.Value);
    }

    [Fact]
    public void IdType_rejects_unknown_strings()
    {
        Assert.True(IdType.Create("FaxNumber").IsFailed(out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IdValue_rejects_empty(string value)
    {
        Assert.True(IdValue.Create(value).IsFailed(out _, out _));
    }

    [Fact]
    public void IdValue_rejects_over_long_values()
    {
        Assert.True(IdValue.Create(new string('x', IdValue.MaxLength + 1)).IsFailed(out _, out _));
        Assert.True(IdValue.Create(new string('x', IdValue.MaxLength)).IsCompleted(out _, out _));
    }

    [Fact]
    public void Start_input_ids_reject_empty_values()
    {
        Assert.True(PunchCardNumber.Create("").IsFailed(out _, out _));
        Assert.True(HoloCrystalId.Create(" ").IsFailed(out _, out _));
        Assert.True(CarrierPigeonTag.Create("").IsFailed(out _, out _));
        Assert.True(PunchCardNumber.Create("PC-1972-0042").IsCompleted(out _, out _));
    }
}
