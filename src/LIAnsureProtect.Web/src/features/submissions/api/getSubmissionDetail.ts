import type { SubmissionDetailResponse } from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function getSubmissionDetail(
  accessToken: string,
  submissionId: string,
) {
  const response = await fetch(
    `${apiBaseUrl}/api/v1/submissions/${submissionId}`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    },
  );

  return parseJsonResponse<SubmissionDetailResponse>(response, {
    notFoundMessage: "Submission was not found.",
  });
}
