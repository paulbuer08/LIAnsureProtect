using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Submissions;

public sealed class Submission
{
    private readonly List<IDomainEvent> domainEvents = [];

    private Submission(
        Guid id,
        string ownerUserId,
        string applicantName,
        string applicantEmail,
        string companyName,
        SubmissionStatus status,
        DateTime createdAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ApplicantName = applicantName;
        ApplicantEmail = applicantEmail;
        CompanyName = companyName;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string OwnerUserId { get; }

    public string ApplicantName { get; }

    public string ApplicantEmail { get; }

    public string CompanyName { get; }

    public SubmissionStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static Submission CreateDraft(
        string applicantName,
        string applicantEmail,
        string companyName,
        string ownerUserId,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));

        return new Submission(
            Guid.NewGuid(),
            ownerUserId,
            applicantName,
            applicantEmail,
            companyName,
            SubmissionStatus.Draft,
            createdAtUtc);
    }

    public void Submit()
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidOperationException("Only draft submissions can be submitted.");

        Status = SubmissionStatus.Submitted;

        domainEvents.Add(new SubmissionSubmittedDomainEvent(
            Id,
            OwnerUserId,
            DateTime.UtcNow));
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    public void Withdraw()
    {
        if (Status == SubmissionStatus.Withdrawn)
            return;

        Status = SubmissionStatus.Withdrawn;
    }
}
