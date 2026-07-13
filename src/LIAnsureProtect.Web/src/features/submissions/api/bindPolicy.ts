import type { BindPolicyRequest, BindPolicyResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

function createIdempotencyKey() {
  return globalThis.crypto?.randomUUID?.() ?? `bind-${Date.now()}`;
}

export async function bindPolicy(
  accessToken: string,
  quoteId: string,
  request: BindPolicyRequest,
) {
  const response = await fetch(`${apiBaseUrl}/api/v1/quotes/${quoteId}/bind`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
      "Idempotency-Key": createIdempotencyKey(),
    },
    body: JSON.stringify(request),
  });

  return parseJsonResponse<BindPolicyResponse>(response, {
    notFoundMessage: "Quote was not found.",
  });
}
