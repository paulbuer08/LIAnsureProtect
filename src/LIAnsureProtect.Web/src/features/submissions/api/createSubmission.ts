import type {
  CreateSubmissionRequest,
  CreateSubmissionResponse,
} from "../types";
import { parseJsonResponse } from "../../../lib/apiClient";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function createSubmission(
  accessToken: string,
  request: CreateSubmissionRequest,
  idempotencyKey: string,
) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: JSON.stringify(request),
  });

  return parseJsonResponse<CreateSubmissionResponse>(response);
}
