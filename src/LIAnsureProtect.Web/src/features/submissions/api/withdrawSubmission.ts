import type { WithdrawSubmissionResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function withdrawSubmission(accessToken: string, submissionId: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions/${submissionId}/withdraw`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  return parseJsonResponse<WithdrawSubmissionResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
