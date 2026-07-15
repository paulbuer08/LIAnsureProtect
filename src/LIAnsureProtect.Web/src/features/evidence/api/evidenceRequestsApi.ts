import { downloadDocumentWithToken } from "../../../lib/documentDownload";
import { parseJsonResponse as parseApiJsonResponse } from "../../../lib/apiClient";
import type {
  EvidenceRequestFilters,
  ListEvidenceRequestsResponse,
  QuoteEvidenceRequest,
  RespondToEvidenceRequest,
} from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

async function parseJsonResponse<T>(
  response: Response,
  notFoundMessage: string,
) {
  return parseApiJsonResponse<T>(response, { notFoundMessage });
}

function authHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}

export async function downloadOwnerEvidenceDocument(
  accessToken: string,
  evidenceRequestId: string,
  documentId: string,
  fileName: string,
) {
  await downloadDocumentWithToken(
    `${apiBaseUrl}/api/v1/evidence-requests/${evidenceRequestId}/documents/${documentId}/download`,
    accessToken,
    fileName,
  );
}

export async function listEvidenceRequests(
  accessToken: string,
  filters: EvidenceRequestFilters = {},
) {
  const search = new URLSearchParams();
  if (filters.status) search.set("status", filters.status);
  if (filters.category) search.set("category", filters.category);
  if (filters.quoteId) search.set("quoteId", filters.quoteId);
  if (filters.overdue !== undefined) search.set("overdue", String(filters.overdue));
  if (filters.cursor) search.set("cursor", filters.cursor);
  if (filters.search) search.set("search", filters.search);
  if (filters.reviewDecision) search.set("reviewDecision", filters.reviewDecision);
  if (filters.documentRequirement) search.set("documentRequirement", filters.documentRequirement);
  if (filters.quoteDisposition && filters.quoteDisposition !== "All") {
    search.set("quoteDisposition", filters.quoteDisposition);
  }
  search.set("pageSize", String(filters.pageSize ?? 12));
  const response = await fetch(`${apiBaseUrl}/api/v1/evidence-requests?${search}`, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ListEvidenceRequestsResponse>(
    response,
    "Evidence requests were not found.",
  );
}

export async function getEvidenceRequest(
  accessToken: string,
  evidenceRequestId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/evidence-requests/${evidenceRequestId}`,
    { headers: authHeaders(accessToken) },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}

export async function respondToEvidenceRequest(
  accessToken: string,
  evidenceRequestId: string,
  request: RespondToEvidenceRequest,
) {
  const formData = new FormData();
  formData.append("respondentName", request.respondentName);
  formData.append("respondentTitle", request.respondentTitle);
  formData.append("respondentEmail", request.respondentEmail);
  if (request.respondentMobileNumber) {
    formData.append("respondentMobileNumber", request.respondentMobileNumber);
  }
  if (request.respondentTelephoneNumber) {
    formData.append("respondentTelephoneNumber", request.respondentTelephoneNumber);
  }
  if (request.responseText) formData.append("responseText", request.responseText);
  if (request.otherConcerns) formData.append("otherConcerns", request.otherConcerns);

  for (const attachment of request.attachments ?? []) {
    formData.append("attachments", attachment);
  }

  const response = await fetch(
    `${apiBaseUrl}/api/v1/evidence-requests/${evidenceRequestId}/respond`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
      body: formData,
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}

export async function uploadReplacementEvidenceDocuments(
  accessToken: string,
  evidenceRequestId: string,
  attachments: File[],
) {
  const formData = new FormData();

  for (const attachment of attachments) {
    formData.append("attachments", attachment);
  }

  const response = await fetch(
    `${apiBaseUrl}/api/v1/evidence-requests/${evidenceRequestId}/documents`,
    {
      method: "POST",
      headers: authHeaders(accessToken),
      body: formData,
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}
