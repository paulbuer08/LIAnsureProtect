import type { ListSubmissionsResponse } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function listSubmissions(accessToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as ListSubmissionsResponse;
}
