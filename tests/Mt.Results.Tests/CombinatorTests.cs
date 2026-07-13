using Mt.Results;
using Xunit;

namespace Mt.Results.Tests;

public class CombinatorTests
{
    private static Result<int> Ok(int value) => value;

    private static Result<int> Fail(string message) => new ValidationFailure(message);

    [Fact]
    public void ImplicitConversions_produce_completed_and_failed()
    {
        Result<int> completed = 42;
        Result<int> failed = new NotFoundFailure("missing");

        Assert.IsType<Result<int>.Completed>(completed);
        Assert.IsType<Result<int>.Failed>(failed);
    }

    [Fact]
    public void IsFailed_and_IsCompleted_are_mirrors()
    {
        Assert.True(Fail("boom").IsFailed(out var value, out var failures));
        Assert.Equal(default, value);
        Assert.Single(failures);

        Assert.True(Ok(7).IsCompleted(out var okValue, out var okFailures));
        Assert.Equal(7, okValue);
        Assert.Null(okFailures);
    }

    [Fact]
    public void Then_value_overload_maps_success()
    {
        var result = Ok(2).Then(x => x + 3);
        Assert.True(result.IsCompleted(out var value, out _));
        Assert.Equal(5, value);
    }

    [Fact]
    public void Then_bind_overload_chains_results()
    {
        var result = Ok(2).Then(x => Ok(x * 10));
        Assert.True(result.IsCompleted(out var value, out _));
        Assert.Equal(20, value);
    }

    [Fact]
    public void Then_short_circuits_on_failure()
    {
        var ran = false;
        var result = Fail("stop").Then(x =>
        {
            ran = true;
            return x + 1;
        });

        Assert.False(ran);
        Assert.True(result.IsFailed(out _, out var failures));
        Assert.Equal("stop", failures[0].Message);
    }

    [Fact]
    public async Task ThenAsync_binds_async_producer()
    {
        var produced = await Ok(4).ThenAsync(x => Task.FromResult<Result<int>>(x * 2));
        Assert.True(produced.IsCompleted(out var value, out _));
        Assert.Equal(8, value);
    }

    [Fact]
    public void Map_transforms_success_only()
    {
        Assert.True(Ok(3).Map(x => x.ToString()).IsCompleted(out var value, out _));
        Assert.Equal("3", value);
        Assert.True(Fail("x").Map(v => v + 1).IsFailed(out _, out _));
    }

    [Fact]
    public void Tap_runs_effect_on_success_only_and_returns_unchanged()
    {
        var seen = 0;
        var result = Ok(9).Tap(x => seen = x);
        Assert.Equal(9, seen);
        Assert.True(result.IsCompleted(out var value, out _));
        Assert.Equal(9, value);

        seen = -1;
        Fail("no").Tap(x => seen = x);
        Assert.Equal(-1, seen);
    }

    [Fact]
    public void Match_folds_each_case_into_a_plain_value()
    {
        Assert.Equal("5", Ok(5).Match(v => v.ToString(), failures => failures[0].Message));
        Assert.Equal("boom", Fail("boom").Match(v => v.ToString(), failures => failures[0].Message));
    }

    [Fact]
    public async Task Done_is_the_completed_unit_result()
    {
        Assert.True(Done.Result.IsCompleted(out _, out _));
        Assert.Same(Done.Result, await Done.Task);
    }

    [Fact]
    public void Failed_requires_at_least_one_failure()
    {
        Assert.Throws<ArgumentException>(() => new Result<int>.Failed([]));
    }

    [Fact]
    public void FailWhen_on_raw_value_validates()
    {
        Assert.True("".FailWhen(string.IsNullOrEmpty, "empty").IsFailed(out _, out var failures));
        Assert.IsType<ValidationFailure>(failures[0]);
        Assert.True("ok".FailWhen(string.IsNullOrEmpty, "empty").IsCompleted(out var value, out _));
        Assert.Equal("ok", value);
    }

    [Fact]
    public void FailWhen_chains_on_result()
    {
        var result = "value"
            .FailWhen(string.IsNullOrEmpty, "empty")
            .FailWhen(s => s.Length > 3, "too long");

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.Equal("too long", failures[0].Message);
    }

    [Fact]
    public void EnsureFound_maps_null_to_not_found()
    {
        string? missing = null;
        Assert.True(missing.EnsureFound("gone").IsFailed(out _, out var failures));
        Assert.IsType<NotFoundFailure>(failures[0]);
    }

    [Fact]
    public void EnsureNotNull_maps_null_to_unexpected_null()
    {
        string? missing = null;
        Assert.True(missing.EnsureNotNull("null").IsFailed(out _, out var failures));
        Assert.IsType<UnexpectedNullFailure>(failures[0]);
    }

    [Fact]
    public void EnsureNoDuplicates_detects_duplicates()
    {
        Assert.True(new[] { 1, 2, 2 }.EnsureNoDuplicates("dup").IsFailed(out _, out var failures));
        Assert.IsType<DuplicateFailure>(failures[0]);
        Assert.True(new[] { 1, 2, 3 }.EnsureNoDuplicates("dup").IsCompleted(out _, out _));
    }
}
