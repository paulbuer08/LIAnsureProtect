import type { CreateQuoteRequest, CreateQuoteResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

function createIdempotencyKey() {
  return globalThis.crypto?.randomUUID?.() ?? `quote-${Date.now()}`;
}

export async function createQuote(
  accessToken: string,
  submissionId: string,
  request: CreateQuoteRequest,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/submissions/${submissionId}/quotes`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
        "Idempotency-Key": createIdempotencyKey(),
      },
      body: JSON.stringify(request),
    },
  );

  return parseJsonResponse<CreateQuoteResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
