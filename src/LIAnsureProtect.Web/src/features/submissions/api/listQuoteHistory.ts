import { parseJsonResponse } from "../../../lib/apiClient";
import type { QuoteHistoryResponse } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function listQuoteHistory(accessToken: string, submissionId: string) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/submissions/${submissionId}/quotes`,
    { headers: { Authorization: `Bearer ${accessToken}` } },
  );

  return parseJsonResponse<QuoteHistoryResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
