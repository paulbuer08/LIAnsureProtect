using LIAnsureProtect.Application.Common.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Idempotency;

public sealed class EfCoreIdempotencyService(SubmissionDbContext dbContext) : IIdempotencyService
{
    private const int MaximumKeyLength = 200;

    public async Task<IdempotencyExecutionResult> ExecuteAsync(
        IdempotencyRequest request,
        Func<CancellationToken, Task<IdempotencyActionResponse>> operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return IdempotencyExecutionResult.Conflict("An Idempotency-Key header value is required.");

        if (request.Key.Length > MaximumKeyLength)
            return IdempotencyExecutionResult.Conflict("The Idempotency-Key header value must be 200 characters or fewer.");

        var existingRecord = await FindRecordAsync(request.Key, cancellationToken);
        if (existingRecord is not null)
            return BuildResultFromExistingRecord(existingRecord, request);

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var recordFoundInsideTransaction = await FindRecordAsync(request.Key, cancellationToken);
            if (recordFoundInsideTransaction is not null)
                return BuildResultFromExistingRecord(recordFoundInsideTransaction, request);

            var record = IdempotencyRecord.Start(
                request.Key,
                request.OwnerUserId,
                request.ActionName,
                request.RequestFingerprint,
                DateTime.UtcNow);

            await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                dbContext.Entry(record).State = EntityState.Detached;

                return IdempotencyExecutionResult.Conflict(
                    "Another request is already using this idempotency key. Retry the same request after the first request finishes.");
            }

            var response = await operation(cancellationToken);

            record.MarkCompleted(
                response.StatusCode,
                response.Body,
                response.ContentType,
                response.Location,
                DateTime.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return IdempotencyExecutionResult.Completed(response);
        });
    }

    private Task<IdempotencyRecord?> FindRecordAsync(
        string key,
        CancellationToken cancellationToken)
    {
        return dbContext.IdempotencyRecords.SingleOrDefaultAsync(
            record => record.Key == key,
            cancellationToken);
    }

    private static IdempotencyExecutionResult BuildResultFromExistingRecord(
        IdempotencyRecord record,
        IdempotencyRequest request)
    {
        if (!record.Matches(
                request.OwnerUserId,
                request.ActionName,
                request.RequestFingerprint))
        {
            return IdempotencyExecutionResult.Conflict(
                "The supplied Idempotency-Key was already used for a different user, action, or request payload.");
        }

        if (!record.IsCompleted
            || record.ResponseStatusCode is null
            || record.ResponseBody is null
            || record.ResponseContentType is null)
        {
            return IdempotencyExecutionResult.Conflict(
                "A request with this Idempotency-Key is already in progress. Retry the same request after it finishes.");
        }

        return IdempotencyExecutionResult.Replayed(
            new IdempotencyActionResponse(
                record.ResponseStatusCode.Value,
                record.ResponseBody,
                record.ResponseContentType,
                record.ResponseLocation));
    }
}
