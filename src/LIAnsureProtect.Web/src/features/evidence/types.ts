import type { QuoteEvidenceRequest } from "../underwriting/types";

export type { QuoteEvidenceRequest };

export type ListEvidenceRequestsResponse = {
  evidenceRequests: EvidenceRequestSummary[];
  nextCursor?: string | null;
};

export type EvidenceRequestSummary = {
  evidenceRequestId: string;
  quoteId: string;
  submissionId: string;
  submissionReference?: string;
  companyName?: string;
  documentRequirement?: "Required" | "Optional" | "NarrativeOnly";
  category: string;
  title: string;
  description: string;
  dueAtUtc: string;
  status: string;
  isOverdue: boolean;
  daysUntilDue: number;
  requestedAtUtc: string;
  reviewDecision: string;
  remediationGuidance?: string | null;
  updatedAtUtc: string;
};

export type EvidenceRequestFilters = {
  status?: string;
  category?: string;
  quoteId?: string;
  overdue?: boolean;
  cursor?: string;
  pageSize?: number;
  search?: string;
  reviewDecision?: string;
  documentRequirement?: string;
};

export type RespondToEvidenceRequest = {
  respondentName: string;
  respondentTitle: string;
  respondentEmail: string;
  respondentPhone?: string | null;
  responseText?: string | null;
  otherConcerns?: string | null;
  attachments?: File[];
  attachmentFileName?: string | null;
  attachmentContentType?: string | null;
  attachmentSizeBytes?: number | null;
};
