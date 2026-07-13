import { parseJsonResponse } from "../../../lib/apiClient";
import type { OwnedQuoteDetail } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function getQuoteDetail(
  accessToken: string,
  submissionId: string,
  quoteId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/submissions/${submissionId}/quotes/${quoteId}`,
    { headers: { Authorization: `Bearer ${accessToken}` } },
  );

  return parseJsonResponse<OwnedQuoteDetail>(response, {
    notFoundMessage: "Quote was not found.",
  });
}
