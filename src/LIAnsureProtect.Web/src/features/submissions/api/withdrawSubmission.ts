import type { WithdrawSubmissionResponse } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function withdrawSubmission(accessToken: string, submissionId: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions/${submissionId}/withdraw`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    throw new Error((await response.text()) || "Unable to withdraw the submission.");
  }

  return (await response.json()) as WithdrawSubmissionResponse;
}
