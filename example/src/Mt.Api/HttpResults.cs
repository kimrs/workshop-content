using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Api;

/// <summary>
/// Maps a <see cref="Result{T}"/> to an HTTP result (§9): completed → 200; <see cref="NotFoundFailure"/>
/// → 404; <see cref="DuplicateFailure"/> (a start for an org with an active migration) /
/// <see cref="MigrationHasIncorrectState"/> / <see cref="ExternalIdConflictFailure"/> (an external
/// id claimed by another active migration, spec 8) → 409; <see cref="ValidationFailure"/> → 400;
/// anything else → 500.
/// </summary>
public static class HttpResults
{
    extension<T>(Result<T> result)
    {
        public IResult ToHttpResult()
        {
            if (result.IsCompleted(out var value, out _))
            {
                return Microsoft.AspNetCore.Http.Results.Ok(value);
            }

            var failure = ((Result<T>.Failed)result).Failures[0];
            return failure switch
            {
                NotFoundFailure => Microsoft.AspNetCore.Http.Results.NotFound(new { error = failure.Message }),
                DuplicateFailure or MigrationHasIncorrectState or ExternalIdConflictFailure =>
                    Microsoft.AspNetCore.Http.Results.Conflict(new { error = failure.Message }),
                ValidationFailure => Microsoft.AspNetCore.Http.Results.BadRequest(new { error = failure.Message }),
                _ => Microsoft.AspNetCore.Http.Results.Json(new { error = failure.Message }, statusCode: 500),
            };
        }
    }
}
