import type { AcceptQuoteRequest, AcceptQuoteResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

function createIdempotencyKey() {
  return globalThis.crypto?.randomUUID?.() ?? `accept-${Date.now()}`;
}

export async function acceptQuote(
  accessToken: string,
  quoteId: string,
  request: AcceptQuoteRequest,
) {
  const response = await fetch(`${apiBaseUrl}/api/v1/quotes/${quoteId}/accept`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
      "Idempotency-Key": createIdempotencyKey(),
    },
    body: JSON.stringify(request),
  });

  return parseJsonResponse<AcceptQuoteResponse>(response, {
    notFoundMessage: "Quote was not found.",
  });
}
