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

    [Fact]
    public void Implemented_control_claim_rejects_internally_inconsistent_details()
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
            SensitiveDataExposure.Low,
            AttestationAccepted: true,
            AttestedByName: "Jane Applicant",
            AttestedByTitle: "CFO",
            ControlDetails: new CyberControlDetails(
                false, false, false, false, false,
                10, false, false, false,
                false, false, false, 72, 72,
                false, false, false, false,
                false, false, [], "Unknown"));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Implemented MFA", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Implemented EDR", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Mature backups", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("incident plan", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Low sensitive-data", StringComparison.Ordinal));
    }

    [Fact]
    public void Reassessment_accepts_a_changed_detailed_control_answer()
    {
        var previousDecision = ControlAssurancePolicy.Evaluate(CreateAssuranceInput(
            new CyberControlDetails(
                true, true, true, true, false,
                98, true, true, true,
                true, true, true, 4, 8,
                true, true, true, true,
                true, true, ["Personal data"], "Moderate")));
        var previous = previousDecision
            .Select(decision => ControlAssertion.Create(
                Guid.NewGuid(),
                1,
                decision.ControlType,
                decision.ClaimedState,
                decision.EvidenceRequired,
                decision.EvidenceReason,
                DateTime.UtcNow,
                decision.DetailsJson))
            .ToArray();
        var current = ControlAssurancePolicy.Evaluate(CreateAssuranceInput(
            new CyberControlDetails(
                true, true, true, true, true,
                98, true, true, true,
                true, true, true, 4, 8,
                true, true, true, true,
                true, true, ["Personal data"], "Moderate")));

        var result = ControlAssurancePolicy.ApplyReassessmentRules(current, previous);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Reassessment_ignores_json_property_order_when_details_are_unchanged()
    {
        var current = ControlAssurancePolicy.Evaluate(CreateAssuranceInput(
            new CyberControlDetails(
                true, true, true, true, false,
                98, true, true, true,
                true, true, true, 4, 8,
                true, true, true, true,
                true, true, ["Personal data"], "Moderate")));
        var previous = current
            .Select(decision => ControlAssertion.Create(
                Guid.NewGuid(),
                1,
                decision.ControlType,
                decision.ClaimedState,
                decision.EvidenceRequired,
                decision.EvidenceReason,
                DateTime.UtcNow,
                ReverseJsonPropertyOrder(decision.DetailsJson)))
            .ToArray();

        var exception = Assert.Throws<LIAnsureProtect.Application.Common.Exceptions.BusinessConflictException>(
            (Action)(() => ControlAssurancePolicy.ApplyReassessmentRules(current, previous)));

        Assert.Equal("quote.reassessment.no_changes", exception.Code);
    }

    private static CreateQuoteAssuranceInput CreateAssuranceInput(CyberControlDetails details) => new(
        1_000_000m,
        CyberSecurityControlStatus.Implemented,
        CyberSecurityControlStatus.Implemented,
        BackupMaturity.Mature,
        true,
        0,
        SensitiveDataExposure.Moderate,
        details);

    private static string ReverseJsonPropertyOrder(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Reverse().ToArray();
        return $"{{{string.Join(',', properties.Select(property => $"\"{property.Name}\":{property.Value.GetRawText()}"))}}}";
    }
}
