import type { ListSubmissionsResponse, SubmissionListFilters } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function listSubmissions(accessToken: string, filters: SubmissionListFilters = {}) {
  const parameters = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== "") parameters.set(key, String(value));
  });
  const query = parameters.size > 0 ? `?${parameters.toString()}` : "";
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions${query}`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  return parseJsonResponse<ListSubmissionsResponse>(response);
}
