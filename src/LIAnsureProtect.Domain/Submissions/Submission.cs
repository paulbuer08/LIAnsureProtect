namespace LIAnsureProtect.Domain.Submissions;

public sealed class Submission
{
    private Submission(
        Guid id,
        string applicantName,
        string applicantEmail,
        string companyName,
        SubmissionStatus status,
        DateTime createdAtUtc)
    {
        Id = id;
        ApplicantName = applicantName;
        ApplicantEmail = applicantEmail;
        CompanyName = companyName;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string ApplicantName { get; }

    public string ApplicantEmail { get; }

    public string CompanyName { get; }

    public SubmissionStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public static Submission CreateDraft(
        string applicantName,
        string applicantEmail,
        string companyName,
        DateTime createdAtUtc)
    {
        return new Submission(
            Guid.NewGuid(),
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
    }

    public void Withdraw()
    {
        if (Status == SubmissionStatus.Withdrawn)
            return;

        Status = SubmissionStatus.Withdrawn;
    }
}
