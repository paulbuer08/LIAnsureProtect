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

function jsonHeaders(accessToken: string) {
  return {
    ...authHeaders(accessToken),
    "Content-Type": "application/json",
  };
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
  const response = await fetch(
    `${apiBaseUrl}/api/v1/evidence-requests/${evidenceRequestId}/respond`,
    {
      method: "POST",
      headers: jsonHeaders(accessToken),
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<QuoteEvidenceRequest>(
    response,
    "Evidence request was not found.",
  );
}
