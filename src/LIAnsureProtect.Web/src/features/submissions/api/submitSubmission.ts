import type { SubmitSubmissionResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

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

  return parseJsonResponse<SubmitSubmissionResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
