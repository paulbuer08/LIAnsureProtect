import type { CurrentUser } from "../../lib/currentUserApi";
import type { NotificationInboxItem } from "./types";

export type NotificationAction = { label: string; to: string };

const capabilities = {
  readSubmission: "Submissions.Read",
  respondToEvidence: "EvidenceRequests.Respond",
  readPolicy: "Policies.Read",
  readClaim: "Claims.Read",
  underwriteQuote: "Quotes.Underwrite",
  adjudicateClaim: "Claims.Adjudicate",
} as const;

function hasCapability(user: CurrentUser | undefined, capability: string) {
  if (user?.capabilities) return user.capabilities.includes(capability);

  // Compatibility for older test fixtures and rolling deployments. Production /me responses include
  // explicit capabilities derived from the same server role policy table.
  const roles = user?.roles ?? [];
  if (capability === capabilities.underwriteQuote)
    return roles.includes("Underwriter") || roles.includes("Admin");
  if (capability === capabilities.adjudicateClaim)
    return roles.includes("ClaimsAdjuster") || roles.includes("Admin");
  return roles.some((role) => ["Customer", "Broker", "Admin"].includes(role));
}

export function resolveNotificationAction(
  notification: NotificationInboxItem,
  user: CurrentUser | undefined,
): NotificationAction | null {
  const { subjectReferenceType, subjectReferenceId, attributes, scope } = notification;

  if (subjectReferenceType === "evidence-request") {
    const evidenceRequestId = attributes.evidenceRequestId ?? subjectReferenceId;
    if (scope === "team") {
      const quoteId = attributes.quoteId;
      return evidenceRequestId && quoteId && hasCapability(user, capabilities.underwriteQuote)
        ? {
            label: "Review evidence response",
            to: `/underwriting/quote-referrals?quoteId=${encodeURIComponent(quoteId)}&evidenceRequestId=${encodeURIComponent(evidenceRequestId)}`,
          }
        : null;
    }
    return evidenceRequestId && hasCapability(user, capabilities.respondToEvidence)
      ? { label: "Open evidence request", to: `/evidence-requests/${evidenceRequestId}` }
      : null;
  }

  if (subjectReferenceType === "policy") {
    const policyId = attributes.policyId ?? subjectReferenceId;
    return policyId && hasCapability(user, capabilities.readPolicy)
      ? { label: "View policy", to: `/policies/${policyId}` }
      : null;
  }

  if (subjectReferenceType === "quote") {
    const quoteId = attributes.quoteId ?? subjectReferenceId;
    if (scope === "team") {
      return quoteId && hasCapability(user, capabilities.underwriteQuote)
        ? { label: "Open underwriting", to: `/underwriting/quote-referrals?quoteId=${encodeURIComponent(quoteId)}` }
        : null;
    }
    const submissionId = attributes.submissionId;
    return submissionId && quoteId && hasCapability(user, capabilities.readSubmission)
      ? { label: "View quote", to: `/submissions/${submissionId}/quotes/${quoteId}` }
      : null;
  }

  if (subjectReferenceType === "reassessment_request") {
    if (scope === "team") {
      const quoteId = attributes.quoteId;
      return hasCapability(user, capabilities.underwriteQuote)
        ? {
            label: "Review reassessment",
            to: quoteId
              ? `/underwriting/quote-referrals?quoteId=${encodeURIComponent(quoteId)}`
              : "/underwriting/quote-referrals",
          }
        : null;
    }
    const submissionId = attributes.submissionId;
    const quoteId = attributes.quoteId;
    return attributes.status === "Approved" && submissionId && quoteId
      ? { label: "View new quote", to: `/submissions/${submissionId}/quotes/${quoteId}` }
      : submissionId && hasCapability(user, capabilities.readSubmission)
        ? { label: "Open submission", to: `/submissions/${submissionId}` }
        : null;
  }

  if (subjectReferenceType === "submission") {
    const submissionId = attributes.submissionId ?? subjectReferenceId;
    return submissionId && hasCapability(user, capabilities.readSubmission)
      ? { label: "Open submission", to: `/submissions/${submissionId}` }
      : null;
  }

  if (subjectReferenceType === "claim") {
    const claimId = attributes.claimId ?? subjectReferenceId;
    if (!claimId) return null;
    if (scope === "team") {
      return hasCapability(user, capabilities.adjudicateClaim)
        ? { label: "Open claim", to: `/claims/adjudication?claimId=${encodeURIComponent(claimId)}` }
        : null;
    }
    return hasCapability(user, capabilities.readClaim)
      ? { label: "Open claim", to: `/claims/${claimId}` }
      : null;
  }

  return null;
}
