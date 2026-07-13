import type { SubmissionDetailResponse, UpdateSubmissionRequest } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function updateSubmission(
  accessToken: string,
  submissionId: string,
  request: UpdateSubmissionRequest,
) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions/${submissionId}`, {
    method: "PUT",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  return parseJsonResponse<SubmissionDetailResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
