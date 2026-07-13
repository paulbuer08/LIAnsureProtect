import type { ListSubmissionsResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function listSubmissions(accessToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  return parseJsonResponse<ListSubmissionsResponse>(response);
}
