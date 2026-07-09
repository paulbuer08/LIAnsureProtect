import type { SubmitSubmissionResponse } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function submitSubmission(
  accessToken: string,
  submissionId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/submissions/${submissionId}/submit`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    },
  );

  if (response.status === 404) {
    throw new Error("Submission was not found.");
  }

  if (!response.ok) {
    const errorBody = await response.text();

    throw new Error(
      `API request failed with ${response.status} ${response.statusText}: ${errorBody}`,
    );
  }

  return (await response.json()) as SubmitSubmissionResponse;
}
