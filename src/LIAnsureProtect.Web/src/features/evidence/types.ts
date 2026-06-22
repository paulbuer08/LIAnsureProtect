import type { QuoteEvidenceRequest } from "../underwriting/types";

export type { QuoteEvidenceRequest };

export type ListEvidenceRequestsResponse = {
  evidenceRequests: QuoteEvidenceRequest[];
};

export type RespondToEvidenceRequest = {
  respondentName: string;
  respondentTitle: string;
  responseText: string;
  attachmentFileName?: string | null;
  attachmentContentType?: string | null;
  attachmentSizeBytes?: number | null;
};
