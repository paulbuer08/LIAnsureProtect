using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class ControlAssurancePolicyTests
{
    [Fact]
    public void Positive_material_control_claims_require_targeted_evidence()
    {
        var decisions = ControlAssurancePolicy.Evaluate(new CreateQuoteAssuranceInput(
            1_000_000m,
            CyberSecurityControlStatus.Implemented,
            CyberSecurityControlStatus.Implemented,
            BackupMaturity.Mature,
            true,
            0,
            SensitiveDataExposure.Low));

        Assert.Equal(5, decisions.Count);
        Assert.All(decisions, decision => Assert.True(decision.EvidenceRequired));
        Assert.All(decisions, decision => Assert.NotEmpty(decision.EvidenceReason));
    }

    [Fact]
    public void Weak_or_unknown_claims_receive_risk_treatment_without_proof_of_absence()
    {
        var decisions = ControlAssurancePolicy.Evaluate(new CreateQuoteAssuranceInput(
            250_000m,
            CyberSecurityControlStatus.NotImplemented,
            CyberSecurityControlStatus.Partial,
            BackupMaturity.Weak,
            false,
            0,
            SensitiveDataExposure.Unknown));

        Assert.All(decisions, decision => Assert.False(decision.EvidenceRequired));
    }

    [Fact]
    public void Quote_command_requires_explicit_named_attestation()
    {
        var validator = new CreateQuoteCommandValidator();
        var command = new CreateQuoteCommand(
            Guid.NewGuid(),
            CyberIndustryClass.ProfessionalServices,
            AnnualRevenueBand.From10MTo50M,
            1_000_000m,
            10_000m,
            CyberSecurityControlStatus.Implemented,
            CyberSecurityControlStatus.Implemented,
            BackupMaturity.Mature,
            true,
            0,
            SensitiveDataExposure.Moderate);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.AttestationAccepted));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.AttestedByName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.AttestedByTitle));
    }
}
