using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Domain.Quotes;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations;

public sealed record AssignQuoteReferralToMeCommand(Guid QuoteId)
    : IRequest<QuoteReferralOperationResult?>;

public sealed record ReleaseQuoteReferralAssignmentCommand(Guid QuoteId)
    : IRequest<QuoteReferralOperationResult?>;

public sealed record TriageQuoteReferralOperationCommand(
    Guid QuoteId,
    ReferralPriority Priority,
    ReferralOperationStatus Status,
    DateTime DueAtUtc)
    : IRequest<QuoteReferralOperationResult?>;

public sealed record AddQuoteReferralNoteCommand(Guid QuoteId, string Note)
    : IRequest<QuoteReferralNoteResult?>;

public sealed record AddQuoteReferralTaskCommand(
    Guid QuoteId,
    string Title,
    DateTime DueAtUtc)
    : IRequest<QuoteReferralTaskResult?>;

public sealed record CompleteQuoteReferralTaskCommand(Guid QuoteId, Guid TaskId)
    : IRequest<QuoteReferralTaskResult?>;

public sealed record GetQuoteReferralTimelineQuery(Guid QuoteId)
    : IRequest<QuoteReferralTimelineResult?>;

public sealed record QuoteReferralOperationResult(
    Guid QuoteId,
    string? AssignedUnderwriterUserId,
    string Priority,
    DateTime DueAtUtc,
    bool IsSlaBreached,
    string Status,
    int OpenTaskCount,
    DateTime? LatestTimelineAtUtc);

public sealed record QuoteReferralNoteResult(
    Guid NoteId,
    Guid QuoteId,
    string Note,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed record QuoteReferralTaskResult(
    Guid TaskId,
    Guid QuoteId,
    string Title,
    DateTime DueAtUtc,
    bool IsCompleted,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    string? CompletedByUserId,
    DateTime? CompletedAtUtc);

public sealed record QuoteReferralTimelineResult(
    Guid QuoteId,
    IReadOnlyCollection<QuoteReferralTimelineEntryResult> Entries);

public sealed record QuoteReferralTimelineEntryResult(
    string EntryType,
    string Summary,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed class AssignQuoteReferralToMeCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AssignQuoteReferralToMeCommand, QuoteReferralOperationResult?>
{
    public async Task<QuoteReferralOperationResult?> Handle(
        AssignQuoteReferralToMeCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        operation.AssignTo(CurrentUnderwriterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteReferralOperationResultFactory.FromOperation(operation);
    }
}

public sealed class ReleaseQuoteReferralAssignmentCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ReleaseQuoteReferralAssignmentCommand, QuoteReferralOperationResult?>
{
    public async Task<QuoteReferralOperationResult?> Handle(
        ReleaseQuoteReferralAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        operation.ReleaseAssignment(CurrentUnderwriterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteReferralOperationResultFactory.FromOperation(operation);
    }
}

public sealed class TriageQuoteReferralOperationCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<TriageQuoteReferralOperationCommand, QuoteReferralOperationResult?>
{
    public async Task<QuoteReferralOperationResult?> Handle(
        TriageQuoteReferralOperationCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        operation.Triage(
            CurrentUnderwriterUser.GetRequiredUserId(currentUser),
            request.Priority,
            request.Status,
            request.DueAtUtc,
            DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteReferralOperationResultFactory.FromOperation(operation);
    }
}

public sealed class AddQuoteReferralNoteCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AddQuoteReferralNoteCommand, QuoteReferralNoteResult?>
{
    public async Task<QuoteReferralNoteResult?> Handle(
        AddQuoteReferralNoteCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        var note = operation.AddNote(CurrentUnderwriterUser.GetRequiredUserId(currentUser), request.Note, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuoteReferralNoteResult(
            note.Id,
            note.QuoteId,
            note.Note,
            note.CreatedByUserId,
            note.CreatedAtUtc);
    }
}

public sealed class AddQuoteReferralTaskCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AddQuoteReferralTaskCommand, QuoteReferralTaskResult?>
{
    public async Task<QuoteReferralTaskResult?> Handle(
        AddQuoteReferralTaskCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        var task = operation.AddTask(
            CurrentUnderwriterUser.GetRequiredUserId(currentUser),
            request.Title,
            request.DueAtUtc,
            DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteReferralTaskResultFactory.FromTask(task);
    }
}

public sealed class CompleteQuoteReferralTaskCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CompleteQuoteReferralTaskCommand, QuoteReferralTaskResult?>
{
    public async Task<QuoteReferralTaskResult?> Handle(
        CompleteQuoteReferralTaskCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        operation.CompleteTask(request.TaskId, CurrentUnderwriterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var task = operation.Tasks.Single(candidate => candidate.Id == request.TaskId);

        return QuoteReferralTaskResultFactory.FromTask(task);
    }
}

public sealed class GetQuoteReferralTimelineQueryHandler(IQuoteRepository quoteRepository)
    : IRequestHandler<GetQuoteReferralTimelineQuery, QuoteReferralTimelineResult?>
{
    public async Task<QuoteReferralTimelineResult?> Handle(
        GetQuoteReferralTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        var reviews = await quoteRepository.ListUnderwritingReviewsAsync(request.QuoteId, cancellationToken);
        var operationEntries = operation.TimelineEntries
            .Select(entry => new QuoteReferralTimelineEntryResult(
                entry.EntryType.ToString(),
                entry.Summary,
                entry.CreatedByUserId,
                entry.CreatedAtUtc));
        var reviewEntries = reviews
            .Select(review => new QuoteReferralTimelineEntryResult(
                ReferralTimelineEntryType.DecisionRecorded.ToString(),
                $"Final underwriting decision audit row recorded: {review.Decision}.",
                review.ReviewedByUserId,
                review.CreatedAtUtc));

        return new QuoteReferralTimelineResult(
            request.QuoteId,
            operationEntries
                .Concat(reviewEntries)
                .OrderBy(entry => entry.CreatedAtUtc)
                .ToList());
    }
}

internal static class QuoteReferralOperationResultFactory
{
    public static QuoteReferralOperationResult FromOperation(QuoteReferralOperation operation)
    {
        return new QuoteReferralOperationResult(
            operation.QuoteId,
            operation.AssignedUnderwriterUserId,
            operation.Priority.ToString(),
            operation.DueAtUtc,
            operation.DueAtUtc < DateTime.UtcNow && operation.Status != ReferralOperationStatus.Closed,
            operation.Status.ToString(),
            operation.Tasks.Count(task => !task.IsCompleted),
            operation.TimelineEntries
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Select(entry => (DateTime?)entry.CreatedAtUtc)
                .FirstOrDefault());
    }
}

internal static class QuoteReferralTaskResultFactory
{
    public static QuoteReferralTaskResult FromTask(QuoteReferralFollowUpTask task)
    {
        return new QuoteReferralTaskResult(
            task.Id,
            task.QuoteId,
            task.Title,
            task.DueAtUtc,
            task.IsCompleted,
            task.CreatedByUserId,
            task.CreatedAtUtc,
            task.CompletedByUserId,
            task.CompletedAtUtc);
    }
}

internal static class CurrentUnderwriterUser
{
    public static string GetRequiredUserId(ICurrentUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required to update referral operations.")
            : currentUser.UserId;
    }
}
