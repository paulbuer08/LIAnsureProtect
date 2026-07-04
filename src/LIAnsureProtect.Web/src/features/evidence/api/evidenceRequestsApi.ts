import { downloadDocumentWithToken } from "../../../lib/documentDownload";
import type {
  ListEvidenceRequestsResponse,
  QuoteEvidenceRequest,
  RespondToEvidenceRequest,
} from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

async function parseJsonResponse<T>(
  response: Response,
  notFoundMessage: string,
) {
  if (response.status === 404) {
    throw new Error(notFoundMessage);
  }

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as T;
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

export async function listEvidenceRequests(accessToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/evidence-requests`, {
    headers: authHeaders(accessToken),
  });

  return parseJsonResponse<ListEvidenceRequestsResponse>(
    response,
    "Evidence requests were not found.",
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
  formData.append("responseText", request.responseText);

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
