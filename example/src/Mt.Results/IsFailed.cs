using System.Diagnostics.CodeAnalysis;

namespace Mt.Results;

public static class IsFailedExtensions
{
    extension<T>(Result<T> result)
    {
        /// <summary>
        /// True when failed, exposing the failures; false when completed, exposing the value.
        /// </summary>
        public bool IsFailed(
            [NotNullWhen(false)] out T? value,
            [NotNullWhen(true)] out Failure[]? failures)
        {
            if (result is Result<T>.Failed failed)
            {
                value = default;
                failures = [.. failed.Failures];
                return true;
            }

            value = ((Result<T>.Completed)result).Value!;
            failures = null;
            return false;
        }

        /// <summary>Mirror of <see cref="IsFailed"/>: true when completed.</summary>
        public bool IsCompleted(
            [NotNullWhen(true)] out T? value,
            [NotNullWhen(false)] out Failure[]? failures)
        {
            if (result is Result<T>.Completed completed)
            {
                value = completed.Value!;
                failures = null;
                return true;
            }

            value = default;
            failures = [.. ((Result<T>.Failed)result).Failures];
            return false;
        }
    }
}
