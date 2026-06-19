import type {
  CreateSubmissionRequest,
  CreateSubmissionResponse,
} from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function createSubmission(
  accessToken: string,
  request: CreateSubmissionRequest,
) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as CreateSubmissionResponse;
}
