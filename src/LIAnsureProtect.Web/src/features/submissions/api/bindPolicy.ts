import type { BindPolicyRequest, BindPolicyResponse } from "../types";

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

  if (response.status === 404) {
    throw new Error("Quote was not found.");
  }

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as BindPolicyResponse;
}
